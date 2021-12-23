#nullable disable

namespace Wallets.Domain.DataTransferObjects
{
    public partial class TblLog
    {
        public long Id { get; set; }
        public long? ApplicationId { get; set; }
        public short? Severity { get; set; }
        public string Message { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public string Initiator { get; set; }
        public short? Type { get; set; }
        public string Name { get; set; }
        public bool? Seen { get; set; }
        public string Uuid { get; set; }

        public virtual TblApplication Application { get; set; }
    }
}
