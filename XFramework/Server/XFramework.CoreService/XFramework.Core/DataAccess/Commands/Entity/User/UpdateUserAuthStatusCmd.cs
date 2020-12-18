using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using XFramework.Domain.BO;

namespace XFramework.Core.DataAccess.Commands.Entity.User
{
    public class UpdateUserAuthStatusCmd : UserAuthStatusBO, IRequest<bool>
    {
    }
}
