using FluentAssertions;
using NUnit.Framework;
using CoreAttributes = XFramework.Core.Attributes;
using SharedAttributes = XFramework.Domain.Shared.Attributes;

namespace XFramework.Core.Tests.Security;

[TestFixture]
public sealed class GenerateEndpointsCompatibilityTests
{
    [Test]
    public void CoreShim_ForwardsTypeAndActionsToCanonicalAttribute()
    {
#pragma warning disable CS0618
        var legacy = new CoreAttributes.GenerateEndpointsAttribute
        {
            Type = CoreAttributes.EndpointType.Service,
            Actions = CoreAttributes.EndpointActions.Get | CoreAttributes.EndpointActions.GetList
        };
#pragma warning restore CS0618

        var canonical = (SharedAttributes.GenerateEndpointsAttribute)legacy;

        canonical.Type.Should().Be(SharedAttributes.EndpointType.Service);
        canonical.Actions.Should().Be(
            SharedAttributes.EndpointActions.Get | SharedAttributes.EndpointActions.GetList);
    }
}
