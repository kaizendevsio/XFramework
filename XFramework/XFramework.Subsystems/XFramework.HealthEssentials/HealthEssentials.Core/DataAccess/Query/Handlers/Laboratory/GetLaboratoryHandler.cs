using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryQuery, QueryResponse<LaboratoryResponse>>
{
    public GetLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<LaboratoryResponse>> Handle(GetLaboratoryQuery request, CancellationToken cancellationToken)
    {
        var laboratory = await _dataLayer.HealthEssentialsContext.Laboratories
            .AsNoTracking()
            .Include(i => i.LaboratoryLocations)
            .Include(i => i.LaboratoryMembers)
            .Where(i => i.Guid == $"{request.Guid}")
            .AsSplitQuery()
            .FirstOrDefaultAsync(CancellationToken.None);

        if (laboratory is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = $"Laboratory with the Guid '{request.Guid}' does not exist",
                IsSuccess = true
            };
        }
        
        var response = laboratory.Adapt<LaboratoryResponse>();

        response.Files = _dataLayer.XnelSystemsContext.StorageFiles.AsNoTracking().Where(i => i.IdentifierGuid == response.Guid).ToList().Adapt<List<StorageFileResponse>>();
        for (var index = 0; index < response.LaboratoryMembers.Count; index++)
        {
            response.LaboratoryMembers[index].Credential = _dataLayer.XnelSystemsContext.IdentityCredentials
                .AsNoTracking()
                .Include(i => i.IdentityInfo)
                .Include(i => i.IdentityContacts)
                .ThenInclude(i => i.Entity)
                .AsSplitQuery()
                .Where(i => i.Id == response.LaboratoryMembers[index].CredentialId)
                .FirstOrDefault()?
                .Adapt<CredentialResponse>();
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory Found",
            IsSuccess = true,
            Response = response
        };        
    }
}