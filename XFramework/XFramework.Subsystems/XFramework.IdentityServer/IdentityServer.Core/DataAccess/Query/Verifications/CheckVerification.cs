using IdentityServer.Domain.Generic.Contracts.Requests;
using IdentityServer.Domain.Generic.Contracts.Responses;
using Microsoft.Extensions.Logging;
using XFramework.Domain.Contexts;
using XFramework.Domain.Generic.Contracts;

namespace IdentityServer.Core.DataAccess.Query.Verifications;

public class CheckVerification(
        IMediator Mediator,
        AppDbContext appDbContext,
        ILogger<CheckVerification> logger) 
    : IRequestHandler<CheckVerificationRequest, QueryResponse<CheckVerificationResponse>>
{
    public async Task<QueryResponse<CheckVerificationResponse>> Handle(CheckVerificationRequest request, CancellationToken cancellationToken)
    {
        var application = await GetTenant(request.Metadata.TenantId);
        
        var identityCredential = await appDbContext.IdentityCredentials
            .Include(i => i.IdentityContacts)
            .ThenInclude(i => i.Type)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == request.CredentialId && i.TenantId == application.Id, cancellationToken: cancellationToken);
       
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
    
    private async Task<Tenant> GetTenant(Guid? id)
    {
        if (id is null) throw new ArgumentNullException(nameof(id));
        var entity = await appDbContext.Tenants
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id);

        ArgumentNullException.ThrowIfNull(entity, "Tenant");

        return entity;
    }
}