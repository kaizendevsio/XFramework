﻿namespace XFramework.Domain.Generic.BusinessObjects;

public class RefreshToken
{
    public string Token { get; set; }
    public DateTime ExpireAt { get; set; }
    public Guid Cuid { get; set; }
}