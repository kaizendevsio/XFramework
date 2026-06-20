using System.Text.RegularExpressions;
using XFramework.TestInfrastructure;

namespace Wallets.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.Wallets)]
[Category(TestCategories.Wrappers)]
public sealed class WrapperCoverageCompletenessTests
{
    [Test]
    public void WalletsWrapperRequestContracts_AllHaveDirectIntegrationCoverage()
    {
        var repositoryRoot = FindRepositoryRoot();
        var requestsRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Modules",
            "XFramework.Wallets",
            "Wallets.Domain.Shared",
            "Contracts",
            "Requests");
        var testsRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Tests",
            "Wallets.IntegrationTests",
            "Tests");

        var requestContracts = Directory
            .EnumerateFiles(requestsRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => RequestContractPattern
                .Matches(File.ReadAllText(path))
                .Select(match => match.Groups["name"].Value))
            .Where(static name => name.EndsWith("Request", StringComparison.Ordinal))
            .Select(static name => name[..^"Request".Length])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name)
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
            .Where(methodName => !testSource.Contains($".{methodName}(", StringComparison.Ordinal))
            .ToArray();

        missing.Should().BeEmpty(
            "every Wallets IBoltRequest contract must have at least one direct service-wrapper integration test");
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "XFramework.slnx")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate XFramework repository root.");
    }

    private static readonly Regex RequestContractPattern = new(
        @"(?:record|class)\s+(?<name>[A-Za-z0-9_]+Request)\b\s*:\s*[^;{]*IBoltRequest",
        RegexOptions.Compiled | RegexOptions.Singleline);
}
