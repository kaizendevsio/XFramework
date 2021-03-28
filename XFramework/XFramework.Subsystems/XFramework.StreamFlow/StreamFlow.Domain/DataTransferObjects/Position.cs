using System;
using System.Collections.Generic;

#nullable disable

namespace StreamFlow.Domain.DataTransferObjects
{
    public partial class Position
    {
        public Position()
        {
            Players = new HashSet<Player>();
        }

        public long PositionId { get; set; }
        public string PositionName { get; set; }

        public virtual ICollection<Player> Players { get; set; }
    }
}
