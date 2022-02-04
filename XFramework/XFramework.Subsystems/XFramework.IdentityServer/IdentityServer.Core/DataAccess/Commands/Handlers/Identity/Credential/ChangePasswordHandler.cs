namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Credential;

public class ChangePasswordHandler : CommandBaseHandler, IRequestHandler<ChangePasswordCmd, CmdResponseBO<ChangePasswordCmd>>
{
    public ChangePasswordHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<CmdResponseBO<ChangePasswordCmd>> Handle(ChangePasswordCmd request, CancellationToken cancellationToken)
    {
        if (GetCredential(request, cancellationToken, out var entity, out var handle)) return handle;
        var hashPasswordByte = Encoding.ASCII.GetBytes(BCrypt.Net.BCrypt.HashPassword(inputKey: request.PasswordString, workFactor:11));

        entity.PasswordByte = hashPasswordByte;
        _dataLayer.Update(entity);
        await _dataLayer.SaveChangesAsync(cancellationToken);
            
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }

    private bool GetCredential(ChangePasswordCmd request, CancellationToken cancellationToken, out TblIdentityCredential entity, out CmdResponseBO<ChangePasswordCmd> handle)
    {
            
        entity = _dataLayer.TblIdentityCredentials
            .Where(i => i.Guid == $"{request.CredentialGuid}" || i.UserName == request.UserName)
            .FirstOrDefault();

        if (entity != null) {
            handle = new();
            return false;
        }
        var entityList1 = _dataLayer.TblIdentityContacts
            .Include(i => i.UserCredential)
            .Where(i => i.Value == request.PhoneNumber.ValidatePhoneNumber(false) && i.UcentitiesId == (long?)GenericContactType.Phone)
            .Select(i => i.UserCredential)
            .ToList();

        if (entityList1.Count > 1)
        {
            {
                handle = new()
                {
                    Message =
                        $"Identity with data [PhoneNumber: {request.PhoneNumber}] exists on multiple identities and is therefore ambiguous",
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
        var entityList2 = _dataLayer.TblIdentityContacts
            .Include(i => i.UserCredential)
            .Where(i => i.Value == request.Email && i.UcentitiesId == (long?)GenericContactType.Email)
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