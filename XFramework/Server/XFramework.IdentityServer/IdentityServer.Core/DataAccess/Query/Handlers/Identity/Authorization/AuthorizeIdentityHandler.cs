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
using IdentityServer.Domain.BO;
using IdentityServer.Domain.Contracts;
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
            // Check if username exists
            var result = await _dataLayer.TblIdentityCredentials.FirstOrDefaultAsync(i => i.UserName == request.Username, cancellationToken: cancellationToken);
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