using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Credential
{
    public class ChangePasswordCmd : CommandBaseEntity, IRequest<CmdResponseBO<ChangePasswordCmd>>
    {
        public string Cuid { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string UserName { get; set; }
        public string PasswordString { get; set; }
        
    }
}