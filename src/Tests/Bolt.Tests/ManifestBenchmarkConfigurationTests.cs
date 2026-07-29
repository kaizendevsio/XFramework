using System.Text.Json;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public class ManifestBenchmarkConfigurationTests
{
    [TestCase("src/Modules/XFramework.Storage/Storage.Api/appsettings.json")]
    [TestCase("src/Presentation/XFramework.Operations.Dashboard/appsettings.json")]
    public void ServiceManifestName_MatchesAuthenticatedBoltClientName(string relativePath)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(repositoryRoot, relativePath)));
        var root = document.RootElement;

        var clientName = root.GetProperty("BoltConfiguration").GetProperty("ClientName").GetString();
        var serviceName = root.GetProperty("XFrameworkServiceManifest").GetProperty("ServiceName").GetString();

        Assert.That(serviceName, Is.EqualTo(clientName));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XFramework.slnx")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the XFramework repository root.");
    }
}
