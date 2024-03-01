using System.Net.Http.Json;
using HealthEssentials.Domain.Contracts.Responses;
using XFramework.Integration.Abstractions;
using TModel = HealthEssentials.Domain.Generics.Contracts.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Medicine;

public class MedicineAutoCompleteHandler(
    DbContext dbContext,
    IIdentityServerServiceWrapper identityServerService,
    ITenantService tenantService,
    ILogger<MedicineAutoCompleteHandler> logger,
    IHelperService helperService
)
    : IRequestHandler<MedicineAutoCompleteRequest, QueryResponse<List<Domain.Generics.Contracts.Medicine>>>
{
    public async Task<QueryResponse<List<Domain.Generics.Contracts.Medicine>>> Handle(MedicineAutoCompleteRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);

        var baseUrl = "https://www.mims.com/autocomplete";
        var countryCode = "PH";
        var url = $"{baseUrl}?countryCode={countryCode}&query={request.SearchString}";

        using (var client = new HttpClient())
        {
            try
            {
                var response = await client.GetAsync(url, cancellationToken);
                
                response.EnsureSuccessStatusCode(); // Throw an exception if not successful
                var responseBody = await response.Content.ReadFromJsonAsync<MimsAutoCompleteResponse>(cancellationToken: cancellationToken);
                responseBody.Suggestions = responseBody.Suggestions.Take(responseBody.Suggestions.Count - 1).ToList();
                
                var existingMedicines = await dbContext.Set<Domain.Generics.Contracts.Medicine>()
                    .Where(i => i.TenantId == tenant.Id)
                    .Where(i => i.IsDeleted == false)
                    .Where(i => responseBody.Suggestions.Select(s => s.Value.ToUpperInvariant()).Contains(i.Name))
                    .ToListAsync(cancellationToken);

                var newMedicines = responseBody.Suggestions;
                newMedicines.RemoveAll(s => existingMedicines.Select(m => m.Name.ToUpperInvariant()).Contains(s.Value.ToUpperInvariant()));

                var newMedicineEntities = new List<Domain.Generics.Contracts.Medicine>();
                
                if (newMedicines.Any())
                {
                    newMedicineEntities = newMedicines.Select(m => new Domain.Generics.Contracts.Medicine
                    {
                        Name = m.Value.ToUpperInvariant(),
                        TenantId = tenant.Id,
                        TypeId = new Guid("570ccf7f-f68a-450d-b110-09648f3540ed")
                    }).ToList();
                    await dbContext.Set<Domain.Generics.Contracts.Medicine>().AddRangeAsync(newMedicineEntities, cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                
                return new QueryResponse<List<Domain.Generics.Contracts.Medicine>>
                {
                    HttpStatusCode = HttpStatusCode.OK,
                    Response = existingMedicines.Concat(newMedicineEntities).ToList()
                };
            }
            catch (HttpRequestException e)
            {
               logger.LogError(e, "Error occurred while fetching data from MIMS");
            }
        }
        
        return new QueryResponse<List<Domain.Generics.Contracts.Medicine>>
        {
            HttpStatusCode = HttpStatusCode.InternalServerError,
            Message = "Error occurred while fetching data from MIMS"
        };
    }
}