using System;

namespace IdentityServer.Domain.DataTransferObjects.Legacy;

public partial class TblAppSystem
{
    public long Id { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedOn { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? LastChanged { get; set; }
    public string Name { get; set; }
    public byte[] PublicByte { get; set; }
    public byte[] PrivateByte { get; set; }
    public bool? Iprequired { get; set; }
    public long? AppSystemType { get; set; }
    public string Uid { get; set; }
}