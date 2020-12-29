using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.BO;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity
{
    public class DeleteIdentityHandler : CommandBaseHandler ,IRequestHandler<DeleteIdentityCmd, CmdResponseBO<DeleteIdentityCmd>>
    {
        public DeleteIdentityHandler(IDataLayer dataLayer, IMapper mapper)
        {
            _dataLayer = dataLayer;
            _mapper = mapper;
        }
        public async Task<CmdResponseBO<DeleteIdentityCmd>> Handle(DeleteIdentityCmd request, CancellationToken cancellationToken)
        {
            var entity = await _dataLayer.TblIdentityInfos.FirstOrDefaultAsync(i => i.Uid == request.Uid.ToString(), cancellationToken);

            if (entity == null)
            {
                return new CmdResponseBO<DeleteIdentityCmd>
                {
                    Message = $"Identity with UID {request.Uid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            _dataLayer.Remove(entity);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new();

        }
    }
}
