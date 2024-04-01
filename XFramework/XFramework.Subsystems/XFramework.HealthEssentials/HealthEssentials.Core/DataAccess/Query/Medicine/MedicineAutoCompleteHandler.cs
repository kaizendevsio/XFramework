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
                var mimsRequest = new HttpRequestMessage(HttpMethod.Get, url);
                mimsRequest.Headers.Clear();
                mimsRequest.Headers.Add("Host", "www.mims.com");
                mimsRequest.Headers.Add("User-Agent", "PostmanRuntime/69");
                mimsRequest.Headers.Add("Accept", "*/*");
                mimsRequest.Headers.Add("Accept-Encoding", "");
                mimsRequest.Headers.Add("Connection", "keep-alive");
                
                var response = await client.SendAsync(mimsRequest, cancellationToken);
                
                response.EnsureSuccessStatusCode(); // Throw an exception if not successful

                var responseBody = await response.Content.ReadFromJsonAsync<MimsAutoCompleteResponse>(cancellationToken: cancellationToken);
                responseBody.Suggestions = responseBody.Suggestions.Take(responseBody.Suggestions.Count - 1).ToList();
                
                var existingMedicines = await dbContext.Set<Domain.Generics.Contracts.Medicine>()
                    .Where(i => i.TenantId == tenant.Id)
                    .Where(i => i.IsDeleted == false)
                    .Where(i => responseBody.Suggestions.Select(s => s.Value.ToUpperInvariant()).Contains(i.Name))
                    .Include(i => i.MedicineVariants)
                    .AsSplitQuery()
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
                        TypeId = new Guid("570ccf7f-f68a-450d-b110-09648f3540ed"),
                        MedicineVariants = new List<MedicineVariant>
                        {
                            new()
                            {
                                Name = "No_Variant",
                                TenantId = tenant.Id,
                                UnitId = new Guid("4369507c-7cda-47c1-99b7-fd6420d5afa3")
                            }
                        }
                    }).ToList();
                    await dbContext.Set<Domain.Generics.Contracts.Medicine>().AddRangeAsync(newMedicineEntities, cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                var results = helperService.RemoveCircularReference(existingMedicines.Concat(newMedicineEntities).ToList());
                
                return new QueryResponse<List<Domain.Generics.Contracts.Medicine>>
                {
                    HttpStatusCode = HttpStatusCode.OK,
                    Response = results
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