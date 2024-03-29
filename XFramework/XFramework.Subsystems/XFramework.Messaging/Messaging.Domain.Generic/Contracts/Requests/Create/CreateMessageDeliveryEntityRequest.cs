﻿using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Create;

public record CreateMessageDeliveryEntityRequest : RequestBase
{
    public string? Name { get; set; }
}