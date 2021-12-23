using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.DataTransferObjects.Legacy;
using MediatR;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Contacts
{
    public class CheckContactExistenceHandler : QueryBaseHandler ,IRequestHandler<CheckContactExistenceQuery, QueryResponseBO<ExistenceContract>>
    {
        private readonly LegacyContext _legacyContext;

        public CheckContactExistenceHandler(IDataLayer dataLayer, LegacyContext legacyContext)
        {
            _legacyContext = legacyContext;
            _dataLayer = dataLayer;
        }
        
        public async Task<QueryResponseBO<ExistenceContract>> Handle(CheckContactExistenceQuery request, CancellationToken cancellationToken)
        {
            var existing = await _dataLayer.TblIdentityContacts
                .AsNoTracking()
                .Where(i => i.Value == request.Value)
                .Where(i => i.Id != request.Cid)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing != null)
            {
                return new ()
                {
                    Message = $"The contact type {(GenericContactType)existing.UcentitiesId} '{request.Value}' already exists",
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