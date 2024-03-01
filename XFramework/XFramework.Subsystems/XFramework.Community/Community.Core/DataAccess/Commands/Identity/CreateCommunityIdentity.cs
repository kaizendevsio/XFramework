namespace Community.Core.DataAccess.Commands.Identity;

public class CreateCommunityIdentity(
    AppDbContext appDbContext,
    ILogger<CreateCommunityIdentity> logger,
    ITenantService tenantService
) 
    : IRequestHandler<CreateCommunityIdentityRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(CreateCommunityIdentityRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);

        var credential = await appDbContext.IdentityCredentials
            .Where(i => i.TenantId == tenant.Id)
            .Include(i => i.IdentityInfo)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == request.CredentialId, cancellationToken: cancellationToken);
     
        if (credential == null)
        {
            return new ()
            {
                Message = $"Credential with Id {request.CredentialId} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var communityIdentityType = await appDbContext.CommunityIdentityTypes
            .FirstOrDefaultAsync(i => i.Id == request.CommunityIdentityTypeId, cancellationToken: cancellationToken);
     
        if (communityIdentityType == null)
        {
            return new ()
            {
                Message = $"Community identity entity with Id {request.CommunityIdentityTypeId} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var identityFileTypes = await appDbContext.CommunityIdentityFileTypes.ToListAsync(CancellationToken.None);
        var storageFileTypes = await appDbContext.StorageFileTypes.ToListAsync(CancellationToken.None);

        var pngType = storageFileTypes.FirstOrDefault(i => i.Id == new Guid("af6b9396-ba01-4f88-a5d0-e0cfbc038146"));
        var entity = new CommunityIdentity
        {
            Credential = credential,
            HandleName = string.IsNullOrEmpty(request.HandleName) ? $"{credential.IdentityInfo.FirstName} {credential.IdentityInfo.LastName}" : request.HandleName,
            Tagline = request.Tagline,
            Alias = request.Alias,
            Status = (int) CommunityIdentityStatus.Active,
            LastActive = DateTime.SpecifyKind(DateTime.Now.ToUniversalTime(), DateTimeKind.Utc),
            Type = communityIdentityType,
            CommunityIdentityFiles =
            {
                // Profile Photo
                new()
                {
                    Type = identityFileTypes.FirstOrDefault(i => i.Id == new Guid("996dd417-170c-4ac9-b565-62caf4ab5ccf")),
                    Storage = new()
                    {
                        ContentPath = "",
                        Type = pngType
                    }
                },
                // Cover Photo
                new()
                {
                    Type = identityFileTypes.FirstOrDefault(i => i.Id == new Guid("8716ec30-b061-45cc-ad5b-77bda960d90e")),
                    Storage = new()
                    {
                        ContentPath = "",
                        Type = pngType
                    }
                }
            }
        };

        appDbContext.CommunityIdentities.Add(entity);
        await appDbContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = $"Community identity created successfully",
        };
    }
}