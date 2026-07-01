using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace XFramework.Core.Tests.Security;

[TestFixture]
public sealed class LegacyTrustPathGuardTests
{
    [Test]
    public void Source_DoesNotUseLegacyMetadataTrustHelper()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenToken = "RequestMetadata" + "Trust";

        var matches = Directory
            .EnumerateFiles(repositoryRoot.FullName, "*.cs", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .Where(path => File.ReadAllText(path).Contains(forbiddenToken, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot.FullName, path))
            .ToList();

        matches.Should().BeEmpty(
            "service-to-service trust must use IdentityServer-issued service tokens and the shared invocation resolver");
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XFramework.slnx")))
                return directory;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be found from the test directory.");
    }

    private static bool IsSourceFile(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return !segments.Contains("bin", StringComparer.OrdinalIgnoreCase) &&
               !segments.Contains("obj", StringComparer.OrdinalIgnoreCase) &&
               !segments.Contains(".git", StringComparer.OrdinalIgnoreCase);
    }
}
