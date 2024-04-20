﻿using System;
using System.Collections.Generic;

#nullable disable

namespace Records.Domain.DataTransferObjects
{
    public partial class TblSessionEntity
    {
        public TblSessionEntity()
        {
            TblSessionData = new HashSet<TblSessionDatum>();
        }

        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; }

        public virtual ICollection<TblSessionDatum> TblSessionData { get; set; }
    }
}
