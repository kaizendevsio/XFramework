namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Credential;

public class CreateCredentialHandler : CommandBaseHandler, IRequestHandler<CreateCredentialCmd, CmdResponseBO<CreateCredentialCmd>>
{

    public CreateCredentialHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponseBO<CreateCredentialCmd>> Handle(CreateCredentialCmd request, CancellationToken cancellationToken)
    {
        var application = await GetApplication(request.RequestServer.ApplicationId);
        var identityInfo = await _dataLayer.TblIdentityInformations.FirstOrDefaultAsync(i => i.Guid == $"{request.IdentityGuid}", cancellationToken: cancellationToken);
        var entity = request.Adapt<TblIdentityCredential>();
            
        if (identityInfo == null)
        {
            return new CmdResponseBO<CreateCredentialCmd>
            {
                Message = $"Identity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var anyCredential = _dataLayer.TblIdentityCredentials
            .AsNoTracking()
            .Any(i => i.UserName == request.UserName);
            
        if (anyCredential)
        {
            return new()
            {
                Message = $"Username '{request.UserName}' already exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var hashPasswordByte = Encoding.ASCII.GetBytes(BCrypt.Net.BCrypt.HashPassword(inputKey: request.PasswordString, workFactor:11));

        entity.Guid = request.Guid != null ? request.Guid.ToString() : Guid.NewGuid().ToString();
        entity.PasswordByte = hashPasswordByte;
        entity.IdentityInfoId = identityInfo.Id;
        entity.ApplicationId = application.Id;

        await _dataLayer.TblIdentityCredentials.AddAsync(entity, cancellationToken);
            
        var roleEntity = await _dataLayer.TblIdentityRoleEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.RoleEntity}", cancellationToken: cancellationToken);

        if (roleEntity == null)
        {
            return new ()
            {
                Message = $"Role with Guid '{request.RoleEntity}' does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var role = new TblIdentityRole()
        {
            UserCred = entity,
            RoleEntityId = roleEntity.Id
        };

        await _dataLayer.TblIdentityRoles.AddAsync(role, cancellationToken);
        await _dataLayer.SaveChangesAsync(cancellationToken);

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}