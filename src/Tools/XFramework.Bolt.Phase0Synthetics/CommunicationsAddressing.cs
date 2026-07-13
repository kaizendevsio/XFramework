namespace XFramework.Bolt.Phase0Synthetics;

public static class CommunicationsAddressing
{
    private const int MaximumDeviceSegmentLength = 64;
    private const int SyntheticSuffixLength = 21;

    public static string PresenceTopic(Guid tenantId) =>
        $"communications.tenant.{tenantId:N}.presence";

    public static string UserTopic(Guid tenantId, Guid credentialId) =>
        $"communications.tenant.{tenantId:N}.user.{credentialId:N}";

    public static string DurableSubscriberId(
        Guid tenantId,
        Guid credentialId,
        string deviceId,
        Guid runId)
    {
        if (!IsValidDeviceInput(deviceId))
            throw new SyntheticConfigurationException("invalid_device_id");

        var runSuffix = runId.ToString("N")[..16];
        var uniqueDeviceId = $"{deviceId}-syn-{runSuffix}";
        return $"communications:{tenantId:N}:{credentialId:N}:device:{uniqueDeviceId}:user";
    }

    public static bool IsValidDeviceInput(string? deviceId) =>
        !string.IsNullOrWhiteSpace(deviceId) &&
        deviceId.Length <= SyntheticOptionsValidator.MaximumDeviceInputLength &&
        deviceId.All(static ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_') &&
        deviceId.Length + SyntheticSuffixLength <= MaximumDeviceSegmentLength;
}
