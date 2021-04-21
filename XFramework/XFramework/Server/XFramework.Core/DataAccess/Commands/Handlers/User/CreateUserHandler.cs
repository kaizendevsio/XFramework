using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using XFramework.Core.DataAccess.Commands.Entity.User;
using XFramework.Core.Interfaces;
using XFramework.Domain.DataTransferObjects;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Core.DataAccess.Commands.Handlers.User
{
    public class CreateUserHandler : CommandBaseHandler, IRequestHandler<CreateUserCmd,CmdResponseBO<CreateUserCmd>>
    {
        public CreateUserHandler(IDataLayer dataLayer, IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
            DataLayer = dataLayer;
        }
        
        public async Task<CmdResponseBO<CreateUserCmd>> Handle(CreateUserCmd request, CancellationToken cancellationToken)
        {
            await IdentityServiceWrapper.CreateIdentity();
            
            
            await DataLayer.TblUserInfo.AddAsync(new TblUserInfo()
            {
                FirstName = request.FirstName,
                LastName =  request.LastName,
                Dob = request.Dob,
                Gender = request.Gender,
                CivilStatus = request.CivilStatus,
                Uid = Guid.NewGuid().ToString()
            }, cancellationToken);

            await DataLayer.SaveChangesAsync(cancellationToken);
            return new();
        }
    }
}