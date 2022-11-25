namespace XFramework.Core.DataAccess.Query.Entity.Identity
{
   public class AuthenticateIdentityQuery : QueryBaseEntity, IRequest<QueryResponse<AuthorizeIdentityResponse>>
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool Remember { get; set; }
        public AuthorizeBy AuthorizeBy { get; set; }
    }
}
