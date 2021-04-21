using Coins.Domain.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace Coins.Core.DataAccess.Commands.Entity
{
    public class CommandBaseEntity
    {
        public RequestServerBO RequestServer { get; set; }
    }
}