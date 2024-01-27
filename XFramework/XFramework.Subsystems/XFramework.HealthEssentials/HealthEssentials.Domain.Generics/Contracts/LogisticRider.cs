using System.ComponentModel.DataAnnotations.Schema;
using XFramework.Domain.Generic.Contracts;

namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LogisticRider : BaseModel
{
    public Guid CredentialId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Remarks { get; set; }

    public short Status { get; set; }


    public string? VehicleType { get; set; }

    public string? PlateNumber { get; set; }

    public string? LicenseNumber { get; set; }

    public DateTime? LicenseExpiry { get; set; }

    public virtual ICollection<LogisticJobOrder> LogisticJobOrders { get; } = new List<LogisticJobOrder>();

    public virtual ICollection<LogisticRiderHandle> LogisticRiderHandles { get; } = new List<LogisticRiderHandle>();

    public virtual ICollection<LogisticRiderTag> LogisticRiderTags { get; } = new List<LogisticRiderTag>();
    
    [NotMapped]
    public List<StorageFile>? Files { get; set; }
    
    [NotMapped]
    public IdentityCredential? Credential { get; set; }
}