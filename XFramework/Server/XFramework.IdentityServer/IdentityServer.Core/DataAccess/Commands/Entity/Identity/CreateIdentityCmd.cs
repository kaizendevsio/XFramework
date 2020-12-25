using System;
using IdentityServer.Domain.BO;
using MediatR;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity
{
    public class CreateIdentityCmd : IRequest<CmdResponseBO<CreateIdentityCmd>>
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