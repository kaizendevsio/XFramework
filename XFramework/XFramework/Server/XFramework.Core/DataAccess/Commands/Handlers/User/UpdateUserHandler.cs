using System;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using XFramework.Core.DataAccess.Commands.Entity.User;
using XFramework.Core.Interfaces;
using XFramework.Domain.Generic.BusinessObjects;

namespace XFramework.Core.DataAccess.Commands.Handlers.User
{
    public class UpdateUserHandler : CommandBaseHandler, IRequestHandler<UpdateUserCmd,CmdResponseBO<UpdateUserCmd>>
    {
        public UpdateUserHandler(IDataLayer dataLayer)
        {
            DataLayer = dataLayer;
        }
        
        public async Task<CmdResponseBO<UpdateUserCmd>> Handle(UpdateUserCmd request, CancellationToken cancellationToken)
        {
            var user = await DataLayer.TblUserInfo.FirstOrDefaultAsync(i => i.Id == request.UserId, cancellationToken: cancellationToken);

            if (user == null) throw new ArgumentException("User ID not found");

            user = request.Adapt(user);
            DataLayer.Update(user);
            await DataLayer.SaveChangesAsync(cancellationToken);

            return new()
            {
                
            };
        }
    }
}