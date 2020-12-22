using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using XFramework.Core.DataAccess.Commands.Entity.User;
using XFramework.Core.Interfaces;
using XFramework.Domain.DTO;

namespace XFramework.Core.DataAccess.Commands.Handlers.User
{
    public class CreateUserHandler : CommandBaseHandler, IRequestHandler<CreateUserCmd,bool>
    {
        public CreateUserHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public async Task<bool> Handle(CreateUserCmd request, CancellationToken cancellationToken)
        {
            await _dataLayer.TblUserInfo.AddAsync(new TblUserInfo()
            {
                FirstName = request.FirstName,
                LastName =  request.LastName,
                Dob = request.Dob,
                Gender = request.Gender,
                CivilStatus = request.CivilStatus,
                Uid = Guid.NewGuid().ToString()
            });

            await _dataLayer.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}