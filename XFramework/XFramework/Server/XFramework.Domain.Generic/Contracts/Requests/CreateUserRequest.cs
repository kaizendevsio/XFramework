using System;
using XFramework.Domain.Generic.Enums;

namespace XFramework.Domain.Generic.Contracts.Requests
{
    public class CreateUserRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime Dob { get; set; }
        public Gender Gender { get; set; }
        public CivilStatus CivilStatus { get; set; }

        public string UserAlias { get; set; }
        public string UserName { get; set; }
        public string PasswordString { get; set; }
    }
}