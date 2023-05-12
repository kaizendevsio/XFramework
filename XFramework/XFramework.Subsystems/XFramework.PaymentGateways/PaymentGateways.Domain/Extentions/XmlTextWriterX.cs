using System.IO;
using System.Text;
using System.Xml;

namespace PaymentGateways.Domain.Extentions;

public class XmlTextWriterX : XmlTextWriter
{
    public XmlTextWriterX(Stream stream) : base(stream, Encoding.Default)
    {

    }

    public override void WriteEndElement()
    {
        base.WriteFullEndElement();
    }
}