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
                
                return new QueryResponse<List<Domain.Generics.Contracts.Medicine>>
                {
                    Response = responseBody?.Suggestions?.Take(responseBody.Suggestions.Count - 1).Select(s => new Domain.Generics.Contracts.Medicine
                    {
                        Name = s.Value
                    }).ToList()
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