using AutoMapper;
using IdentityServer.Core.Interfaces;

namespace IdentityServer.Core.DataAccess.Commands.Handlers
{
    public class CommandBaseHandler
    {
        public IDataLayer _dataLayer;
        public IMapper _mapper;

    }
}
