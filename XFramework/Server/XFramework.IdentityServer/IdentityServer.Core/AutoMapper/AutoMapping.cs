using System.Collections.Generic;
using AutoMapper;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity;
using IdentityServer.Domain.Contracts;
using IdentityServer.Domain.DTO;

namespace IdentityServer.Core.AutoMapper
{
    public class AutoMapping : Profile
    {
        public AutoMapping()
        {
            CreateMap<CreateIdentityCmd, TblIdentityInfo>();
            CreateMap<TblIdentityInfo, GetIdentityContract>();
            CreateMap<List<GetApplicationListContract>, List<TblApplication>>();
        }
    }
}