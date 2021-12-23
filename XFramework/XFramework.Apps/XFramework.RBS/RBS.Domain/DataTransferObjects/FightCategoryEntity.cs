using System;
using System.Collections.Generic;

#nullable disable

namespace RBS.Domain.DataTransferObjects
{
    public partial class FightCategoryEntity
    {
        public FightCategoryEntity()
        {
            FightHistories = new HashSet<FightHistory>();
        }

        public long Id { get; set; }
        public string Name { get; set; }

        public virtual ICollection<FightHistory> FightHistories { get; set; }
    }
}
