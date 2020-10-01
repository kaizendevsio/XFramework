using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using XFramework.Data.BO;
using XFramework.Data.DTO;

namespace XFramework.Core.DataAccess.Commands.Entity.User
{
   public class CreateUserCmd : UserBO, IRequest<bool>
    {
        public TblApplication Application { get; set; }
    }
}
