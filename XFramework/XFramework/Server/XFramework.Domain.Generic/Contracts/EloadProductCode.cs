﻿namespace XFramework.Domain.Generic.Contracts;

public partial class EloadProductCode : BaseModel
{
    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? Validity { get; set; }

    public string? Description { get; set; }


    public Guid? TelcoTypeId { get; set; }

    public Guid? EloadPromoId { get; set; }

    public decimal Amount { get; set; }


    public virtual ICollection<EloadTransaction> EloadTransactions { get; } = new List<EloadTransaction>();

    public virtual EloadPromo? EloadPromo { get; set; }

    public virtual EloadTelcoType? EloadTelcoType { get; set; }
}