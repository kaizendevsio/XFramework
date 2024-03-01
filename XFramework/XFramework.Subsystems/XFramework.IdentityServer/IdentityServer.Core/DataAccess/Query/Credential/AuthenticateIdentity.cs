using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdentityServer.Domain.Generic.Contracts.Requests;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Enums;
using XFramework.Core.Services;
using XFramework.Integration.Abstractions;

namespace IdentityServer.Core.DataAccess.Query.Credential;
using XFramework.Domain.Generic.Contracts;

public class AuthenticateIdentity(
        IJwtService jwtService,
        AppDbContext appDbContext,
        IHelperService helperService,
        ITenantService tenantService,
        ILogger<AuthenticateIdentity> logger
    )
    : IRequestHandler<AuthenticateIdentityRequest, QueryResponse<AuthenticateIdentityResponse>>
{

    public async Task<QueryResponse<AuthenticateIdentityResponse>> Handle(AuthenticateIdentityRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);
        
        if (request.RoleId == Guid.Empty)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = "Role is required"
            };
        }
        
        var credential = await ValidateAuthorization(request, tenant, request.AuthorizationType, cancellationToken);
        if (credential is null)
        {
            return new()
            {
                Message = $"User or identity does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
            
        credential = await ValidatePassword(request, request.AuthorizationType, credential, cancellationToken);
        if (credential == null)
        {
            //_recordsService.NewAuthorizationLog(AuthenticationState.WrongPassword, cuid);
            return new()
            {
                Message = $"Wrong password",
                HttpStatusCode = HttpStatusCode.BadRequest
            };
        }

        var roleList = await GetRoleList(cancellationToken, credential);
        if (roleList is null || roleList.Any(i => i.Type.Id == request.RoleId) is false)
        {
            return new()
            {
                Message = $"You do not have permission to access this resource",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var token = new JwtToken();
        if (request.GenerateToken)
        {
            token = await jwtService.GenerateToken(request.Username, credential.Id, roleList.Select(i => i.TypeId ?? Guid.Empty).ToList());
        }
        //_recordsService.NewAuthorizationLog(AuthenticationState.Success, cuid);

        credential = helperService.RemoveCircularReference(credential);
        
        return new()
        {
            Message = $"Identity Authorized",
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = new()
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                Identity = credential.IdentityInfo,
                Credential = credential
            }
        };
    }

    private async Task<List<IdentityRole>?> GetRoleList(CancellationToken cancellationToken, IdentityCredential credential)
    {
        var roleList = await appDbContext.IdentityRoles
            .AsNoTracking()
            .Include(i => i.Type)
            .Where(i => i.CredentialId == credential.Id)
            .ToListAsync(cancellationToken: cancellationToken);

        return roleList.Any() ? roleList : null;
    }

    private async Task<IdentityCredential?> ValidateAuthorization(AuthenticateIdentityRequest request, Tenant tenant, AuthorizationType authorizationType, CancellationToken cancellationToken)
    {
        IdentityCredential? result;
            
        reAuth:
        switch (authorizationType)
        {
            case AuthorizationType.Default:
                
                var getDefaults = await appDbContext.RegistryConfigurations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.TenantId == tenant.Id & i.Key == "DefaultAuthorizeBy", cancellationToken: cancellationToken);
                if (getDefaults is null)
                {
                    throw new ArgumentException($"Unable to login: Tenant with id '{tenant.Id}' does not have 'DefaultAuthorizeBy' key in registry");
                }
                authorizationType = (AuthorizationType) int.Parse(getDefaults.Value);
                goto reAuth;
            case AuthorizationType.UsernameEmailPhone:
                result = await appDbContext.IdentityCredentials
                    .Include(i => i.IdentityInfo)
                    .Include(i => i.IdentityRoles)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i =>
                            i.TenantId == tenant.Id & 
                            i.UserName == request.Username,
                        cancellationToken: cancellationToken);
                result ??= await appDbContext.IdentityContacts
                    .Include(i => i.Credential.IdentityRoles)
                    .Include(i => i.Credential.IdentityInfo)
                    .AsNoTracking()
                    .Where(i => 
                        i.Credential.TenantId == tenant.Id & 
                        i.Value == request.Username & 
                        i.Type.Name == nameof(GenericContactType.Email))
                    .Select(i => i.Credential)
                    .FirstOrDefaultAsync(cancellationToken);
                result ??= await appDbContext.IdentityContacts
                    .Include(i => i.Credential.IdentityRoles)
                    .Include(i => i.Credential.IdentityInfo)
                    .AsNoTracking()
                    .Where(i => 
                        i.Credential.TenantId == tenant.Id & 
                        i.Value == request.Username.ValidatePhoneNumber(true) & 
                        i.Type.Name == nameof(GenericContactType.Phone))
                    .Select(i => i.Credential)
                    .FirstOrDefaultAsync(cancellationToken);
                break;
            case AuthorizationType.Username:
                result = await appDbContext.IdentityCredentials
                    .Include(i => i.IdentityInfo)
                    .Include(i => i.IdentityRoles)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.TenantId == tenant.Id & i.UserName == request.Username,
                        cancellationToken: cancellationToken);
                break;
            case AuthorizationType.Email:
                request.Username?.ValidateEmailAddress();
                result = await appDbContext.IdentityContacts
                    .Include(i => i.Credential.IdentityRoles)
                    .Include(i => i.Credential.IdentityInfo)
                    .AsNoTracking()
                    .Where(i => 
                        i.Credential.TenantId == tenant.Id & 
                        i.Value == request.Username & 
                        i.Type.Name == nameof(GenericContactType.Email))
                    .Select(i => i.Credential)
                    .FirstOrDefaultAsync(cancellationToken);
                break;
            case AuthorizationType.Phone:
                result = await appDbContext.IdentityContacts
                    .Include(i => i.Credential.IdentityRoles)
                    .Include(i => i.Credential.IdentityInfo)
                    .AsNoTracking()
                    .Where(i => 
                        i.Credential.TenantId == tenant.Id & 
                        i.Value == request.Username.ValidatePhoneNumber(true) & 
                        i.Type.Name == nameof(GenericContactType.Phone))
                    .Select(i => i.Credential)
                    .FirstOrDefaultAsync(cancellationToken);
                break;
            case AuthorizationType.Token:
                result = await appDbContext.IdentityCredentials
                    .Include(i => i.IdentityRoles)
                    .Include(i => i.IdentityInfo)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.UserName == request.Username,
                        cancellationToken: cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return result;
    }

    private async Task<IdentityCredential?> ValidatePassword(AuthenticateIdentityRequest request, AuthorizationType authorizationType, IdentityCredential credential, CancellationToken cancellationToken)
    {
        if (authorizationType == AuthorizationType.Token) return credential;

        var hashPassword = Encoding.ASCII.GetString(credential.PasswordByte);
        return BCrypt.Net.BCrypt.Verify(request.Password, hashPassword) is false ? null : credential;
    }
        
    
}