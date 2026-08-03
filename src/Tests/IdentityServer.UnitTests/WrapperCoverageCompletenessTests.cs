using System.Text.RegularExpressions;
using FluentAssertions;
using NUnit.Framework;

namespace IdentityServer.UnitTests;

[TestFixture]
[Category("Kind:Integration")]
[Category("Kind:ExtendedIntegration")]
[Category("Module:IdentityServer")]
[Category("Area:Wrappers")]
public sealed class WrapperCoverageCompletenessTests
{
    [Test]
    public void IdentityServerWrapperRequestContracts_AllHaveDirectIntegrationCoverage()
    {
        var repositoryRoot = FindRepositoryRoot();
        var requestsRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Modules",
            "XFramework.IdentityServer",
            "IdentityServer.Domain.Shared",
            "Contracts",
            "Requests");
        var testsRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Tests",
            "IdentityServer.IntegrationTests",
            "Tests");

        var requestContracts = Directory
            .EnumerateFiles(requestsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("IBoltRequest", StringComparison.Ordinal))
            .SelectMany(path => Regex.Matches(
                    File.ReadAllText(path),
                    @"\b(?:class|record|struct)\s+(?<name>[A-Za-z0-9_]+Request)\b")
                .Select(match => match.Groups["name"].Value))
            .Select(name => name[..^"Request".Length])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name)
            .ToArray();

        var testSource = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(testsRoot, "*.cs", SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));

        var missing = requestContracts
            .Where(methodName => !Regex.IsMatch(
                testSource,
                $@"\bIntegrationTestFixture\s*\.\s*ServiceWrapper\s*\.\s*{Regex.Escape(methodName)}\s*\(",
                RegexOptions.CultureInvariant))
            .ToArray();

        missing.Should().BeEmpty(
            "every IdentityServer IBoltRequest contract must have at least one direct service-wrapper integration test");
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
