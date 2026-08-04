using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace Attendance.IntegrationTests.Infrastructure;

internal static class AttendanceTestIdentity
{
    public const string ActorToken = "attendance-test-actor-token";
    public const string ServiceToken = "attendance-test-service-token";
    public const string ServiceClientId = XFrameworkServiceNames.Portal;

    public static TrustedActorIdentity Actor { get; } = new(
        Guid.Parse("00000000-0000-0000-0000-000000000186"),
        Guid.Parse("00000000-0000-0000-0000-000000000185"),
        AttendanceIntegrationTestFixture.TestTenantId,
        Guid.Parse("00000000-0000-0000-0000-000000000187"),
        new HashSet<string>(StringComparer.Ordinal) { "attendance-test" },
        new HashSet<string>(StringComparer.Ordinal),
        "attendance-tests-g1",
        DateTimeOffset.UtcNow.AddHours(1));

    public static TrustedServiceIdentity Service { get; } = new(
        ServiceClientId,
        "Attendance",
        new HashSet<string>(XFrameworkServiceScopes.AdminDefaults, StringComparer.Ordinal),
        "attendance-tests-g1");
}

internal sealed class AttendanceTestActorAccessTokenProvider : IActorAccessTokenProvider
{
    public ValueTask<string?> GetTokenAsync(CancellationToken ct = default) =>
        ValueTask.FromResult<string?>(AttendanceTestIdentity.ActorToken);
}

internal sealed class AttendanceTestServiceTokenProvider : IServiceTokenProvider
{
    public ValueTask<string> GetTokenAsync(
        string audience,
        IReadOnlyCollection<string>? scopes = null,
        CancellationToken ct = default) =>
        ValueTask.FromResult(AttendanceTestIdentity.ServiceToken);
}

internal sealed class AttendanceTestActorIdentityProvider : IActorIdentityProvider
{
    public Task<ActorIdentityValidationResult> ValidateAsync(
        string token,
        CancellationToken ct = default) =>
        Task.FromResult(token == AttendanceTestIdentity.ActorToken
            ? ActorIdentityValidationResult.Success(AttendanceTestIdentity.Actor)
            : ActorIdentityValidationResult.Failure("Invalid test actor token."));
}

internal sealed class AttendanceTestServiceIdentityProvider : IServiceIdentityProvider
{
    public Task<ServiceIdentityValidationResult> ValidateAsync(
        string token,
        string expectedAudience,
        CancellationToken ct = default) =>
        Task.FromResult(token == AttendanceTestIdentity.ServiceToken
            ? ServiceIdentityValidationResult.Success(
                new TrustedServiceIdentity(
                    AttendanceTestIdentity.Service.ClientId,
                    expectedAudience,
                    AttendanceTestIdentity.Service.Scopes,
                    AttendanceTestIdentity.Service.GenerationId))
            : ServiceIdentityValidationResult.Failure("Invalid test service token."));
}

internal sealed class AttendanceTestInvocationContextAccessor(Guid tenantId)
    : ITrustedInvocationContextAccessor
{
    public TrustedInvocationContext? Current { get; } = new(
        AttendanceTestIdentity.Actor,
        AttendanceTestIdentity.Service,
        tenantId,
        tenantId,
        Guid.NewGuid());
}
