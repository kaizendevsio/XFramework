using System.Security.AccessControl;
using System.Security.Principal;
using FluentAssertions;
using XFramework.Bolt.Phase0Synthetics;

namespace Bolt.Phase0Synthetics.Tests;

public sealed class SecretTokenFileReaderTests
{
    [Test]
    public void Read_SecureRegularFile_ReadsTrimmedToken()
    {
        const string tokenValue = "file-backed-secret-token";
        var path = CreateTokenFile($"  {tokenValue}{Environment.NewLine}");
        try
        {
            var token = SecretTokenFileReader.Read(path);

            token.Sha256Prefix.Should().Be(new SecretToken(tokenValue).Sha256Prefix);
            token.ToString().Should().Be("[REDACTED]");
        }
        finally
        {
            DeleteTokenFile(path);
        }
    }

    [Test]
    public void Read_OverPermissiveFile_FailsClosed()
    {
        var path = CreateTokenFile("secret", secure: false);
        try
        {
            var action = () => SecretTokenFileReader.Read(path);

            action.Should().Throw<SyntheticConfigurationException>()
                .Which.Code.Should().Be("invalid_token_file_permissions");
        }
        finally
        {
            DeleteTokenFile(path);
        }
    }

    [Test]
    public void Read_OversizedFile_FailsBeforeReadingContent()
    {
        var path = CreateTokenFile(new string('x', SecretTokenFileReader.MaximumTokenFileBytes + 1));
        try
        {
            var action = () => SecretTokenFileReader.Read(path);

            action.Should().Throw<SyntheticConfigurationException>()
                .Which.Code.Should().Be("invalid_token_file");
        }
        finally
        {
            DeleteTokenFile(path);
        }
    }

    [Test]
    public void Read_TokenWithEmbeddedWhitespace_FailsClosed()
    {
        var path = CreateTokenFile("header.payload signature");
        try
        {
            var action = () => SecretTokenFileReader.Read(path);

            action.Should().Throw<SyntheticConfigurationException>()
                .Which.Code.Should().Be("invalid_token_file");
        }
        finally
        {
            DeleteTokenFile(path);
        }
    }

    [Test]
    public void Read_SymbolicLink_FailsClosed()
    {
        if (OperatingSystem.IsWindows())
            Assert.Ignore("Creating symbolic links is privilege-dependent on Windows.");

        var target = CreateTokenFile("linked-secret-token");
        var link = Path.Combine(Path.GetTempPath(), $"bolt-phase0-token-link-{Guid.NewGuid():N}");
        try
        {
            File.CreateSymbolicLink(link, target);

            var action = () => SecretTokenFileReader.Read(link);

            action.Should().Throw<SyntheticConfigurationException>()
                .Which.Code.Should().Be("invalid_token_file");
        }
        finally
        {
            DeleteTokenFile(link);
            DeleteTokenFile(target);
        }
    }

    private static string CreateTokenFile(string content, bool secure = true)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bolt-phase0-token-{Guid.NewGuid():N}");
        File.WriteAllText(path, content);
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var user = identity.User!;
            var security = new FileSecurity();
            security.SetOwner(user);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                user,
                FileSystemRights.FullControl,
                AccessControlType.Allow));
            if (!secure)
            {
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.Read,
                    AccessControlType.Allow));
            }
            new FileInfo(path).SetAccessControl(security);
        }
        else
        {
            File.SetUnixFileMode(
                path,
                secure
                    ? UnixFileMode.UserRead | UnixFileMode.UserWrite
                    : UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        }

        return path;
    }

    private static void DeleteTokenFile(string path)
    {
        try { File.Delete(path); }
        catch { }
    }
}
