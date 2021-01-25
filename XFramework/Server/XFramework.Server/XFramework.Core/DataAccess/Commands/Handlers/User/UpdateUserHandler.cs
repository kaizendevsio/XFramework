using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using XFramework.Core.DataAccess.Commands.Entity.User;
using XFramework.Core.Interfaces;
using XFramework.External.Models;

namespace XFramework.Core.DataAccess.Commands.Handlers.User
{
    public class UpdateUserHandler : CommandBaseHandler, IRequestHandler<UpdateUserCmd>
    {
        public UpdateUserHandler(IDataLayer dataLayer)
        {
            DataLayer = dataLayer;
        }
        
        public async Task<Unit> Handle(UpdateUserCmd request, CancellationToken cancellationToken)
        {
            var user = await DataLayer.TblUserInfo.FirstOrDefaultAsync(i => i.Id == request.UserId, cancellationToken: cancellationToken);

            if (user == null) throw new ArgumentException("User ID not found");

            user = request.Adapt(user);

            DataLayer.Update(user);
            await DataLayer.SaveChangesAsync(cancellationToken);

            return await Task.FromResult(Unit.Value);
        }
    }
}