using System;
using System.Collections.Generic;
using System.Text;
using XFramework.Core.DataAccess.Query.Entity.User;

namespace XFramework.Core.Interfaces.Query
{
    public interface IUserQueryHandler
    {
        public bool Handle(AuthenticateUserQuery query);
        public bool Handle(GetUserProfileQuery query);
    }
}
