using AutoMapper;
using XFramework.Core.DataAccess.Commands.Entity.User;
using XFramework.Domain.DTO;

namespace XFramework.Core.AutoMapper
{
    public class AutoMapping : Profile
    {
        public AutoMapping()
        {
            CreateMap<UpdateUserCmd, TblUserInfo>();
        }
    }
}
