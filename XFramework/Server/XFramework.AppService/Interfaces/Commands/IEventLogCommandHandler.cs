using System.Threading.Tasks;
using XFramework.Core.DataAccess.Commands.Entity.Events;

namespace XFramework.Core.Interfaces.Commands
{
    public interface IEventLogCommandHandler
    {
        public Task<bool> HandleAsync(CreateEventLogCmd command);
    }
}
