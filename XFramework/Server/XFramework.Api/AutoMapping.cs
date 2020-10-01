using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using XFramework.Core.DataAccess.Commands;
using XFramework.Core.DataAccess.Commands.Entity.User;
using XFramework.Data.BO;

namespace XFramework.Api
{
    public class AutoMapping : Profile
    {
        public AutoMapping() {
            CreateMap<UserBO, ValidateUserEmailCmd>();
        }
    }
}
