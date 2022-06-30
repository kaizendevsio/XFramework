using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Verification;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Verifications;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Credential;

public class ChangePasswordHandler : CommandBaseHandler, IRequestHandler<ChangePasswordCmd, CmdResponse<ChangePasswordCmd>>
{
    private readonly IMediator _mediator;

    public ChangePasswordHandler(IDataLayer dataLayer, IMediator mediator)
    {
        _mediator = mediator;
        _dataLayer = dataLayer;
    }
        
    public async Task<CmdResponse<ChangePasswordCmd>> Handle(ChangePasswordCmd request, CancellationToken cancellationToken)
    {
        if (GetCredential(request, cancellationToken, out var entity, out var handle)) return handle;
        
        /*
        var response = await _mediator.Send(new CheckVerificationQuery
        {
            RequestServer = request.RequestServer,
            CredentialGuid = Guid.Parse(entity.Guid),
            VerificationTypeGuid = Guid.Parse("45a7a8a7-3735-4a58-b93f-aa9e7b24a7c4")

        });

        if (response.HttpStatusCode is not HttpStatusCode.Accepted)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = "Invalid verification code",
                Request = request
            };
        }*/
        
        var hashPasswordByte = Encoding.ASCII.GetBytes(BCrypt.Net.BCrypt.HashPassword(inputKey: request.Password, workFactor:11));

        entity.PasswordByte = hashPasswordByte;
        _dataLayer.Update(entity);
        await _dataLayer.SaveChangesAsync(cancellationToken);
            
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }

    private bool GetCredential(ChangePasswordCmd request, CancellationToken cancellationToken, out IdentityCredential entity, out CmdResponse<ChangePasswordCmd> handle)
    {
            
        entity = _dataLayer.IdentityCredentials
            .Where(i => i.Guid == $"{request.CredentialGuid}" || i.UserName == request.UserName)
            .FirstOrDefault();

        if (entity != null) {
            handle = new();
            return false;
        }
        var entityList1 = _dataLayer.IdentityContacts
            .Include(i => i.UserCredential)
            .Where(i => i.Value == request.PhoneNumber.ValidatePhoneNumber(false) && i.EntityId == (long?)GenericContactType.Phone)
            .Select(i => i.UserCredential)
            .ToList();

        if (entityList1.Count > 1)
        {
            {
                handle = new()
                {
                    Message = $"Identity with data [PhoneNumber: {request.PhoneNumber}] exists on multiple identities and is therefore ambiguous",
                    HttpStatusCode = HttpStatusCode.Conflict
                };
                return true;
            }
        }

        if (entityList1.Any()) entity = entityList1.First();
        if (entity != null) {
            handle = new();
            return false;
        }

        request.Email.ValidateEmailAddress();
        var entityList2 = _dataLayer.IdentityContacts
            .Include(i => i.UserCredential)
            .Where(i => i.Value == request.Email && i.EntityId == (long?)GenericContactType.Email)
            .Select(i => i.UserCredential)
            .ToList();

        if (entityList2.Count > 1)
        {
            {
                handle = new()
                {
                    Message = $"Identity with data [Email: {request.Email};] exists on multiple identities and is therefore ambiguous",
                    HttpStatusCode = HttpStatusCode.Conflict
                };
                return true;
            }
        }

        if (entityList2.Any()) entity = entityList2.First();
        if (entity != null)
        {
            handle = new();
            return false;
        }

        handle = new()
        {
            Message = $"Identity with data [GUID: {request.CredentialGuid}; UserName: {request.UserName}; Email: {request.Email}; PhoneNumber: {request.PhoneNumber}] does not exist",
            HttpStatusCode = HttpStatusCode.NotFound
        };
        return true;
    }
}