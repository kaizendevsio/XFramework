using AutoMapper;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity;
using IdentityServer.Domain.DTO;

namespace IdentityServer.Core.AutoMapper
{
    public class AutoMapping : Profile
    {
        public AutoMapping()
        {
            CreateMap<CreateIdentityCmd, TblIdentityInfo>();
        }
    }
}