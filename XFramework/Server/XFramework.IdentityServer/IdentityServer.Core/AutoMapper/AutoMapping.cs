using System.Collections.Generic;
using AutoMapper;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity;
using IdentityServer.Domain.Contracts;
using IdentityServer.Domain.DataTableObject;

namespace IdentityServer.Core.AutoMapper
{
    public class AutoMapping : Profile
    {
        public AutoMapping()
        {
            CreateMap<CreateIdentityCmd, TblIdentityInfo>();
            CreateMap<TblIdentityInfo, GetIdentityContract>();
            CreateMap<TblApplication,GetApplicationListContract>();
        }
    }
}