#nullable disable

namespace RBS.Domain.DataTransferObjects
{
    public partial class Salary
    {
        public long SalaryId { get; set; }
        public long? PlayerId { get; set; }
        public decimal? Amount { get; set; }

        public virtual Player Player { get; set; }
    }
}
