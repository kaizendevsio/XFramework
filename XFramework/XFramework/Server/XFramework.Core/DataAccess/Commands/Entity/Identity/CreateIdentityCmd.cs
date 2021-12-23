using System;
using IdentityServer.Domain.Generic.Enums;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Enums;

namespace XFramework.Core.DataAccess.Commands.Entity.Identity
{
    public class CreateIdentityCmd : CommandBaseEntity ,IRequest<CmdResponseBO>
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime Dob { get; set; }
        public Gender Gender { get; set; }
        public CivilStatus CivilStatus { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }

        public string UserAlias { get; set; }
        public string UserName { get; set; }
        public string PasswordString { get; set; }
        public int RoleEntity { get; set; }
    }
}