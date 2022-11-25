using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyJobOrderListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyJobOrderListQuery, QueryResponse<List<PharmacyJobOrderResponse>>>
{
    public GetPharmacyJobOrderListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyJobOrderResponse>>> Handle(GetPharmacyJobOrderListQuery request, CancellationToken cancellationToken)
    {
        var jobOrder = await _dataLayer.HealthEssentialsContext.PharmacyJobOrders
            .Include(i => i.PharmacyLocation)
            .Include(i => i.Schedule)
            .Include(x => x.Patient)
            .Include(x => x.PharmacyJobOrderMedicines)
            .Where(i => EF.Functions.ILike(i.ReferenceNumber, $"%{request.SearchField}%"))
            .Where(i => i.PharmacyLocation.Guid == $"{request.PharmacyLocationGuid}")
            .Where(i => i.Status == (int) request.Status)
            .OrderBy(i => i.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!jobOrder.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No records found",
                IsSuccess = true
            };
        }
        
        var response = jobOrder.Adapt<List<PharmacyJobOrderResponse>>();
        await GetCredential(response);
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Records found",
            IsSuccess = true,
            Response = response
        };
    }
    
    private async Task GetCredential(List<PharmacyJobOrderResponse> response)
    {
        for (var index = 0; index < response.Count; index++)
        {
            response[index].Patient.Credential = _dataLayer.XnelSystemsContext.IdentityCredentials
                .AsNoTracking()
                .Include(i => i.IdentityInfo)
                .Include(i => i.IdentityContacts)
                .ThenInclude(i => i.Entity)
                .AsSplitQuery()
                .Where(i => i.Guid == response[index].Patient.CredentialGuid)
                .FirstOrDefault()?
                .Adapt<CredentialResponse>();
        }
    }
}