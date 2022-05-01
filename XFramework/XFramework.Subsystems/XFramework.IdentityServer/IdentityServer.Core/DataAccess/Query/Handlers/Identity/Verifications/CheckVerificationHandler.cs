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
        var verificationType = await _dataLayer.IdentityVerificationEntities.AsNoTracking().FirstOrDefaultAsync(i => i.Guid == $"{request.VerificationTypeGuid}", CancellationToken.None);
        if (verificationType == null)
        {
            return new()
            {
                Message = $"Verification type with guid {request.VerificationTypeGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }

        var anyVerification = _dataLayer.IdentityVerifications
            .Where(i => i.VerificationTypeId == verificationType.Id)
            .Where(i => i.Status == (short?) GenericStatusType.Approved)
            .Where(i => i.Expiry > DateTime.SpecifyKind(DateTime.Now.ToUniversalTime(), DateTimeKind.Utc))
            .Any();

        if (anyVerification)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                IsSuccess = true,
                Response = new ()
                {
                    IsVerified = true
                }
            };
        }

        var lastVerification = await _dataLayer.IdentityVerifications
            .Where(i => i.VerificationTypeId == verificationType.Id)
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
                IsSuccess = false
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Response = new ()
            {
                IsVerified = false,
                LastRequestExpiration = lastVerification.Expiry,
                Guid = Guid.Parse(lastVerification.Guid)
            }
        };
    }
}