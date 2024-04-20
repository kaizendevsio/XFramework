﻿using System;
using System.Collections.Generic;

#nullable disable

namespace Records.Domain.DataTransferObjects
{
    public partial class ScoredGoal
    {
        public long GoalId { get; set; }
        public long? PlayerId { get; set; }
        public long? GameId { get; set; }
        public int? Minute { get; set; }

        public virtual Player Player { get; set; }
    }
}
