using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Authorization;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.BusinessObjects;
using IdentityServer.Domain.Contracts;
using IdentityServer.Domain.DataTableObjects;
using IdentityServer.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Authorization
{
    public class AuthenticateIdentityHandler : QueryBaseHandler, IRequestHandler<AuthenticateIdentityQuery, QueryResponseBO<AuthorizeIdentityContract>>
    {
        public AuthenticateIdentityHandler(IDataLayer dataLayer, ICachingService cachingService, IHelperService helperService)
        {
            _helperService = helperService;
            _dataLayer = dataLayer;
            _cachingService = cachingService;
        }
        public async Task<QueryResponseBO<AuthorizeIdentityContract>> Handle(AuthenticateIdentityQuery request, CancellationToken cancellationToken)
        {
            TblIdentityCredential result = null;
            var authorizeBy = request.AuthorizeBy;
            
            reAuth:
            switch (authorizeBy)
            {
                case AuthorizeBy.Default:
                    var getDefaults = await _dataLayer.TblConfigurations.FirstOrDefaultAsync(i =>
                        i.ApplicationId == request.RequestServer.ApplicationId & i.Key == "DefaultAuthorizeBy", cancellationToken: cancellationToken);
                    authorizeBy = (AuthorizeBy)int.Parse(getDefaults.Value);
                    goto reAuth;
                    
                    break;
                case AuthorizeBy.UsernameEmailPhone:
                    result = await _dataLayer.TblIdentityCredentials.FirstOrDefaultAsync(i => i.UserName == request.Username, cancellationToken: cancellationToken);
                    result ??= _dataLayer.TblIdentityContacts.FirstOrDefault(i => i.Value == request.Username & i.UcentitiesId == 1)?.UserCredential;
                    result ??= _dataLayer.TblIdentityContacts.FirstOrDefault(i => i.Value == request.Username & i.UcentitiesId == 2)?.UserCredential;
                    
                    break;
                case AuthorizeBy.Username:
                    result = await _dataLayer.TblIdentityCredentials.FirstOrDefaultAsync(i => i.UserName == request.Username, cancellationToken: cancellationToken);
                    break;
                case AuthorizeBy.Email:
                    result = _dataLayer.TblIdentityContacts.FirstOrDefault(i => i.Value == request.Username & i.UcentitiesId == 1)?.UserCredential;
                    break;
                case AuthorizeBy.Phone:
                    result = _dataLayer.TblIdentityContacts.FirstOrDefault(i => i.Value == request.Username & i.UcentitiesId == 2)?.UserCredential;
                    break;
                case AuthorizeBy.Token:
                    result = await _dataLayer.TblIdentityCredentials.FirstOrDefaultAsync(i => i.UserName == request.Username, cancellationToken: cancellationToken);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            // Check if identity credential exists
            
            if (result == null)
            {
                return new QueryResponseBO<AuthorizeIdentityContract>()
                {
                    Message = $"Identity does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            
            SHA512 shaM = new SHA512Managed();
            var passwordByte = Encoding.ASCII.GetBytes(request.Password);
            var hashPasswordByte = shaM.ComputeHash(passwordByte);
            var token = _helperService.GenerateToken();

            if (authorizeBy != AuthorizeBy.Token)
            {
                result = await _dataLayer.TblIdentityCredentials
                    .Include(i => i.IdentityInfo)
                    .FirstOrDefaultAsync(i => i.Id == result.Id & i.PasswordByte == hashPasswordByte, cancellationToken: cancellationToken);
            }

            if (result == null)
            {
                return new QueryResponseBO<AuthorizeIdentityContract>()
                {
                    Message = $"Identity Authentication Failed",
                    HttpStatusCode = HttpStatusCode.BadRequest
                };
            }

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
            
            return new QueryResponseBO<AuthorizeIdentityContract>()
            {
                Message = $"Identity Authorized",
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = new AuthorizeIdentityContract()
                {
                    Token = token
                }
            };
            
        }
    }
}