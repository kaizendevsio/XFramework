using System.Xml.Serialization;

namespace PaymentGateways.Domain.Contracts.Requests.EcLink;

[XmlRoot(ElementName = "Envelope", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
public class EcLinkTransactionRequest
{
    [XmlElement(ElementName = "Header", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
    public EcLinkTransactionRequestHeader EcLinkTransactionRequestHeader { get; set; }

    [XmlElement(ElementName = "Body", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
    public EcLinkTransactionRequestBody EcLinkTransactionRequestBody { get; set; }

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
public class EcLinkTransactionRequestAuthHeader
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
public class EcLinkTransactionRequestHeader
{
    [XmlElement(ElementName = "AuthHeader", Namespace = "https://ecpay.ph/eclink")]
    public EcLinkTransactionRequestAuthHeader EcLinkTransactionRequestAuthHeader { get; set; }
}

[XmlRoot(ElementName = "CommitPayment", Namespace = "https://ecpay.ph/eclink")]
public class EcLinkTransactionRequestCommitPayment
{
    [XmlElement(ElementName = "referenceno", Namespace = "https://ecpay.ph/eclink")]
    public string Referenceno { get; set; }

    [XmlElement(ElementName = "amount", Namespace = "https://ecpay.ph/eclink")]
    public string Amount { get; set; }

    [XmlElement(ElementName = "expirydate", Namespace = "https://ecpay.ph/eclink")]
    public string Expirydate { get; set; }

    [XmlElement(ElementName = "remarks", Namespace = "https://ecpay.ph/eclink")]
    public string Remarks { get; set; }

    [XmlAttribute(AttributeName = "xmlns", Namespace = "")]
    public string Xmlns { get; set; }

    [XmlText] public string Text { get; set; }
}

[XmlRoot(ElementName = "Body", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
public class EcLinkTransactionRequestBody
{
    [XmlElement(ElementName = "CommitPayment", Namespace = "https://ecpay.ph/eclink")]
    public EcLinkTransactionRequestCommitPayment EcLinkTransactionRequestCommitPayment { get; set; }
}