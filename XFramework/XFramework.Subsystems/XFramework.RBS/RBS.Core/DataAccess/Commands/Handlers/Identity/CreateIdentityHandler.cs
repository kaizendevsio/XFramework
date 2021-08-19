using Mapster;
using MediatR;
using RBS.Core.DataAccess.Commands.Entity.Identity;
using RBS.Core.Interfaces;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using RBS.Domain.BusinessObjects;
using RBS.Domain.DataTransferObjects;

namespace RBS.Core.DataAccess.Commands.Handlers.Identity
{
    public class CreateIdentityHandler : CommandBaseHandler, IRequestHandler<CreateIdentityCmd, CmdResponseBO<CreateIdentityCmd>>
    {
        public CreateIdentityHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        public async Task<CmdResponseBO<CreateIdentityCmd>> Handle(CreateIdentityCmd request, CancellationToken cancellationToken)
        {
            var entity = request.Adapt<TblIdentityInformation>();
            entity.Uuid = Guid.NewGuid().ToString();
            request.Uid = Guid.Parse(entity.Uuid);
            await _dataLayer.TblIdentityInformations.AddAsync(entity, cancellationToken);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new();
        }

    }
}