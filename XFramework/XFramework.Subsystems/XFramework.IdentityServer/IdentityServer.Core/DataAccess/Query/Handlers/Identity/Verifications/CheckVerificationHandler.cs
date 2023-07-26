using IdentityServer.Core.DataAccess.Query.Entity.Identity.Verifications;
using IdentityServer.Domain.Generic.Contracts.Responses.Verification;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Verifications;

public class CheckVerificationHandler : QueryBaseHandler, IRequestHandler<CheckVerificationQuery, QueryResponse<IdentityVerificationSummaryResponse>>
{
    public CheckVerificationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<IdentityVerificationSummaryResponse>> Handle(CheckVerificationQuery request, CancellationToken cancellationToken)
    {
        var application = await GetApplication(request.RequestServer.ApplicationId);
        
        var identityCredential = await _dataLayer.IdentityCredentials
            .Include(i => i.IdentityContacts)
            .ThenInclude(i => i.Entity)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}" && i.ApplicationId == application.Id, cancellationToken: cancellationToken);
       
        if (identityCredential == null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var verificationType = await _dataLayer.IdentityVerificationEntities.AsNoTracking().FirstOrDefaultAsync(i => i.Guid == $"{request.VerificationTypeGuid}", CancellationToken.None);
        if (verificationType == null)
        {
            return new()
            {
                Message = $"Verification type with guid {request.VerificationTypeGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                
            };
        }

        var anyVerification = _dataLayer.IdentityVerifications
            .Where(i => i.VerificationTypeId == verificationType.Id)
            .Where(i => i.IdentityCredId == identityCredential.Id)
            .Where(i => i.Status == (short?) GenericStatusType.Approved)
            .Where(i => i.Expiry > DateTime.SpecifyKind(DateTime.Now.ToUniversalTime(), DateTimeKind.Utc))
            .Any();

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

        var lastVerification = await _dataLayer.IdentityVerifications
            .Where(i => i.VerificationTypeId == verificationType.Id)
            .Where(i => i.IdentityCredId == identityCredential.Id)
            .Where(i => i.Status == (short?) GenericStatusType.Pending)
            .Where(i => i.Expiry > DateTime.SpecifyKind(DateTime.Now.ToUniversalTime(), DateTimeKind.Utc))
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(CancellationToken.None);
        
        if (lastVerification == null)
        {
            return new()
            {
                Message = $"Verification with type {request.VerificationTypeGuid} has no records",
                HttpStatusCode = HttpStatusCode.NotFound,
                
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = new ()
            {
                IsVerified = false,
                LastRequestExpiration = lastVerification.Expiry,
                Guid = Guid.Parse(lastVerification.Guid)
            }
        };
    }
}