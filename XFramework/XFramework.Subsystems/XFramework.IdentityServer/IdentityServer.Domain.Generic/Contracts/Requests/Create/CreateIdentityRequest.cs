using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Create;

public class CreateIdentityRequest : TransactionRequestBase
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
        
    public Guid? Guid { get; set; }
}