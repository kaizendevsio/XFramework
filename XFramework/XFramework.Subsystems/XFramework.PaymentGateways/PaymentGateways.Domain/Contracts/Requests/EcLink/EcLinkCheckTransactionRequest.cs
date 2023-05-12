using System.Xml.Serialization;

namespace PaymentGateways.Domain.Contracts.Requests.EcLink;

[XmlRoot(ElementName = "Envelope", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
public class EcLinkCheckTransactionRequest
{
    [XmlElement(ElementName = "Header", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
    public EcLinkCheckTransactionRequestHeader EcLinkCheckTransactionRequestHeader { get; set; }

    [XmlElement(ElementName = "Body", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
    public EcLinkCheckTransactionRequestBody EcLinkCheckTransactionRequestBody { get; set; }

    [XmlAttribute(AttributeName = "xsi", Namespace = "http://www.w3.org/2000/xmlns/")]
    public string Xsi { get; set; }

    [XmlAttribute(AttributeName = "xsd", Namespace = "http://www.w3.org/2000/xmlns/")]
    public string Xsd { get; set; }

    [XmlAttribute(AttributeName = "soap", Namespace = "http://www.w3.org/2000/xmlns/")]
    public string Soap { get; set; }

    [XmlText] public string Text { get; set; }

    public string ToXML()
    {
        var serializer = new XmlSerializer(GetType());
        using var stringWriter = new System.IO.StringWriter();
        var serializerNamespaces = new XmlSerializerNamespaces();
        serializerNamespaces.Add("soap", "http://schemas.xmlsoap.org/soap/envelope/");
        serializer.Serialize(stringWriter, this, serializerNamespaces);
        var r = stringWriter.ToString();
        stringWriter.Dispose();
        serializer = null;
        return r;
    }
}


[XmlRoot(ElementName = "AuthHeader", Namespace = "https://ecpay.ph/eclink")]
public class EcLinkCheckTransactionRequestAuthHeader
{
    [XmlElement(ElementName = "merchantCode", Namespace = "https://ecpay.ph/eclink")]
    public string MerchantCode { get; set; }

    [XmlElement(ElementName = "merchantKey", Namespace = "https://ecpay.ph/eclink")]
    public string MerchantKey { get; set; }

    [XmlAttribute(AttributeName = "xmlns", Namespace = "")]
    public string Xmlns { get; set; }

    [XmlText] public string Text { get; set; }
}

[XmlRoot(ElementName = "Header", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
public class EcLinkCheckTransactionRequestHeader
{
    [XmlElement(ElementName = "AuthHeader", Namespace = "https://ecpay.ph/eclink")]
    public EcLinkCheckTransactionRequestAuthHeader EcLinkCheckTransactionRequestAuthHeader { get; set; }
}

[XmlRoot(ElementName = "ConfirmPayment", Namespace = "https://ecpay.ph/eclink")]
public class EcLinkCheckTransactionRequestConfirmPayment
{
    [XmlElement(ElementName = "referenceno", Namespace = "https://ecpay.ph/eclink")]
    public string ReferenceNo { get; set; }

    [XmlAttribute(AttributeName = "xmlns", Namespace = "")]
    public string Xmlns { get; set; }

    [XmlText] public string Text { get; set; }
}

[XmlRoot(ElementName = "Body", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
public class EcLinkCheckTransactionRequestBody
{
    [XmlElement(ElementName = "ConfirmPayment", Namespace = "https://ecpay.ph/eclink")]
    public EcLinkCheckTransactionRequestConfirmPayment EcLinkCheckTransactionRequestConfirmPayment { get; set; }
}