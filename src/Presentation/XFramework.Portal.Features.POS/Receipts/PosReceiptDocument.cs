using System.Globalization;
using System.Net;
using System.Text;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Responses;
using POS.Domain.Shared.Enums;

namespace XFramework.Portal.Features.POS.Receipts;

/// <summary>A bounded customer receipt, deliberately excluding operational identifiers and audit fields.</summary>
public static class PosReceiptDocument
{
    public static string Create(PosSaleReceiptResponse sale, PosReceiptSettings settings, string merchantName, string? currencyCode)
    {
        if (!settings.IsValid()) throw new ArgumentException("Receipt settings are invalid.", nameof(settings));
        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Receipt ")
            .Append(E(sale.SaleNumber)).Append("</title><style>").Append(Styles)
            .Append("body{width:").Append(settings.PaperWidthMm).Append("mm} @page{size:")
            .Append(settings.PaperWidthMm).Append("mm 300mm;margin:0}</style></head><body class=\"")
            .Append(settings.Template.ToString().ToLowerInvariant()).Append("\"><main class=\"receipt\">");
        if (!string.IsNullOrEmpty(settings.LogoDataUrl))
            html.Append("<img class=\"logo\" alt=\"Merchant logo\" src=\"").Append(E(settings.LogoDataUrl)).Append("\">");
        html.Append("<h1>").Append(E(string.IsNullOrWhiteSpace(settings.MerchantName) ? merchantName : settings.MerchantName)).Append("</h1>");
        foreach (var section in settings.SectionOrder.Where(section => !settings.HiddenSections.Contains(section)))
        {
            html.Append("<section data-section=\"").Append(section).Append("\">");
            switch (section)
            {
                case PosReceiptSection.Header:
                    html.Append("<p class=\"text center\">").Append(E(settings.Header)).Append("</p>");
                    break;
                case PosReceiptSection.Sale:
                    html.Append("<h2>RECEIPT</h2>");
                    Row(html, "Receipt", sale.SaleNumber);
                    if (sale.CompletedAt is { } completed) Row(html, "Date (UTC)", completed.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
                    // Never imply that a pending or failed payment was completed.
                    if (sale.Status != PosSaleStatus.Completed) Row(html, "Sale status", StatusLabel(sale.Status));
                    if (!string.IsNullOrWhiteSpace(sale.CheckoutMetadata.TableNumber)) Row(html, "Table", sale.CheckoutMetadata.TableNumber);
                    break;
                case PosReceiptSection.Items:
                    html.Append("<h2>ITEMS</h2>");
                    foreach (var line in sale.Lines.OrderBy(x => x.LineNumber))
                    {
                        html.Append("<div class=\"item\"><strong>").Append(E(line.ProductName)).Append("</strong>");
                        if (!string.IsNullOrWhiteSpace(line.VariantName)) html.Append("<div>").Append(E(line.VariantName)).Append("</div>");
                        Row(html, $"{line.Quantity.ToString("0.####", CultureInfo.InvariantCulture)} x {Money(line.UnitPrice, currencyCode)}", Money(line.LineTotal, currencyCode));
                        html.Append("</div>");
                    }
                    break;
                case PosReceiptSection.Totals:
                    Row(html, "Subtotal", Money(sale.SubtotalAmount, currencyCode));
                    Row(html, "Discount", Money(sale.DiscountAmount, currencyCode));
                    Row(html, "Tax", Money(sale.TaxAmount, currencyCode));
                    html.Append("<div class=\"total\">");
                    Row(html, "TOTAL", Money(sale.TotalAmount, currencyCode));
                    html.Append("</div>");
                    break;
                case PosReceiptSection.Payment:
                    foreach (var payment in sale.Payments.Where(x => x.Status is PosPaymentStatus.Captured or PosPaymentStatus.Refunded))
                    {
                        Row(html, payment.Method == PosPaymentMethod.CashDrawer ? "Paid (Cash)" : "Paid (Wallet)", Money(payment.Amount, currencyCode));
                        if (payment.CashTenderedAmount is { } tendered) Row(html, "Cash received", Money(tendered, currencyCode));
                        if (payment.ChangeAmount is { } change) Row(html, "Change", Money(change, currencyCode));
                        if (payment.RefundedAmount > 0) Row(html, "Refunded", Money(payment.RefundedAmount, currencyCode));
                    }
                    break;
                case PosReceiptSection.Footer:
                    html.Append("<p class=\"text center\">").Append(E(settings.Footer)).Append("</p>");
                    break;
            }
            html.Append("</section>");
        }
        return html.Append("</main></body></html>").ToString();
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value);
    private static string StatusLabel(PosSaleStatus status) => status switch
    {
        PosSaleStatus.PaymentPending => "Payment pending",
        PosSaleStatus.PaymentCaptured or PosSaleStatus.InventoryFulfillmentFailed => "Paid - fulfillment pending",
        PosSaleStatus.PaymentFailed => "Payment failed",
        PosSaleStatus.Cancelled => "Cancelled",
        _ => "Unpaid"
    };
    private static void Row(StringBuilder html, string label, string value) => html.Append("<div class=\"row\"><span>")
        .Append(E(label)).Append("</span><span>").Append(E(value)).Append("</span></div>");
    private static string Money(decimal amount, string? currency) =>
        $"{(currency is { Length: 3 } && currency.All(char.IsAsciiLetter) ? currency.ToUpperInvariant() + " " : "")}{amount.ToString("N2", CultureInfo.InvariantCulture)}";

    private const string Styles = """
        *{box-sizing:border-box}html{margin:0;background:#fff;color:#000}body{margin:0;font:12px Arial,sans-serif;letter-spacing:0}
        .receipt{padding:3mm;overflow-wrap:anywhere}.logo{display:block;max-width:36mm;max-height:20mm;object-fit:contain;margin:0 auto 2mm}
        h1{font-size:18px;line-height:1.15;text-align:center;margin:0 0 2mm}h2{font-size:11px;letter-spacing:0;margin:0 0 2mm}
        section{border-top:1px dashed #555;padding:2mm 0}section:empty,section:has(p:empty){display:none}
        p{margin:0}.center{text-align:center}.text{white-space:pre-wrap}.item{margin:0 0 2mm;break-inside:avoid}
        .row{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:2mm;line-height:1.45;break-inside:avoid}
        .row>span:last-child{text-align:right;font-variant-numeric:tabular-nums}.total{font-size:15px;font-weight:700;margin-top:2mm}
        .total .row{grid-template-columns:minmax(0,.6fr) minmax(0,1.4fr)}
        .compact{font-family:'Courier New',monospace;font-size:11px}.compact h1{font-size:15px}.compact section{padding:1.5mm 0}.compact .item{margin-bottom:1mm}
        .bold h1{text-align:left;font-size:21px}.bold h2{border-bottom:2px solid #000;padding-bottom:1mm}.bold section{border-top:1px solid #000}
        .bold .total{border-top:3px solid #000;border-bottom:3px solid #000;padding:2mm 0;font-size:16px}
        @media print{html,body{print-color-adjust:exact;-webkit-print-color-adjust:exact}}
        """;
}
