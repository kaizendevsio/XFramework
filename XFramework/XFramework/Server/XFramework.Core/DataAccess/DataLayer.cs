using XFramework.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.DataTransferObjects;

namespace XFramework.Core.DataAccess
{
    public class DataLayer : XnelSystemsContext, IDataLayer
    {
        public DataLayer(DbContextOptions<XnelSystemsContext> options)
           : base(options)
        {
        }
        
    }
}
