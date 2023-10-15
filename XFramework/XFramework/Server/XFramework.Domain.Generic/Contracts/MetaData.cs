﻿namespace XFramework.Domain.Generic.Contracts;

public partial record MetaData : BaseModel
{
    public Guid TypeId { get; set; }

    public Guid KeyId { get; set; }

    public string? Value { get; set; }

    public virtual MetaDataType Type { get; set; } = null!;
}