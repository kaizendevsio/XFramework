using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryMemberHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryMemberQuery, QueryResponse<LaboratoryMemberResponse>>
{
    public GetLaboratoryMemberHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<LaboratoryMemberResponse>> Handle(GetLaboratoryMemberQuery request, CancellationToken cancellationToken)
    {
        var laboratoryMember = await _dataLayer.HealthEssentialsContext.LaboratoryMembers
            .Include(i => i.Laboratory)
            .Include(i => i.LaboratoryLocation)
            .Where(i => i.Guid == $"{request.Guid}")
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(CancellationToken.None);
        
        if (laboratoryMember is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = $"Laboratory member with guid {request.Guid} not found",
                IsSuccess = true
            };
        }
        
        var credential = await _dataLayer.XnelSystemsContext.IdentityCredentials
            .Where(i => i.Guid == laboratoryMember.CredentialId)
            .AsNoTracking()
            .FirstOrDefaultAsync(CancellationToken.None);
       
        if (credential is null)
        {
            return new ()
            {
                Message = $"Credential with Guid {laboratoryMember.CredentialId} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var response = laboratoryMember.Adapt<LaboratoryMemberResponse>();
        response.Credential = credential.Adapt<CredentialResponse>();
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory member found",
            IsSuccess = true,
            Response = response
        };        
    }
}