using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using XFramework.Core.Services;

namespace IdentityServer.Core.DataAccess.Query.Verifications;

public class CheckVerification(
        IMediator Mediator,
        AppDbContext appDbContext,
        ITenantService tenantService,
        ILogger<CheckVerification> logger) 
    : IRequestHandler<CheckVerificationRequest, QueryResponse<CheckVerificationResponse>>
{
    public async Task<QueryResponse<CheckVerificationResponse>> Handle(CheckVerificationRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);
        
        var identityCredential = await appDbContext.IdentityCredentials
            .Include(i => i.IdentityContacts)
            .ThenInclude(i => i.Type)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == request.CredentialId && i.TenantId == tenant.Id, cancellationToken: cancellationToken);
       
        if (identityCredential is null)
        {
            return new ()
            {
                Message = $"Identity credential with id {request.CredentialId} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var verificationType = await appDbContext.IdentityVerificationTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.VerificationTypeId, CancellationToken.None);
        if (verificationType is null)
        {
            return new()
            {
                Message = $"Verification type with id {request.VerificationTypeId} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                
            };
        }

        var anyVerification = await appDbContext.IdentityVerifications
            .Where(i => i.VerificationTypeId == verificationType.Id)
            .Where(i => i.CredentialId == identityCredential.Id)
            .Where(i => i.Status == (short?) GenericStatusType.Approved)
            .Where(i => i.Expiry > DateTime.SpecifyKind(DateTime.Now.ToUniversalTime(), DateTimeKind.Utc))
            .AnyAsync(CancellationToken.None);

        if (anyVerification)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = new ()
                {
                    IsVerified = true
                }
            };
        }

        var lastVerification = await appDbContext.IdentityVerifications
            .Where(i => i.VerificationTypeId == verificationType.Id)
            .Where(i => i.CredentialId == identityCredential.Id)
            .Where(i => i.Status == (short?) GenericStatusType.Pending)
            .Where(i => i.Expiry > DateTime.SpecifyKind(DateTime.Now.ToUniversalTime(), DateTimeKind.Utc))
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(CancellationToken.None);
        
        if (lastVerification == null)
        {
            return new()
            {
                Message = $"No pending verification found for credential with id {request.CredentialId}",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = new()
            {
                IsVerified = false,
                LastVerification = lastVerification
            }
        };
    }
}