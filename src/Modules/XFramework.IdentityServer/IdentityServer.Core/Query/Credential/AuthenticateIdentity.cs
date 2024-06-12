using System.Text.Json;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using XFramework.Core.Services;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Services;
using Session = XFramework.Domain.Shared.Contracts.Session;

namespace IdentityServer.Core.Query.Credential;

public class AuthenticateIdentity(
        IJwtService jwtService,
        AppDbContext appDbContext,
        IHelperService helperService,
        ITenantService tenantService,
        CacheManager cache,
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

        if (string.IsNullOrEmpty(request.UserName))
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = "Username is required"
            };
        }
        
        if (string.IsNullOrEmpty(request.Password))
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = "Password is required"
            };
        }

        AuthorizationLog? authorizationLog = null;
        
        var originalCredential = await ValidateAuthorization(request, tenant, request.AuthorizationType, cancellationToken);
        if (originalCredential is null)
        {
            return new()
            {
                Message = $"User or identity does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
            
        var credential = await ValidatePassword(request, request.AuthorizationType, originalCredential, cancellationToken);
        if (credential == null)
        {
            authorizationLog = new()
            {
                TenantId = tenant.Id,
                CredentialId = originalCredential.Id,
                Ipaddress = request.Metadata.IpAddress,
                IsSuccess = false,
                AuthStatus = AuthenticationState.WrongPassword,
                LoginSource = request.Metadata.Name,
                DeviceName = request.Metadata.DeviceName,
                DeviceAgent = request.Metadata.DeviceAgent
            };
            
            appDbContext.AuthorizationLogs.Add(authorizationLog);
            _ = appDbContext.SaveChangesAsync(CancellationToken.None);
            
            return new()
            {
                Message = $"Wrong password",
                HttpStatusCode = HttpStatusCode.BadRequest
            };
        }

        var roleList = await GetRoleList(cancellationToken, credential);
        if (roleList is null || roleList.Any(i => i.Type.Id == request.RoleId) is false)
        {
            authorizationLog = new()
            {
                TenantId = tenant.Id,
                CredentialId = credential.Id,
                Ipaddress = request.Metadata.IpAddress,
                IsSuccess = false,
                AuthStatus = AuthenticationState.Unauthorized,
                LoginSource = request.Metadata.Name,
                DeviceName = request.Metadata.DeviceName,
                DeviceAgent = request.Metadata.DeviceAgent
            };
            
            appDbContext.AuthorizationLogs.Add(authorizationLog);
            _ = appDbContext.SaveChangesAsync(CancellationToken.None);
            
            return new()
            {
                Message = $"You do not have permission to access this resource",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var token = new JwtToken();
        if (request.GenerateToken)
        {
            token = await jwtService.GenerateToken(request.UserName, credential.Id, roleList.Select(i => i.TypeId ?? Guid.Empty).ToList());
        }

        Guid? sessionTypeId = null;
        if (request.AuthorizationType is not AuthorizationType.Token)
        {
           sessionTypeId = cache.Get<Guid>("SessionTypeId:User");
           if (sessionTypeId is null || sessionTypeId == Guid.Empty)
           {
               var userSsessionTypeId = await appDbContext.SessionTypes
                   .Where(i => i.TenantId == tenant.Id)
                   .Where(i => i.SystemReferenceId == IdentityConstants.SessionType.User)
                   .Where(i => i.Name == "User")
                   .Select(i => i.Id)
                   .FirstOrDefaultAsync(CancellationToken.None);
               
               await cache.Set("SessionTypeId:User", userSsessionTypeId);
               sessionTypeId = userSsessionTypeId;
           }
        }
        else
        {
            sessionTypeId = cache.Get<Guid>("SessionTypeId:Token");
            if (sessionTypeId is null)
            {
                var userSsessionTypeId = await appDbContext.SessionTypes
                    .Where(i => i.TenantId == tenant.Id)
                    .Where(i => i.SystemReferenceId == IdentityConstants.SessionType.Service)
                    .Where(i => i.Name == "Service")
                    .Select(i => i.Id)
                    .FirstOrDefaultAsync(CancellationToken.None);
                
                await cache.Set("SessionTypeId:Token", userSsessionTypeId);
                sessionTypeId = userSsessionTypeId;
            }
        }
        
        authorizationLog = new()
        {
            TenantId = tenant.Id,
            CredentialId = credential.Id,
            Ipaddress = request.Metadata.IpAddress,
            IsSuccess = true,
            AuthStatus = AuthenticationState.Authenticated,
            LoginSource = request.Metadata.Name,
            DeviceName = request.Metadata.DeviceName,
            DeviceAgent = request.Metadata.DeviceAgent,
            SessionId = token.SessionId
        };
        
        var session = new Session()
        {
            Id = authorizationLog.SessionId.Value,
            TenantId = tenant.Id,
            SessionTypeId = sessionTypeId,
            CredentialId = credential.Id,
            SessionData = JsonSerializer.Serialize(token),
        };
        
        appDbContext.AuthorizationLogs.Add(authorizationLog);
        appDbContext.Session.Add(session);

        await appDbContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Identity Authorized",
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = new()
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                SessionId = token.SessionId,
                Identity = credential.IdentityInfo,
                Credential = credential,
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

    private async Task<IdentityCredential?> ValidateAuthorization(AuthenticateIdentityRequest request, XFramework.Domain.Shared.Contracts.Tenant tenant, AuthorizationType authorizationType, CancellationToken cancellationToken)
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
                            i.UserName == request.UserName,
                        cancellationToken: cancellationToken);
                result ??= await appDbContext.IdentityContacts
                    .Include(i => i.Credential.IdentityRoles)
                    .Include(i => i.Credential.IdentityInfo)
                    .AsNoTracking()
                    .Where(i => 
                        i.Credential.TenantId == tenant.Id & 
                        i.Value == request.UserName & 
                        i.Type.Name == nameof(GenericContactType.Email))
                    .Select(i => i.Credential)
                    .FirstOrDefaultAsync(cancellationToken);
                result ??= await appDbContext.IdentityContacts
                    .Include(i => i.Credential.IdentityRoles)
                    .Include(i => i.Credential.IdentityInfo)
                    .AsNoTracking()
                    .Where(i => 
                        i.Credential.TenantId == tenant.Id & 
                        i.Value == request.UserName.ValidatePhoneNumber(true) & 
                        i.Type.Name == nameof(GenericContactType.Phone))
                    .Select(i => i.Credential)
                    .FirstOrDefaultAsync(cancellationToken);
                break;
            case AuthorizationType.Username:
                result = await appDbContext.IdentityCredentials
                    .Include(i => i.IdentityInfo)
                    .Include(i => i.IdentityRoles)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.TenantId == tenant.Id & i.UserName == request.UserName,
                        cancellationToken: cancellationToken);
                break;
            case AuthorizationType.Email:
                request.UserName?.ValidateEmailAddress();
                result = await appDbContext.IdentityContacts
                    .Include(i => i.Credential.IdentityRoles)
                    .Include(i => i.Credential.IdentityInfo)
                    .AsNoTracking()
                    .Where(i => 
                        i.Credential.TenantId == tenant.Id & 
                        i.Value == request.UserName & 
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
                        i.Value == request.UserName.ValidatePhoneNumber(true) & 
                        i.Type.Name == nameof(GenericContactType.Phone))
                    .Select(i => i.Credential)
                    .FirstOrDefaultAsync(cancellationToken);
                break;
            case AuthorizationType.Token:
                result = await appDbContext.IdentityCredentials
                    .Include(i => i.IdentityRoles)
                    .Include(i => i.IdentityInfo)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.UserName == request.UserName,
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