using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace XFramework.Bolt.Phase0Synthetics;

public static class SecretTokenFileReader
{
    public const int MaximumTokenFileBytes = 16 * 1024;

    public static SecretToken Read(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
                throw new SyntheticConfigurationException("invalid_token_file");

            var file = new FileInfo(path);
            file.Refresh();
            if (!file.Exists ||
                file.LinkTarget is not null ||
                file.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
                file.Length is <= 0 or > MaximumTokenFileBytes)
            {
                throw new SyntheticConfigurationException("invalid_token_file");
            }

            if (OperatingSystem.IsWindows())
                ValidateWindowsPermissions(file);
            else
                ValidateUnixPermissions(path);

            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.SequentialScan);
            if (stream.Length is <= 0 or > MaximumTokenFileBytes)
                throw new SyntheticConfigurationException("invalid_token_file");

            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: false);
            var value = reader.ReadToEnd().Trim();
            if (value.Length == 0 || value.IndexOf('\0') >= 0 || value.Any(char.IsWhiteSpace))
                throw new SyntheticConfigurationException("invalid_token_file");

            return new SecretToken(value);
        }
        catch (SyntheticConfigurationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   System.Security.SecurityException or DecoderFallbackException)
        {
            throw new SyntheticConfigurationException("invalid_token_file");
        }
    }

    [UnsupportedOSPlatform("windows")]
    private static void ValidateUnixPermissions(string path)
    {
        var mode = File.GetUnixFileMode(path);
        const UnixFileMode forbidden =
            UnixFileMode.UserExecute |
            UnixFileMode.GroupRead |
            UnixFileMode.GroupWrite |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherWrite |
            UnixFileMode.OtherExecute;
        if (!mode.HasFlag(UnixFileMode.UserRead) || (mode & forbidden) != 0)
            throw new SyntheticConfigurationException("invalid_token_file_permissions");
    }

    [SupportedOSPlatform("windows")]
    private static void ValidateWindowsPermissions(FileInfo file)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var owner = identity.User ?? throw new SyntheticConfigurationException("invalid_token_file_permissions");
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var security = file.GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access);
        if (security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier fileOwner)
            throw new SyntheticConfigurationException("invalid_token_file_permissions");

        if (fileOwner != owner && fileOwner != system && fileOwner != administrators)
            throw new SyntheticConfigurationException("invalid_token_file_permissions");

        const FileSystemRights readable =
            FileSystemRights.Read |
            FileSystemRights.ReadAndExecute |
            FileSystemRights.FullControl;
        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier));
        foreach (FileSystemAccessRule rule in rules)
        {
            if (rule.AccessControlType != AccessControlType.Allow || (rule.FileSystemRights & readable) == 0)
                continue;

            if (rule.IdentityReference is not SecurityIdentifier sid)
                throw new SyntheticConfigurationException("invalid_token_file_permissions");

            if (sid != owner && sid != system && sid != administrators)
                throw new SyntheticConfigurationException("invalid_token_file_permissions");
        }
    }
}
