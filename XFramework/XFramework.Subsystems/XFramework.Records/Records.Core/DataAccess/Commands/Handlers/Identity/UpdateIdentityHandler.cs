using IdentityServer.Domain.BusinessObjects;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Records.Core.DataAccess.Commands.Entity.Identity;
using Records.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Records.Core.DataAccess.Commands.Handlers.Identity
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
