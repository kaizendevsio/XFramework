namespace Community.Core.DataAccess.Commands.Identity;

public class UpdateCommunityIdentity(
    AppDbContext appDbContext,
    ILogger<CreateCommunityIdentity> logger,
    ITenantService tenantService
) 
    : IRequestHandler<UpdateCommunityIdentityRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(UpdateCommunityIdentityRequest request, CancellationToken cancellationToken)
    {
        var communityIdentity = await appDbContext.CommunityIdentities.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken: cancellationToken);
        if (communityIdentity == null)
        {
            return new ()
            {
                Message = $"Community identity with guid {request.Id} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                
            };
        }
        
        communityIdentity = request.Adapt(communityIdentity);
        
        var credential = new IdentityCredential();
        if (request.CredentialId != Guid.Empty)
        {
            credential = await appDbContext.IdentityCredentials.FirstOrDefaultAsync(i => i.Id == request.CredentialId, cancellationToken: cancellationToken);
            if (credential == null)
            {
                return new ()
                {
                    Message = $"Credential with guid {request.CredentialId} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            communityIdentity.Credential = credential;
        }

        var communityIdentityType = new CommunityIdentityType();
        if (request.CommunityIdentityTypeId != Guid.Empty)
        {
            communityIdentityType = await appDbContext.CommunityIdentityTypes
                .FirstOrDefaultAsync(i => i.Id == request.CommunityIdentityTypeId, cancellationToken: cancellationToken);
         
            if (communityIdentityType == null)
            {
                return new ()
                {
                    Message = $"Community Identity Type with guid {request.CommunityIdentityTypeId} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound,
                    
                };
            }
            communityIdentity.Type = communityIdentityType;
        }

        appDbContext.CommunityIdentities.Update(communityIdentity);
        await appDbContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}