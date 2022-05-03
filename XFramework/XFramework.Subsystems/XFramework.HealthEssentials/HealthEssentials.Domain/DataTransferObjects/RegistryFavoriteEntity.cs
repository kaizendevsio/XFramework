﻿using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects
{
    public partial class RegistryFavoriteEntity
    {
        public RegistryFavoriteEntity()
        {
            IdentityFavorites = new HashSet<IdentityFavorite>();
        }

        public long Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? IsDeleted { get; set; }
        public string Guid { get; set; } = null!;

        public virtual ICollection<IdentityFavorite> IdentityFavorites { get; set; }
    }
}
