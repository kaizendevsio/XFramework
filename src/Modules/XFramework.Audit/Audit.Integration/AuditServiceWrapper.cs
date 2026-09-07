using Audit.Domain.Shared;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using XFramework.Integration.Security;
namespace Audit.Integration;
public interface IAuditServiceWrapper : IServiceWrapper
{
    Task<QueryResponse<AuditPage>> SearchAuditEvents(SearchAuditEventsRequest request, CancellationToken ct = default);
    Task<QueryResponse<AuditDetail>> GetAuditEvent(GetAuditEventRequest request, CancellationToken ct = default);
}
public sealed record AuditServiceWrapper(IMessageBusWrapper bus, IConfiguration configuration)
    : DriverBase(bus, configuration), IAuditServiceWrapper
{
    public override void Initialize() => TargetClient = "XFramework.Audit".ToSha256();
    public Task<QueryResponse<AuditPage>> SearchAuditEvents(SearchAuditEventsRequest request, CancellationToken ct = default)
        => SendAsync<SearchAuditEventsRequest, AuditPage>(request, ct);
    public Task<QueryResponse<AuditDetail>> GetAuditEvent(GetAuditEventRequest request, CancellationToken ct = default)
        => SendAsync<GetAuditEventRequest, AuditDetail>(request, ct);
}
