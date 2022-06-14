using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credentials;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Credential;

public class GetCredentialHandler : QueryBaseHandler ,IRequestHandler<GetCredentialQuery, QueryResponse<CredentialResponse>>
{
    public GetCredentialHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<CredentialResponse>> Handle(GetCredentialQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.IdentityCredentials
            .Include(i => i.IdentityInfo)
            .ThenInclude(i => i.IdentityAddresses)
            .ThenInclude(i => i.CountryNavigation)
            
            .Include(i => i.IdentityInfo)
            .ThenInclude(i => i.IdentityAddresses)
            .ThenInclude(i => i.RegionNavigation)
            
            .Include(i => i.IdentityInfo)
            .ThenInclude(i => i.IdentityAddresses)
            .ThenInclude(i => i.ProvinceNavigation)
            
            .Include(i => i.IdentityInfo)
            .ThenInclude(i => i.IdentityAddresses)
            .ThenInclude(i => i.CityNavigation)
            
            .Include(i => i.IdentityInfo)
            .ThenInclude(i => i.IdentityAddresses)
            .ThenInclude(i => i.BarangayNavigation)
            
            .Include(i => i.IdentityRoles)
            .ThenInclude(i => i.RoleEntity)
            .Include(i => i.IdentityContacts)
            .ThenInclude(i => i.Entity)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken);
           
        if (entity == null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = entity.Adapt<CredentialResponse>()
        };
    }
}