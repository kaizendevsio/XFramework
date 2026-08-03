using System.Text.RegularExpressions;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.IdentityServer)]
[Category(TestCategories.Wrappers)]
public sealed class WrapperCoverageCompletenessTests
{
    [Test]
    public void IdentityServerWrapperRequestContracts_AllHaveDirectIntegrationCoverage()
    {
        var repositoryRoot = FindRepositoryRoot();
        string[] requestRoots =
        [
            Path.Combine(
                repositoryRoot.FullName,
                "src",
                "Modules",
                "XFramework.IdentityServer",
                "IdentityServer.Domain.Shared",
                "Contracts",
                "Requests"),
            Path.Combine(
                repositoryRoot.FullName,
                "src",
                "Modules",
                "XFramework.Bolt",
                "Bolt.Domain.Shared",
                "Contracts",
                "ServiceIdentity")
        ];
        var testsRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Tests",
            "IdentityServer.IntegrationTests",
            "Tests");

        var requestContracts = requestRoots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(path => File.ReadAllText(path).Contains("IBoltRequest", StringComparison.Ordinal))
            .SelectMany(path => Regex.Matches(
                    File.ReadAllText(path),
                    @"\b(?:class|record|struct)\s+(?<name>[A-Za-z0-9_]+Request)\b(?=[^{;]*\bIBoltRequest\s*<)")
                .Select(match => match.Groups["name"].Value))
            .Select(name => name[..^"Request".Length])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name)
            .ToArray();

        var testSource = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(testsRoot, "*.cs", SearchOption.TopDirectoryOnly)
                .Where(path => !string.Equals(
                    Path.GetFileName(path),
                    nameof(WrapperCoverageCompletenessTests) + ".cs",
                    StringComparison.Ordinal))
                .Select(File.ReadAllText));

        var missing = requestContracts
            .Where(methodName => !Regex.IsMatch(
                testSource,
                $@"\bIntegrationTestFixture\s*\.\s*ServiceWrapper\s*\.\s*{Regex.Escape(methodName)}\s*\("))
            .ToArray();

        missing.Should().BeEmpty(
            "every IdentityServer IBoltRequest contract must be invoked directly through IntegrationTestFixture.ServiceWrapper");
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "XFramework.slnx")))
                return current;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate XFramework repository root.");
    }
}
