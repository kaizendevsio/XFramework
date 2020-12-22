using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Query.Entity.Application;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.DTO;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Application
{
    public class AppsInfoQueryHandler : QueryBaseHandler, IRequestHandler<AppsListQuery, List<TblApplication>>
    {
        public AppsInfoQueryHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        public async Task<List<TblApplication>> Handle(AppsListQuery request, CancellationToken cancellationToken)
        {
            return await _dataLayer.TblApplications.ToListAsync(cancellationToken: cancellationToken);
        }
    }
}