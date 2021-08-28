using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.DataTransferObjects;
using Mapster;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

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
            entity.Uuid = string.IsNullOrEmpty(entity.Uuid) 
                ? Guid.NewGuid().ToString() 
                : entity.Uuid;
            entity.BirthDate = request.Dob;
            
            await _dataLayer.TblIdentityInformations.AddAsync(entity, cancellationToken);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new ()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }

    }
}