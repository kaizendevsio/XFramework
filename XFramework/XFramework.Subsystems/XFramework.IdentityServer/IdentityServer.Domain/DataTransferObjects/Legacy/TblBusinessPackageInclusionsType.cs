using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy;

public partial class TblBusinessPackageInclusionsType
{
    public TblBusinessPackageInclusionsType()
    {
        TblBusinessPackageInclusions = new HashSet<TblBusinessPackageInclusions>();
    }

    public long Id { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedOn { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? LastChanged { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool? IsNumericValue { get; set; }
    public string Code { get; set; }
    public string IconImage { get; set; }
    public string Unit { get; set; }

    public virtual ICollection<TblBusinessPackageInclusions> TblBusinessPackageInclusions { get; set; }
}