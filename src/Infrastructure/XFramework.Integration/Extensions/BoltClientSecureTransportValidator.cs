using XFramework.Domain.Shared.Configurations;

namespace XFramework.Integration.Extensions;

internal static class BoltClientSecureTransportValidator
{
    public static void Validate(BoltConfiguration configuration, bool requireSecureTransport)
    {
        if (!requireSecureTransport || configuration.ServerUrls is null)
            return;

        for (var index = 0; index < configuration.ServerUrls.Count; index++)
        {
            var serverUrl = configuration.ServerUrls[index];
            if (serverUrl.IsAbsoluteUri
                && (serverUrl.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase)
                    || serverUrl.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var scheme = serverUrl.IsAbsoluteUri ? serverUrl.Scheme : "relative";
            throw new InvalidOperationException(
                $"BoltConfiguration:ServerUrls:{index} must use wss:// or https:// when " +
                $"secure transport is required; scheme '{scheme}' is not secure.");
        }
    }
}
