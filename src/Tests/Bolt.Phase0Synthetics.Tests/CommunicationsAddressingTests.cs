using FluentAssertions;
using XFramework.Bolt.Phase0Synthetics;

namespace Bolt.Phase0Synthetics.Tests;

public sealed class CommunicationsAddressingTests
{
    private static readonly Guid TenantId = new("11111111-2222-3333-4444-555555555555");
    private static readonly Guid CredentialId = new("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid RunId = new("01234567-89ab-cdef-0123-456789abcdef");

    [Test]
    public void PresenceTopic_ValidTenant_UsesAuthorizerGrammar()
    {
        var topic = CommunicationsAddressing.PresenceTopic(TenantId);

        topic.Should().Be("communications.tenant.11111111222233334444555555555555.presence");
    }

    [Test]
    public void UserTopic_ValidTenantAndCredential_UsesAuthorizerGrammar()
    {
        var topic = CommunicationsAddressing.UserTopic(TenantId, CredentialId);

        topic.Should().Be(
            "communications.tenant.11111111222233334444555555555555.user.aaaaaaaabbbbccccddddeeeeeeeeeeee");
    }

    [Test]
    public void DurableSubscriberId_ValidInputs_IsUniqueAndUsesAuthorizerGrammar()
    {
        var subscriberId = CommunicationsAddressing.DurableSubscriberId(
            TenantId,
            CredentialId,
            "phase0_device",
            RunId);

        subscriberId.Should().Be(
            "communications:11111111222233334444555555555555:aaaaaaaabbbbccccddddeeeeeeeeeeee:" +
            "device:phase0_device-syn-0123456789abcdef:user");
        subscriberId.Split(':').Should().HaveCount(6);
        subscriberId.Split(':')[4].Length.Should().BeLessThanOrEqualTo(64);
    }

    [TestCase("bad device")]
    [TestCase("bad:device")]
    [TestCase("")]
    public void DurableSubscriberId_InvalidDevice_FailsClosed(string deviceId)
    {
        var action = () => CommunicationsAddressing.DurableSubscriberId(
            TenantId,
            CredentialId,
            deviceId,
            RunId);

        action.Should().Throw<SyntheticConfigurationException>()
            .Which.Code.Should().Be("invalid_device_id");
    }
}
