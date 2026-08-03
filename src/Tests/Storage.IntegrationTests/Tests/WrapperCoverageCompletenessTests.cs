using System.Text.RegularExpressions;
using FluentAssertions;
using NUnit.Framework;
using XFramework.TestInfrastructure;

namespace Storage.IntegrationTests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Storage)]
[Category(TestCategories.Wrappers)]
public sealed class WrapperCoverageCompletenessTests
{
    [Test]
    public void StorageWrapperRequestContracts_AllHaveDirectIntegrationCoverage()
    {
        var repositoryRoot = FindRepositoryRoot();
        var requestRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Modules",
            "XFramework.Storage",
            "Storage.Domain.Shared",
            "Contracts",
            "Requests");
        var testsRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Tests",
            "Storage.IntegrationTests",
            "Tests");

        var methods = Directory.EnumerateFiles(requestRoot, "*.cs", SearchOption.AllDirectories)
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
            Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.TopDirectoryOnly)
                .Where(path => !string.Equals(
                    Path.GetFileName(path),
                    nameof(WrapperCoverageCompletenessTests) + ".cs",
                    StringComparison.Ordinal))
                .Select(File.ReadAllText));

        var missing = methods
            .Where(method => !Regex.IsMatch(
                testSource,
                $@"\bServiceWrapper\s*\.\s*{Regex.Escape(method)}\s*\("))
            .ToArray();

        missing.Should().BeEmpty(
            "every Storage IBoltRequest contract must be invoked directly through IStorageServiceWrapper");
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
