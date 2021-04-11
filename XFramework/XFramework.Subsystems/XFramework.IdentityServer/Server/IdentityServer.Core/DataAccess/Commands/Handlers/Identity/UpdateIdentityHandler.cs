﻿using IdentityServer.Core.DataAccess.Commands.Entity.Identity;
using IdentityServer.Core.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity
{
    public class UpdateIdentityHandler : CommandBaseHandler, IRequestHandler<UpdateIdentityCmd, CmdResponseBO<UpdateIdentityCmd>>
    {
        public UpdateIdentityHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        public async Task<CmdResponseBO<UpdateIdentityCmd>> Handle(UpdateIdentityCmd request, CancellationToken cancellationToken)
        {
            var entity = await _dataLayer.TblIdentityInformations.FirstOrDefaultAsync(i => i.Uuid == request.Uuid.ToString(), cancellationToken);

            if (entity == null)
            {
                return new CmdResponseBO<UpdateIdentityCmd>
                {
                    Message = $"Identity with UID {request.Uuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            entity = request.Adapt(entity);
            _dataLayer.Update(entity);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new();

        }


    }
}
