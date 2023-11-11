namespace XFramework.Domain.Generic.Contracts;

public partial class IdentityContactType : BaseModel
{
    public string? Name { get; set; }


    public virtual ICollection<IdentityContact> IdentityContacts { get; } = new List<IdentityContact>();
}