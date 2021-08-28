using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential;
using IdentityServer.Core.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Credential
{
    public class CheckCredentialExistenceHandler : QueryBaseHandler ,IRequestHandler<CheckCredentialExistenceQuery, QueryResponseBO<ExistenceContract>>
    {
        public CheckCredentialExistenceHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        public async Task<QueryResponseBO<ExistenceContract>> Handle(CheckCredentialExistenceQuery request, CancellationToken cancellationToken)
        {
            var existing = _dataLayer.TblIdentityCredentials
                .AsNoTracking()
                .Where(i => i.UserName == request.UserName)
                .Where(i => i.Cuid != request.Cuid.ToString())
                .Any();
            
            if (existing)
            {
                return new ()
                {
                    Message = $"The identity with username '{request.UserName}' already exists",
                    HttpStatusCode = HttpStatusCode.Conflict
                };
            }

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
    }
}