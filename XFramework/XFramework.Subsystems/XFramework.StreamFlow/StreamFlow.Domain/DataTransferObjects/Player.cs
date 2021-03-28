using System;
using System.Collections.Generic;

#nullable disable

namespace StreamFlow.Domain.DataTransferObjects
{
    public partial class Player
    {
        public Player()
        {
            Salaries = new HashSet<Salary>();
            ScoredGoals = new HashSet<ScoredGoal>();
        }

        public long PlayerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public long? PositionId { get; set; }

        public virtual Position Position { get; set; }
        public virtual ICollection<Salary> Salaries { get; set; }
        public virtual ICollection<ScoredGoal> ScoredGoals { get; set; }
    }
}
