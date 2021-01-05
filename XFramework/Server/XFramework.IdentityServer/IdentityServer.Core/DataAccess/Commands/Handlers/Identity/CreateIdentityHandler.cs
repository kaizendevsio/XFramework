using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.BusinessObject;
using IdentityServer.Domain.DataTableObject;
using MediatR;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity
{
    public class CreateIdentityHandler : CommandBaseHandler, IRequestHandler<CreateIdentityCmd,CmdResponseBO<CreateIdentityCmd>>
    {

        public CreateIdentityHandler(IDataLayer dataLayer, IMapper mapper)
        {
            _dataLayer = dataLayer;
            _mapper = mapper;
        }
        public async Task<CmdResponseBO<CreateIdentityCmd>> Handle(CreateIdentityCmd request, CancellationToken cancellationToken)
        {
            var entity = _mapper.Map<TblIdentityInfo>(request);
            entity.Uid = Guid.NewGuid().ToString();
            
            await _dataLayer.TblIdentityInfos.AddAsync(entity, cancellationToken);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new();
        }
    }
}