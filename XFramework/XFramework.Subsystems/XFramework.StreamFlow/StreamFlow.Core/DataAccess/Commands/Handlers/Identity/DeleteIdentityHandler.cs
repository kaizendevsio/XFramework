using MediatR;
using Microsoft.EntityFrameworkCore;
using StreamFlow.Core.DataAccess.Commands.Entity.Identity;
using StreamFlow.Core.Interfaces;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using StreamFlow.Domain.BusinessObjects;

namespace StreamFlow.Core.DataAccess.Commands.Handlers.Identity
{
    public class DeleteIdentityHandler : CommandBaseHandler, IRequestHandler<DeleteIdentityCmd, CmdResponseBO<DeleteIdentityCmd>>
    {
        public DeleteIdentityHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        public async Task<CmdResponseBO<DeleteIdentityCmd>> Handle(DeleteIdentityCmd request, CancellationToken cancellationToken)
        {
            var entity = await _dataLayer.TblIdentityInformations.FirstOrDefaultAsync(i => i.Uuid == request.Uid.ToString(), cancellationToken);

            if (entity == null)
            {
                return new CmdResponseBO<DeleteIdentityCmd>
                {
                    Message = $"Identity with UID {request.Uid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            if (_dataLayer.TblIdentityCredentials.Any(i => i.IdentityInfoId == entity.Id))
            {
                return new CmdResponseBO<DeleteIdentityCmd>
                {
                    Message = $"Identity with UID {request.Uid} has existing credentials and cannot be deleted",
                    HttpStatusCode = HttpStatusCode.Forbidden
                };
            }

            _dataLayer.Remove(entity);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new();

        }
    }
}
