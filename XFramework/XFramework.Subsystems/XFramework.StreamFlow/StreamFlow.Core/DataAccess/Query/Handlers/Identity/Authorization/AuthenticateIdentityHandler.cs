using IdentityServer.Domain.BusinessObjects;
using IdentityServer.Domain.Contracts;
using IdentityServer.Domain.DataTransferObjects;
using IdentityServer.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StreamFlow.Core.DataAccess.Query.Entity.Identity.Authorization;
using StreamFlow.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamFlow.Core.DataAccess.Query.Handlers.Identity.Authorization
{
    public class AuthenticateIdentityHandler : QueryBaseHandler,
        IRequestHandler<AuthenticateIdentityQuery, QueryResponseBO<AuthorizeIdentityContract>>
    {
        public AuthenticateIdentityHandler(IDataLayer dataLayer, ICachingService cachingService, IHelperService helperService, JwtOptionsBO jwtOptionsBo, IJwtService jwtService)
        {
            _jwtService = jwtService;
            _jwtOptions = jwtOptionsBo;
            _helperService = helperService;
            _dataLayer = dataLayer;
            _cachingService = cachingService;
        }

        public async Task<QueryResponseBO<AuthorizeIdentityContract>> Handle(AuthenticateIdentityQuery request, CancellationToken cancellationToken)
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

            credential = await ValidatePassword(request, request.AuthorizeBy, credential, cancellationToken);
            if (credential == null)
            {
                return new()
                {
                    Message = $"Identity Authentication Failed",
                    HttpStatusCode = HttpStatusCode.BadRequest
                };
            }

            var token = await _jwtService.GenerateToken(request.Username, new Guid(credential.Cuid));
            return new()
            {
                Message = $"Identity Authorized",
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = new()
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken
                }
            };
        }

        private async Task<TblIdentityCredential> ValidateAuthorization(AuthenticateIdentityQuery request, CancellationToken cancellationToken, AuthorizeBy authorizeBy)
        {
            TblIdentityCredential result;

        reAuth:
            switch (authorizeBy)
            {
                case AuthorizeBy.Default:
                    var getDefaults = await _dataLayer.TblConfigurations
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.ApplicationId == request.RequestServer.ApplicationId & i.Key == "DefaultAuthorizeBy", cancellationToken: cancellationToken);
                    authorizeBy = (AuthorizeBy)int.Parse(getDefaults.Value);
                    goto reAuth;
                case AuthorizeBy.UsernameEmailPhone:
                    result = await _dataLayer.TblIdentityCredentials
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.UserName == request.Username,
                        cancellationToken: cancellationToken);
                    result ??= _dataLayer.TblIdentityContacts
                        .AsNoTracking()
                        .FirstOrDefault(i => i.Value == request.Username & i.UcentitiesId == 1)?.UserCredential;
                    result ??= _dataLayer.TblIdentityContacts
                        .FirstOrDefault(i => i.Value == request.Username & i.UcentitiesId == 2)?.UserCredential;
                    break;
                case AuthorizeBy.Username:
                    result = await _dataLayer.TblIdentityCredentials
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.UserName == request.Username,
                        cancellationToken: cancellationToken);
                    break;
                case AuthorizeBy.Email:
                    result = _dataLayer.TblIdentityContacts
                        .AsNoTracking()
                        .FirstOrDefault(i => i.Value == request.Username & i.UcentitiesId == 1)?.UserCredential;
                    break;
                case AuthorizeBy.Phone:
                    result = _dataLayer.TblIdentityContacts
                        .AsNoTracking()
                        .FirstOrDefault(i => i.Value == request.Username & i.UcentitiesId == 2)?.UserCredential;
                    break;
                case AuthorizeBy.Token:
                    result = await _dataLayer.TblIdentityCredentials
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.UserName == request.Username,
                        cancellationToken: cancellationToken);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return result;
        }

        private async Task<TblIdentityCredential> ValidatePassword(AuthenticateIdentityQuery request, AuthorizeBy authorizeBy, TblIdentityCredential credential, CancellationToken cancellationToken)
        {
            SHA512 shaM = new SHA512Managed();
            var passwordByte = Encoding.ASCII.GetBytes(request.Password);
            var hashPasswordByte = shaM.ComputeHash(passwordByte);

            if (authorizeBy == AuthorizeBy.Token)
                return credential;

            credential = await _dataLayer.TblIdentityCredentials
                .Include(i => i.IdentityInfo)
                .FirstOrDefaultAsync(i => i.Id == credential.Id & i.PasswordByte == hashPasswordByte, cancellationToken: cancellationToken);
            return credential;
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