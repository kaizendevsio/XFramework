﻿using System.ComponentModel.DataAnnotations;
using System.Net;

namespace XFramework.Domain.Generic.BusinessObjects;

public class QueryResponse<T>
{
    [Key]
    public Guid ResponseId { get; set; } = Guid.NewGuid();
    public HttpStatusCode HttpStatusCode { get; set; }
    public string Message { get; set; }
    public bool IsSuccess => (int)HttpStatusCode >= 200 && (int)HttpStatusCode < 300;
    public T Response { get; set; }
}