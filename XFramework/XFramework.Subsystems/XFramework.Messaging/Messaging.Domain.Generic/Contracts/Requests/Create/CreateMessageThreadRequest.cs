﻿using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Create;

public record CreateMessageThreadRequest : RequestBase
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid? EntityGuid { get; set; }
}