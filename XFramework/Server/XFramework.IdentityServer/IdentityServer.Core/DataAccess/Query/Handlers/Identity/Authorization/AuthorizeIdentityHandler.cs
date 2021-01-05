using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Authorization;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.BusinessObject;
using IdentityServer.Domain.Contracts;
using IdentityServer.Domain.DataTableObject;
using IdentityServer.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Authorization
{
    public class AuthorizeIdentityHandler : QueryBaseHandler, IRequestHandler<AuthorizeIdentityQuery, QueryResponseBO<bool>>
    {
        public AuthorizeIdentityHandler(IDataLayer dataLayer, IMapper mapper, ICachingService cachingService)
        {
            _dataLayer = dataLayer;
            _mapper = mapper;
            _cachingService = cachingService;
        }
        public async Task<QueryResponseBO<bool>> Handle(AuthorizeIdentityQuery request, CancellationToken cancellationToken)
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
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            // Check if identity credential exists
            
            if (result == null)
            {
                return new QueryResponseBO<bool>()
                {
                    Message = $"Identity does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            
            SHA512 shaM = new SHA512Managed();
            var passwordByte = Encoding.ASCII.GetBytes(request.Password);
            var hashPasswordByte = shaM.ComputeHash(passwordByte);
            
            result = await _dataLayer.TblIdentityCredentials
                .FirstOrDefaultAsync(i => i.UserName == request.Username & i.PasswordByte == hashPasswordByte
                , cancellationToken: cancellationToken);

            if (result == null)
            {
                return new QueryResponseBO<bool>()
                {
                    Message = $"Identity Authentication Failed",
                    HttpStatusCode = HttpStatusCode.BadRequest
                };
            }
            
            return new QueryResponseBO<bool>()
            {
                Message = $"Identity Authorized",
                HttpStatusCode = HttpStatusCode.Accepted
            };
            
        }
    }
}