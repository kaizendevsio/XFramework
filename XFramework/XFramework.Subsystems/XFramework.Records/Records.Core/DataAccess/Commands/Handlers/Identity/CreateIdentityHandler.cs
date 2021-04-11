using Mapster;
using MediatR;
using Records.Core.DataAccess.Commands.Entity.Identity;
using Records.Core.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using Records.Domain.BusinessObjects;
using Records.Domain.DataTransferObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace Records.Core.DataAccess.Commands.Handlers.Identity
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

            await _dataLayer.TblIdentityInformations.AddAsync(entity, cancellationToken);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new();
        }

    }
}