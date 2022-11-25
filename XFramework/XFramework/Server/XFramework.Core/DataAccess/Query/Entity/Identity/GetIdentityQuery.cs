namespace XFramework.Core.DataAccess.Query.Entity.Identity
{
    public class GetIdentityQuery : IRequest<QueryResponse<IdentityResponse>>
    {
        public Guid Uid { get; set; }   
    }
}