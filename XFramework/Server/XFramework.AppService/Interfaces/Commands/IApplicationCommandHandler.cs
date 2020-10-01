using System.Threading.Tasks;
using XFramework.Core.DataAccess.Commands.Entity.Application;
using XFramework.Data.DTO;

namespace XFramework.Core.Interfaces.Commands
{
    public interface IApplicationCommandHandler
    {
        public Task<TblApplication> HandleAsync(GetAppInfoCmd command);
    }
}
