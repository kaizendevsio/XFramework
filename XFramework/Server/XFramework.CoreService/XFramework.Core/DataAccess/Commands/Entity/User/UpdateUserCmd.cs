using System;
using System.Runtime.Intrinsics.X86;
using MediatR;
using XFramework.Domain.Enums;

namespace XFramework.Core.DataAccess.Commands.Entity.User
{
    public class UpdateUserCmd : IRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime Dob { get; set; }
        public Gender Gender { get; set; }
        public CivilStatus CivilStatus { get; set; }
        public long UserId { get; set; }
    }
}