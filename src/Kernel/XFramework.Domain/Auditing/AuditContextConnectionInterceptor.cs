using System.Data.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using XFramework.Domain.Shared.Security;

namespace XFramework.Domain.Auditing;

/// <summary>
/// Places trusted invocation metadata on each PostgreSQL session so database triggers can
/// attribute changes without depending on a remote audit service.
/// </summary>
public sealed class AuditContextConnectionInterceptor(
    IEnumerable<IAuditContextAccessor> contextAccessors,
    IHttpContextAccessor httpContextAccessor,
    IHostEnvironment hostEnvironment) : DbConnectionInterceptor
{
    private const int MaxOperationNameLength = 300;
    private const int MaxUserAgentLength = 512;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        ApplyContext(connection);

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default) =>
        ApplyContextAsync(connection, cancellationToken);

    private void ApplyContext(DbConnection connection)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    private async Task ApplyContextAsync(DbConnection connection, CancellationToken ct)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var context = contextAccessors.FirstOrDefault(item => item.HasAuditContext);
        var httpContext = httpContextAccessor.HttpContext;
        var activity = Activity.Current;
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                set_config('xframework.audit.actor_kind', @actor_kind, false),
                set_config('xframework.audit.actor_credential_id', @actor_credential_id, false),
                set_config('xframework.audit.actor_identity_id', @actor_identity_id, false),
                set_config('xframework.audit.actor_tenant_id', @actor_tenant_id, false),
                set_config('xframework.audit.effective_tenant_id', @effective_tenant_id, false),
                set_config('xframework.audit.session_id', @session_id, false),
                set_config('xframework.audit.service_name', @service_name, false),
                set_config('xframework.audit.environment', @environment, false),
                set_config('xframework.audit.instance_id', @instance_id, false),
                set_config('xframework.audit.correlation_id', @correlation_id, false),
                set_config('xframework.audit.operation_name', @operation_name, false),
                set_config('xframework.audit.client_ip', @client_ip, false),
                set_config('xframework.audit.user_agent', @user_agent, false),
                set_config('xframework.audit.trace_id', @trace_id, false),
                set_config('xframework.audit.span_id', @span_id, false),
                set_config('xframework.audit.transaction_ordinal', '', false);
            """;

        AddParameter(command, "actor_kind", ResolveActorKind(context));
        AddParameter(command, "actor_credential_id", Format(context?.ActorCredentialId));
        AddParameter(command, "actor_identity_id", Format(context?.ActorIdentityId));
        AddParameter(command, "actor_tenant_id", Format(context?.ActorTenantId));
        AddParameter(command, "effective_tenant_id", Format(context?.EffectiveTenantId));
        AddParameter(command, "session_id", Format(context?.SessionId));
        AddParameter(command, "service_name", FirstNonEmpty(context?.ServiceClientId, hostEnvironment.ApplicationName));
        AddParameter(command, "environment", hostEnvironment.EnvironmentName);
        AddParameter(command, "instance_id", Environment.MachineName);
        AddParameter(command, "correlation_id", Format(context?.CorrelationId));
        AddParameter(command, "operation_name", Truncate(httpContext?.GetEndpoint()?.DisplayName, MaxOperationNameLength));
        AddParameter(command, "client_ip", httpContext?.Connection.RemoteIpAddress?.ToString());
        AddParameter(command, "user_agent", Truncate(httpContext?.Request.Headers.UserAgent.ToString(), MaxUserAgentLength));
        AddParameter(command, "trace_id", activity?.TraceId.ToHexString());
        AddParameter(command, "span_id", activity?.SpanId.ToHexString());
        return command;
    }

    private static string ResolveActorKind(IAuditContextAccessor? context) =>
        context switch
        {
            { ActorCredentialId: not null } => "User",
            { ServiceClientId: not null } => "Service",
            { HasAuditContext: true } => "System",
            _ => "Unknown"
        };

    private static string Format(Guid? value) => value?.ToString("D") ?? string.Empty;

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string Truncate(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim()[..Math.Min(value.Trim().Length, maxLength)];

    private static void AddParameter(DbCommand command, string name, string? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? string.Empty;
        command.Parameters.Add(parameter);
    }
}
