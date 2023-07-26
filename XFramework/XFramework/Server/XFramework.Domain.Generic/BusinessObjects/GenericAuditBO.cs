using System;

namespace XFramework.Domain.Generic.BusinessObjects;

public class GenericAuditBO
{
    public bool Active { get; set; }
    public bool Enabled { get; set; }
    public Guid Guid { get; set; }
}