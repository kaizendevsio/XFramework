﻿namespace HealthEssentials.Domain.Generics.Contracts.Responses.Logistic;

public class LogisticRiderHandleResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long LogisticId { get; set; }
    public long LogisticRiderId { get; set; }
    public short Status { get; set; }
    public string Guid { get; set; } = null!;

}