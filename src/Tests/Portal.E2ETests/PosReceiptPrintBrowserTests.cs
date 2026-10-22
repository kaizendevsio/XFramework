using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using POS.Domain.Shared.Contracts;
using XFramework.Portal.Features.POS.Receipts;

namespace Portal.E2ETests;

[TestFixture, NonParallelizable, Category("Area:PortalLayoutBrowser"), Category("Module:POS")]
public sealed class PosReceiptPrintBrowserTests : PageTest
{
    [TestCase(58, PosReceiptTemplate.Classic)]
    [TestCase(80, PosReceiptTemplate.Classic)]
    [TestCase(58, PosReceiptTemplate.Compact)]
    [TestCase(80, PosReceiptTemplate.Compact)]
    [TestCase(58, PosReceiptTemplate.Bold)]
    [TestCase(80, PosReceiptTemplate.Bold)]
    public async Task PrintDocument_AllTemplatesAndWidths_LogoFitsAndPdfUsesThermalPaper(int width, PosReceiptTemplate template)
    {
        await Page.SetViewportSizeAsync(390, 900);
        await Page.SetContentAsync("<html><body><p>PRIVATE PORTAL AUDIT</p></body></html>");
        var logo = await Page.EvaluateAsync<string>("""
            () => { const c=document.createElement('canvas'); c.width=240; c.height=80;
              const x=c.getContext('2d'); x.fillStyle='white'; x.fillRect(0,0,240,80);
              x.fillStyle='black'; x.fillRect(4,8,60,60); x.fillStyle='white'; x.font='bold 42px Arial'; x.fillText('N',18,54);
              x.fillStyle='black'; x.font='bold 32px Arial'; x.fillText('NORTH',76,50); return c.toDataURL('image/png'); }
            """);
        var settings = new PosReceiptSettings
        {
            PaperWidthMm = width, Template = template, LogoDataUrl = logo, MerchantName = "North Market",
            Header = "123 Market Street\nDowntown", Footer = "Thank you for shopping locally.\nReturns with receipt within 14 days."
        };
        var sale = PosReceiptTests.Sale();
        sale = sale with
        {
            Lines = [.. sale.Lines, new() { LineNumber = 2, ProductName = "Long product name with an exceptionally-long-unbroken-variant-description-that-must-wrap", Quantity = 1.25m, UnitPrice = 1234.56m, LineTotal = 1543.20m }],
            SubtotalAmount = 1555.20m, TotalAmount = 1555.20m,
            Payments = [sale.Payments[0] with { Amount = 1555.20m, CashTenderedAmount = 1600m, ChangeAmount = 44.80m }]
        };
        var html = PosReceiptDocument.Create(sale, settings, "Store", "USD");
        var script = await File.ReadAllTextAsync(Path.Combine(FindRoot(), "src/Presentation/XFramework.Portal.Features.POS/wwwroot/js/receipt-print.js"));
        await Page.EvaluateAsync("""
            async ({html,script,width}) => {
              const module=await import('data:text/javascript;base64,'+btoa(script));
              const frame=await module.createReceiptFrame(html,width); frame.style.left='0';
            }
            """, new { html, script, width });
        var frame = Page.Frames.Single(x => x != Page.MainFrame);
        (await frame.Locator("body").InnerTextAsync()).Should().NotContain("PRIVATE PORTAL AUDIT").And.NotContain("AUDIT-SECRET");
        (await frame.Locator("body").BoundingBoxAsync())!.Width.Should().BeApproximately(width * 96f / 25.4f, 1f);
        (await frame.EvaluateAsync<bool>("() => document.images[0].naturalWidth > 0")).Should().BeTrue();
        (await frame.EvaluateAsync<bool>("() => document.body.scrollWidth <= Math.ceil(document.body.getBoundingClientRect().width)")).Should().BeTrue();
        (await frame.EvaluateAsync<bool>("""
            () => [...document.querySelectorAll('.row')].every(row => {
                const [a,b]=[...row.children].map(x=>x.getBoundingClientRect()); return a.right <= b.left + 1;
            })
            """)).Should().BeTrue();
        var logoBox = (await frame.Locator(".logo").BoundingBoxAsync())!;
        var titleBox = (await frame.Locator("h1").BoundingBoxAsync())!;
        (logoBox.Y + logoBox.Height).Should().BeLessThanOrEqualTo(titleBox.Y);
        var printHtml = await frame.ContentAsync();
        await Page.SetContentAsync(printHtml);
        await Page.EvaluateAsync("() => Promise.all([...document.images].map(x=>x.decode()))");
        var output = Path.Combine(TestContext.CurrentContext.WorkDirectory, "receipt-artifacts");
        Directory.CreateDirectory(output);
        var stem = Path.Combine(output, $"{template.ToString().ToLowerInvariant()}-{width}mm");
        await Page.Locator(".receipt").ScreenshotAsync(new() { Path = stem + ".png" });
        var pdf = await Page.PdfAsync(new() { Path = stem + ".pdf", PreferCSSPageSize = true, PrintBackground = true });
        var match = Regex.Match(Encoding.Latin1.GetString(pdf), @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]");
        match.Success.Should().BeTrue();
        double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture).Should().BeApproximately(width * 72d / 25.4, 1);
        Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type /Page\b").Count.Should().Be(1);
        TestContext.AddTestAttachment(stem + ".pdf");
        TestContext.AddTestAttachment(stem + ".png");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CLAUDE.md"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
