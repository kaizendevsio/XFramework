using System.ComponentModel.DataAnnotations.Schema;

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

    public virtual ICollection<LogisticJobOrder> LogisticJobOrders { get; set; } = new List<LogisticJobOrder>();

    public virtual ICollection<LogisticRiderHandle> LogisticRiderHandles { get; set; } = new List<LogisticRiderHandle>();

    public virtual ICollection<LogisticRiderTag> LogisticRiderTags { get; set; } = new List<LogisticRiderTag>();
    
    [NotMapped]
    public List<StorageFile>? Files { get; set; }
    
    [NotMapped]
    public IdentityCredential? Credential { get; set; }
}