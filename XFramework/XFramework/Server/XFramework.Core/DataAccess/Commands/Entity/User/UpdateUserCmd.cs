using System;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Enums;

namespace XFramework.Core.DataAccess.Commands.Entity.User
{
    public class UpdateUserCmd : CommandBaseEntity, IRequest<CmdResponseBO<UpdateUserCmd>>
    {
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string IdentityName { get; set; }
        public string IdentityDescription { get; set; }
        public DateTime Dob { get; set; }
        public short Gender { get; set; }
        public bool IsVerified { get; set; }
        public short CivilStatus { get; set; }
    }
}