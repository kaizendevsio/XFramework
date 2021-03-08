using System;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.BusinessObjects;
using IdentityServer.Domain.DataTableObjects;
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
            var entity = request.Adapt<TblIdentityInformation>();
            entity.Uuid = Guid.NewGuid().ToString();
            
            await _dataLayer.TblIdentityInformations.AddAsync(entity, cancellationToken);
            await _dataLayer.SaveChangesAsync(cancellationToken);

           return new();
        }
    }
}