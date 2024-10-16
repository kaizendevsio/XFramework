﻿namespace XFramework.Domain.Shared.Contracts.Base;

public interface IAuditable
{
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}