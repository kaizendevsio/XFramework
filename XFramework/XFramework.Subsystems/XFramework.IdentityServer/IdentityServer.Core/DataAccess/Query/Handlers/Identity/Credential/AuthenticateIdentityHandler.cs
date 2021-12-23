using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.DataTransferObjects;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Enums;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;
using XFramework.Integration.Services.Helpers;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Credential
{
    public class AuthenticateIdentityHandler : QueryBaseHandler, IRequestHandler<AuthenticateCredentialQuery, QueryResponseBO<AuthorizeIdentityContract>>
    {
        public AuthenticateIdentityHandler(IDataLayer dataLayer, ICachingService cachingService, IHelperService helperService, JwtOptionsBO jwtOptionsBo, IJwtService jwtService, ILoggerWrapper recordsWrapper)
        {
            _recordsService = recordsWrapper;
            _jwtService = jwtService;
            _jwtOptions = jwtOptionsBo;
            _helperService = helperService;
            _dataLayer = dataLayer;
            _cachingService = cachingService;
        }

        public async Task<QueryResponseBO<AuthorizeIdentityContract>> Handle(AuthenticateCredentialQuery request, CancellationToken cancellationToken)
        {
            var credential = await ValidateAuthorization(request, cancellationToken, request.AuthorizeBy);
            if (credential == null)
            {
                return new()
                {
                    Message = $"Identity does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            
            var cuid = new Guid(credential.Cuid);
            credential = await ValidatePassword(request, request.AuthorizeBy, credential, cancellationToken);
            if (credential == null)
            {
                _recordsService.NewAuthorizationLog(AuthenticationState.WrongPassword, cuid);
                return new()
                {
                    Message = $"Identity Authentication Failed",
                    HttpStatusCode = HttpStatusCode.BadRequest
                };
            }

            var roleList = await GetRoleList(cancellationToken, credential, cuid);

            var token = await _jwtService.GenerateToken(request.Username, cuid, roleList.Select(i => i.RoleEntityId).Adapt<List<RoleEntity>>());
            _recordsService.NewAuthorizationLog(AuthenticationState.Success, cuid);
            
            return new()
            {
                Message = $"Identity Authorized",
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = new()
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken,
                    Uid = Guid.Parse(credential.IdentityInfo.Uuid),
                    Cuid = Guid.Parse(credential.Cuid),
                    RoleList = roleList.Adapt<List<IdentityRoleContract>>() 
                }
            };
        }

        private async Task<List<TblIdentityRole>> GetRoleList(CancellationToken cancellationToken, TblIdentityCredential credential, Guid cuid)
        {
            var roleList = await _dataLayer.TblIdentityRoles
                .AsNoTracking()
                .Where(i => i.UserCredId == credential.Id)
                .ToListAsync(cancellationToken: cancellationToken);

            if (roleList == null)
            {
                _recordsService.NewAuthorizationLog(AuthenticationState.InvalidUser, cuid);
                return roleList;
            }

            return roleList;
        }

        private async Task<TblIdentityCredential> ValidateAuthorization(AuthenticateCredentialQuery request, CancellationToken cancellationToken, AuthorizeBy authorizeBy)
        {
            TblIdentityCredential result;
            
            reAuth:
            switch (authorizeBy)
            {
                case AuthorizeBy.Default:
                    var getDefaults = await _dataLayer.TblConfigurations
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.ApplicationId == request.RequestServer.ApplicationId & i.Key == "DefaultAuthorizeBy", cancellationToken: cancellationToken);
                    authorizeBy = (AuthorizeBy) int.Parse(getDefaults.Value);
                    goto reAuth;
                case AuthorizeBy.UsernameEmailPhone:
                    result = await _dataLayer.TblIdentityCredentials
                        .Include(i => i.IdentityInfo)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.UserName == request.Username,
                        cancellationToken: cancellationToken);
                    result ??= _dataLayer.TblIdentityContacts
                        .Include(i => i.UserCredential)
                        .ThenInclude(i => i.IdentityInfo)
                        .AsNoTracking()
                        .FirstOrDefault(i => i.Value == request.Username & i.UcentitiesId == (long?)GenericContactType.Email)?.UserCredential;
                    result ??= _dataLayer.TblIdentityContacts
                        .Include(i => i.UserCredential)
                        .ThenInclude(i => i.IdentityInfo)
                        .AsNoTracking()
                        .FirstOrDefault(i => i.Value == request.Username & i.UcentitiesId == (long?)GenericContactType.Phone)?.UserCredential;
                    break;
                case AuthorizeBy.Username:
                    result = await _dataLayer.TblIdentityCredentials
                        .Include(i => i.IdentityInfo)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.UserName == request.Username,
                        cancellationToken: cancellationToken);
                    break;
                case AuthorizeBy.Email:
                    request.Username.ValidateEmailAddress();
                    result = _dataLayer.TblIdentityContacts
                        .Include(i => i.UserCredential)
                        .ThenInclude(i => i.IdentityInfo)
                        .AsNoTracking()
                        .FirstOrDefault(i => i.Value == request.Username & i.UcentitiesId == 1)?.UserCredential;
                    break;
                case AuthorizeBy.Phone:
                    result = _dataLayer.TblIdentityContacts
                        .Include(i => i.UserCredential)
                        .ThenInclude(i => i.IdentityInfo)
                        .AsNoTracking()
                        .FirstOrDefault(i => i.Value == request.Username & i.UcentitiesId == 2)?.UserCredential;
                    break;
                case AuthorizeBy.Token:
                    result = await _dataLayer.TblIdentityCredentials
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

        private async Task<TblIdentityCredential> ValidatePassword(AuthenticateCredentialQuery request, AuthorizeBy authorizeBy, TblIdentityCredential credential, CancellationToken cancellationToken)
        {
            if (authorizeBy == AuthorizeBy.Token) return credential;
            
            credential = await _dataLayer.TblIdentityCredentials
                .Include(i => i.IdentityInfo)
                .FirstOrDefaultAsync(i => i.Id == credential.Id, cancellationToken: cancellationToken);
            
            var hashPassword = Encoding.ASCII.GetString(credential.PasswordByte);
            return !BCrypt.Net.BCrypt.Verify(request.Password, hashPassword) ? null : credential;
        }

        private async Task CacheQuery(TblIdentityCredential result, string token)
        {
            // Add token to caching server
            var identitySession = _cachingService.IdentitySessions.FirstOrDefault(i => i.Guid == Guid.Parse(result.IdentityInfo.Uuid));

            _cachingService.IdentitySessions.Remove(identitySession);
            _cachingService.IdentitySessions.Add(new IdentitySessionBO()
            {
                Token = token,
                LogonDateTime = DateTime.Now,
                SessionState = SessionState.Active,
                MaxSessionTimeSpan = TimeSpan.FromMinutes(30)
            });
        }
        
      
    }
}