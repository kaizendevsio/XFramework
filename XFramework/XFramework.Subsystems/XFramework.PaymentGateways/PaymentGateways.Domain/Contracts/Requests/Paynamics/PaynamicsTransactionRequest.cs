using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using PaymentGateways.Domain.Extentions;

namespace PaymentGateways.Domain.Contracts.Requests.Paynamics;

[XmlRoot(ElementName = "Request")]
public class PaynamicsTransactionRequest
{ 
    [XmlElement(ElementName = "orders")] public Orders Orders { get; set; }
    [XmlElement(ElementName = "mid")] public string Mid { get; set; }

    [XmlElement(ElementName = "request_id")]
    public string Request_id { get; set; }

    [XmlElement(ElementName = "ip_address")]
    public string Ip_address { get; set; }

    [XmlElement(ElementName = "notification_url")]
    public string Notification_url { get; set; }

    [XmlElement(ElementName = "response_url")]
    public string Response_url { get; set; }

    [XmlElement(ElementName = "cancel_url")]
    public string Cancel_url { get; set; }

    [XmlElement(ElementName = "mtac_url")] public string Mtac_url { get; set; }

    [XmlElement(ElementName = "descriptor_note")]
    public string Descriptor_note { get; set; }

    [XmlElement(ElementName = "fname")] public string Fname { get; set; }
    [XmlElement(ElementName = "lname")] public string Lname { get; set; }
    [XmlElement(ElementName = "mname")] public string Mname { get; set; }
    [XmlElement(ElementName = "address1")] public string Address1 { get; set; }
    [XmlElement(ElementName = "address2")] public string Address2 { get; set; }
    [XmlElement(ElementName = "city")] public string City { get; set; }
    [XmlElement(ElementName = "state")] public string State { get; set; }
    [XmlElement(ElementName = "country")] public string Country { get; set; }
    [XmlElement(ElementName = "zip")] public string Zip { get; set; }
    [XmlElement(ElementName = "secure3d")] public string Secure3d { get; set; }
    [XmlElement(ElementName = "trxtype")] public string Trxtype { get; set; }
    [XmlElement(ElementName = "email")] public string Email { get; set; }
    [XmlElement(ElementName = "phone")] public string Phone { get; set; }
    [XmlElement(ElementName = "mobile")] public string Mobile { get; set; }

    [XmlElement(ElementName = "client_ip")]
    public string Client_ip { get; set; }

    [XmlElement(ElementName = "amount")] public string Amount { get; set; }
    [XmlElement(ElementName = "currency")] public string Currency { get; set; }

    [XmlElement(ElementName = "mlogo_url")]
    public string Mlogo_url { get; set; }

    [XmlElement(ElementName = "pmethod")] public string Pmethod { get; set; }

    [XmlElement(ElementName = "signature")]
    public string Signature { get; set; }
    
    static Regex emptyElementRegex = new Regex(@"<(\w+)\s*/>");
    public string ToXml()
    {
        using var ms = new MemoryStream();
        using var stringWriter = new XmlTextWriterX(ms);
        
        var serializer = new XmlSerializer(GetType());
        var serializerNamespaces = new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty });
        
        serializer.Serialize(stringWriter, this, serializerNamespaces);
        var r = Encoding.Default.GetString(ms.ToArray());
        
        stringWriter.Dispose();
        serializer = null;
            
        return r;
    }
}

[XmlRoot(ElementName = "Items")]
public class Items
{
    [XmlElement(ElementName = "itemname")] public string Itemname { get; set; }
    [XmlElement(ElementName = "quantity")] public string Quantity { get; set; }
    [XmlElement(ElementName = "amount")] public string Amount { get; set; }
}

[XmlRoot(ElementName = "items")]
public class SubItems
{
    [XmlElement(ElementName = "Items")] public List<Items> Items { get; set; }
}

[XmlRoot(ElementName = "orders")]
public class Orders
{
    [XmlElement(ElementName = "items")] public SubItems SubItems { get; set; }
}