using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Yap.Services;

namespace Yap.Tests;

[TestFixture]
public sealed class AttachmentDownloadTests
{
    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Yap:AttachmentPublicEndpoint"] = "http://xeon-dev:9000",
        ["Yap:AttachmentInternalEndpoint"] = "http://minio:9000"
    }).Build();

    [Test]
    public void InternalRoute_PreservesSignedAuthorityAndEncodedObject()
    {
        const string url = "http://xeon-dev:9000/private/a%20b%2Fc.png?X-Amz-Signature=test&X-Amz-Credential=a%2Fb";
        using var request = YapApi.CreateAttachmentDownloadRequest(url, Configuration());
        Assert.Multiple(() =>
        {
            Assert.That(request.RequestUri!.Authority, Is.EqualTo("minio:9000"));
            Assert.That(request.Headers.Host, Is.EqualTo("xeon-dev:9000"));
            Assert.That(request.RequestUri.PathAndQuery, Is.EqualTo(new Uri(url).PathAndQuery));
        });
    }

    [TestCase("https://other-storage.example/private/object?signature=test")]
    [TestCase("http://xeon-dev:9001/private/object?signature=test")]
    [TestCase("https://xeon-dev:9000/private/object?signature=test")]
    public void OtherProviderOrigin_IsNeverRewritten(string url)
    {
        using var request = YapApi.CreateAttachmentDownloadRequest(url, Configuration());
        Assert.That(request.RequestUri, Is.EqualTo(new Uri(url)));
        Assert.That(request.Headers.Host, Is.Null);
    }

    [Test]
    public void NoInternalRoute_UsesAuthorizedUrlUnchanged()
    {
        const string url = "https://storage.example/private/object?signature=test";
        using var request = YapApi.CreateAttachmentDownloadRequest(url, new ConfigurationBuilder().Build());
        Assert.That(request.RequestUri, Is.EqualTo(new Uri(url)));
    }

    [TestCase("file:///private/object")]
    [TestCase("http://user:password@xeon-dev:9000/private/object")]
    public void InvalidDownloadAuthority_IsRejected(string url) =>
        Assert.Throws<YapApiException>(() => YapApi.CreateAttachmentDownloadRequest(url, Configuration()));
}
