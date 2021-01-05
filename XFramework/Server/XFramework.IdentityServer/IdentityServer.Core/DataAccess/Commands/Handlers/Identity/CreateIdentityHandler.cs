using System;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.BusinessObject;
using IdentityServer.Domain.DataTableObject;
using Mapster;
using MediatR;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity
{
    public class CreateIdentityHandler : CommandBaseHandler, IRequestHandler<CreateIdentityCmd,CmdResponseBO<CreateIdentityCmd>>
    {

        public CreateIdentityHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        public async Task<CmdResponseBO<CreateIdentityCmd>> Handle(CreateIdentityCmd request, CancellationToken cancellationToken)
        {
            var entity = request.Adapt<TblIdentityInfo>();
            entity.Uid = Guid.NewGuid().ToString();
            
            await _dataLayer.TblIdentityInfos.AddAsync(entity, cancellationToken);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new();
        }
    }
}