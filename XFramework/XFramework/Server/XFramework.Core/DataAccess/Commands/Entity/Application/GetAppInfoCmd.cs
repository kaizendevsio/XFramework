using MediatR;
using XFramework.Domain.DataTransferObjects;

namespace XFramework.Core.DataAccess.Commands.Entity.Application
{
   public class GetAppInfoCmd : TblApplication, IRequest<bool>
    {

    }
}
