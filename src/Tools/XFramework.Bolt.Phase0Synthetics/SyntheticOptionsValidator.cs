namespace XFramework.Bolt.Phase0Synthetics;

public static class SyntheticOptionsValidator
{
    public const int MaximumDeviceInputLength = 43;

    public static void Validate(SyntheticOptions options)
    {
        ValidateTarget(options.Target);

        if (options.TenantId == Guid.Empty)
            throw new SyntheticConfigurationException("invalid_tenant_id");

        if (options.CredentialId == Guid.Empty)
            throw new SyntheticConfigurationException("invalid_credential_id");

        if (!CommunicationsAddressing.IsValidDeviceInput(options.DeviceId))
            throw new SyntheticConfigurationException("invalid_device_id");

        if (options.OperationTimeout < TimeSpan.FromSeconds(1) ||
            options.OperationTimeout > TimeSpan.FromMinutes(5))
        {
            throw new SyntheticConfigurationException("invalid_operation_timeout");
        }

        if (options.ExpiryGrace < TimeSpan.Zero || options.ExpiryGrace > TimeSpan.FromMinutes(2))
            throw new SyntheticConfigurationException("invalid_expiry_grace");

        if (options.ExpiryMaxWait < TimeSpan.FromSeconds(1) ||
            options.ExpiryMaxWait > TimeSpan.FromMinutes(10))
        {
            throw new SyntheticConfigurationException("invalid_expiry_max_wait");
        }

        if (options.RejectedCommunicationsTransportToken?.HasSameValue(options.CommunicationsTransportToken) == true ||
            options.RejectedPortalTransportToken?.HasSameValue(options.PortalTransportToken) == true)
        {
            throw new SyntheticConfigurationException("old_generation_token_matches_current");
        }
    }

    public static void ValidateTarget(Uri target)
    {
        if (!target.IsAbsoluteUri ||
            !string.Equals(target.Scheme, "wss", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(target.Host) ||
            !string.IsNullOrEmpty(target.UserInfo) ||
            !string.IsNullOrEmpty(target.Query) ||
            !string.IsNullOrEmpty(target.Fragment))
        {
            throw new SyntheticConfigurationException("invalid_wss_target");
        }
    }
}
