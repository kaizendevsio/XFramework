using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using IdentityServer.Core.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity
{
    public class CheckIdentityExistenceHandler : QueryBaseHandler ,IRequestHandler<CheckIdentityExistenceQuery, QueryResponseBO<ExistenceContract>>
    {
        public CheckIdentityExistenceHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public async Task<QueryResponseBO<ExistenceContract>> Handle(CheckIdentityExistenceQuery request, CancellationToken cancellationToken)
        {

            var existing = _dataLayer.TblIdentityInformations
                .AsNoTracking()
                .Where(i => i.FirstName == request.FirstName)
                .Where(i  => i.MiddleName == request.MiddleName)
                .Where(i => i.LastName == request.LastName)
                .Where(i => i.Uuid != request.Uid.ToString())
                .Any();
            
            if (existing)
            {
                return new ()
                {
                    Message = $"The identity with name '{request.FirstName} {request.MiddleName} {request.LastName}' already exists",
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