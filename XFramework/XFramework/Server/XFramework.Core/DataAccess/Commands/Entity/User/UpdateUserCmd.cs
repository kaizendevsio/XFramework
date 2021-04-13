using System;
using MediatR;
using XFramework.Domain.Generic.Enums;

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