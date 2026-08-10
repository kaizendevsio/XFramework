using System.Reflection;
using Attendance.Api.Features.Contexts.GetList;
using Attendance.Domain.Shared.Contracts;
using Attendance.Integration.Drivers;
using FluentAssertions;
using NUnit.Framework;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Attendance.Tests.Services;

[TestFixture]
public sealed class AttendanceBoltContractTests
{
    [Test]
    public void CustomHandlers_RequireActorTenantAndPortalReadOrWriteScope()
    {
        var handlers = typeof(GetAttendanceContextsEndpoint).Assembly
            .GetTypes()
            .Where(type => type.Namespace?.StartsWith("Attendance.Api.Features", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(method => new
            {
                Method = method,
                Attribute = method.GetCustomAttribute<BoltHandlerAttribute>()
            })
            .Where(item => item.Attribute is not null)
            .ToList();

        handlers.Should().HaveCount(18);
        foreach (var handler in handlers)
        {
            var requestType = handler.Method.GetParameters()[0].ParameterType;
            var expectedScope = requestType.Name.StartsWith("Get", StringComparison.Ordinal)
                ? XFrameworkServiceScopes.AttendanceRead
                : XFrameworkServiceScopes.AttendanceWrite;

            handler.Attribute!.RequiredServiceScopes.Should().Equal(expectedScope);
            handler.Attribute.AllowedServiceCallers.Should().Equal(XFrameworkServiceNames.Portal);
            handler.Attribute.ActorRequirement.Should().Be(ActorRequirement.Required);
            handler.Attribute.TenantAccessMode.Should().Be(TenantAccessMode.ActorTenant);
            handler.Attribute.AllowAnonymous.Should().BeFalse();

            if (handler.Method.DeclaringType?.Namespace?.StartsWith(
                    "Attendance.Api.Features.Reads",
                    StringComparison.Ordinal) == true)
            {
                handler.Attribute.RequiredActorCapabilities.Should()
                    .Equal(AttendanceAuthorizationCapabilities.View);
            }
        }
    }

    [Test]
    public async Task BusinessWrapperMethods_RequestExactScopeAndPropagateCancellation()
    {
        var methods = typeof(IAttendanceServiceWrapper)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(method => method.Name)
            .ToList();

        methods.Should().HaveCount(18);
        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            parameters.Should().HaveCount(2);
            parameters[1].ParameterType.Should().Be<CancellationToken>();
            parameters[1].HasDefaultValue.Should().BeTrue();

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var serviceTokenProvider = new RecordingServiceTokenProvider();
            var actorTokenProvider = new RecordingActorAccessTokenProvider();
            var wrapper = new AttendanceServiceWrapper(
                null!,
                null!,
                null!,
                serviceTokenProvider,
                actorTokenProvider);
            var request = Activator.CreateInstance(parameters[0].ParameterType)!;

            var task = (Task)method.Invoke(wrapper, [request, cancellation.Token])!;
            Func<Task> act = async () => await task;

            await act.Should().ThrowAsync<OperationCanceledException>();
            var expectedScope = method.Name.StartsWith("Get", StringComparison.Ordinal)
                ? XFrameworkServiceScopes.AttendanceRead
                : XFrameworkServiceScopes.AttendanceWrite;
            serviceTokenProvider.Audience.Should().Be(XFrameworkServiceNames.Attendance);
            serviceTokenProvider.Scopes.Should().Equal(expectedScope);
            serviceTokenProvider.CancellationToken.Should().Be(cancellation.Token);
            actorTokenProvider.CancellationToken.Should().Be(cancellation.Token);
        }
    }

    private sealed class RecordingActorAccessTokenProvider : IActorAccessTokenProvider
    {
        public CancellationToken CancellationToken { get; private set; }

        public ValueTask<string?> GetTokenAsync(CancellationToken ct = default)
        {
            CancellationToken = ct;
            return ValueTask.FromResult<string?>("actor-token");
        }
    }

    private sealed class RecordingServiceTokenProvider : IServiceTokenProvider
    {
        public string? Audience { get; private set; }
        public IReadOnlyCollection<string>? Scopes { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public ValueTask<string> GetTokenAsync(
            string audience,
            IReadOnlyCollection<string>? scopes = null,
            CancellationToken ct = default)
        {
            Audience = audience;
            Scopes = scopes;
            CancellationToken = ct;
            return ValueTask.FromException<string>(new OperationCanceledException(ct));
        }
    }
}
