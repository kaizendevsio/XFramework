using Attendance.Domain.Shared.Contracts;
using FluentAssertions;
using NUnit.Framework;
using XFramework.Core.DataContext;
using XFramework.Domain.Shared.Attributes;

namespace GeneratedAuthorizationContractTests.Attendance;

[TestFixture]
[Category("Kind:Integration")]
[Category("Module:Attendance")]
[Category("Area:GeneratedAuthorization")]
public sealed class AttendanceGeneratedAuthorizationCompletenessTests
{
    [Test]
    public void GeneratedAttendanceEntities_KeepGenericDataContextAccessFailClosed()
    {
        var registryType = typeof(global::Attendance.Api.Services.IAttendanceReadService)
            .Assembly
            .GetType("XFramework.Core.DataContext.DataContextEntityRegistrations", throwOnError: true)!;
        var entities = (Dictionary<string, Type>)registryType
            .GetMethod("GetDataContextEntityTypes")!
            .Invoke(null, null)!;
        var policies = (IReadOnlyCollection<GeneratedEntityAuthorizationPolicy>)registryType
            .GetMethod("GetDataContextAuthorizationPolicies")!
            .Invoke(null, null)!;
        var mutableEntities = (HashSet<string>)registryType
            .GetMethod("GetDataContextMutableEntityTypes")!
            .Invoke(null, null)!;
        var expectedEntities = new[]
        {
            nameof(AttendanceAdjustment),
            nameof(AttendanceContext),
            nameof(AttendanceEvent),
            nameof(AttendanceParticipant),
            nameof(AttendancePolicy),
            nameof(AttendanceRecord),
            nameof(AttendanceSession)
        };

        entities.Keys.Should().BeEquivalentTo(expectedEntities);
        policies.Should().BeEmpty("approved Attendance reads use explicit actor-authorized wrappers");
        mutableEntities.Should().BeEmpty("Attendance business mutations must never use remote IDataContext");

        foreach (var entityType in entities.Values)
        {
            var attribute = entityType.GetCustomAttributes(typeof(GenerateEndpointsAttribute), false)
                .Cast<GenerateEndpointsAttribute>()
                .Single();
            attribute.Type.Should().Be(EndpointType.Rest);
            attribute.Actions.Should().Be(EndpointActions.None);
            attribute.AuthorizationFeature.Should().Be("attendance");
            attribute.ActorRequirement.Should().Be(GeneratedActorRequirement.Required);
            Attribute.IsDefined(entityType, typeof(AllowRemoteDataContextMutationAttribute))
                .Should().BeFalse();
            Attribute.IsDefined(entityType, typeof(AllowGeneratedServiceAccessAttribute))
                .Should().BeFalse();
        }
    }
}
