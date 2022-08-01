using System.Diagnostics;
using HealthEssentials.Core.DataAccess.Query.Entity.Medicine;
using HealthEssentials.Domain.Contracts.Responses;
using RestSharp;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Medicine;

public class GetMedicineAutoCompleteHandler : QueryBaseHandler, IRequestHandler<GetMedicineAutoCompleteQuery, QueryResponse<List<string>>>
{
    public GetMedicineAutoCompleteHandler(IDataLayer dataLayer, IDataLayer dataLayer2, IDataLayer dataLayer3, IDataLayer dataLayer4, IDataLayer dataLayer5)
    {
        _dataLayer2 = dataLayer2;
        _dataLayer3 = dataLayer3;
        _dataLayer4 = dataLayer4;
        _dataLayer5 = dataLayer5;
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<string>>> Handle(GetMedicineAutoCompleteQuery request, CancellationToken cancellationToken)
    {
        var patientConsultation = await _dataLayer.HealthEssentialsContext.Medicines
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Select(i => i.Name)    
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!patientConsultation.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No patient consultation found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Pharmacy found",
            IsSuccess = true,
            Response = patientConsultation
        };        
    }
}