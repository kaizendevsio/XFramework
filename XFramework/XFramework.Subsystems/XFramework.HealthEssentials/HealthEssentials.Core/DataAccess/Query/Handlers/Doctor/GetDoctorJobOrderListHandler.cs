using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetDoctorJobOrderListHandler : QueryBaseHandler, IRequestHandler<GetDoctorJobOrderListQuery, QueryResponse<List<DoctorConsultationJobOrderResponse>>>
{
    public GetDoctorJobOrderListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<DoctorConsultationJobOrderResponse>>> Handle(GetDoctorJobOrderListQuery request, CancellationToken cancellationToken)
    {
        var record = await _dataLayer.HealthEssentialsContext.DoctorConsultationJobOrders
            .Include(i => i.ConsultationJobOrder.Consultation)
            .Include(i => i.Doctor)
            .Where(i => EF.Functions.ILike(i.ConsultationJobOrder.ReferenceNumber, $"%{request.SearchField}%"))
            .Where(i => i.ConsultationJobOrder.Status == (int) request.RecordType)
            .OrderBy(i => i.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!record.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No records found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Records found",
            Response = record.Adapt<List<DoctorConsultationJobOrderResponse>>()
        };
    }
}