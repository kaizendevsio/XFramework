using System.Globalization;
using System.Web;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XFramework.Integration.Extensions;

public static class HttpFormExtension
{
    private static IDictionary<string, string> ToKeyValue(this object metaToken)
    {
        if (metaToken == null)
        {
            return null;
        }

        // Added by me: avoid cyclic references
        var serializer = new JsonSerializer { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
        var token = metaToken as JToken;
        if (token == null)
        {
            // Modified by me: use serializer defined above
            return ToKeyValue(JObject.FromObject(metaToken, serializer));
        }

        if (token.HasValues)
        {
            var contentData = new Dictionary<string, string>();
            foreach (var child in token.Children().ToList())
            {
                var childContent = child.ToKeyValue();
                if (childContent != null)
                {
                    contentData = contentData.Concat(childContent)
                        .ToDictionary(k => k.Key, v => v.Value);
                }
            }

            return contentData;
        }

        var jValue = token as JValue;
        if (jValue?.Value == null)
        {
            return null;
        }

        var value = jValue?.Type == JTokenType.Date
            ? jValue?.ToString("o", CultureInfo.InvariantCulture)
            : jValue?.ToString(CultureInfo.InvariantCulture);

        return new Dictionary<string, string> { { token.Path, value } };
    }

    public static FormUrlEncodedContent ToFormData(this object obj)
    {
        var formData = obj.ToKeyValue();

        return new(formData);
    }

    public static string JsonToQuery(this string jsonQuery)
    {
        string str = "?";
        str += jsonQuery
            .Replace(":", "=")
            .Replace("{", "")
            .Replace("}", "")
            .Replace(",", "&")
            .Replace("\"", "");
        return str;
    }

    public static T ToXmlObject<T>(this string stringObject)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StringReader(stringObject);
        var result = (T)(serializer.Deserialize(reader));

        return result;
    }

    public static string UrlEncode(this string text)
    {
        return HttpUtility.UrlEncode(text);
    }

    public static string UrlDecode(this string text)
    {
        return HttpUtility.UrlDecode(text);
    }
}