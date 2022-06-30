using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class SubscriptionEntity
    {
        public SubscriptionEntity()
        {
            Subscriptions = new HashSet<Subscription>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Guid { get; set; }

        public virtual ICollection<Subscription> Subscriptions { get; set; }
    }
}
