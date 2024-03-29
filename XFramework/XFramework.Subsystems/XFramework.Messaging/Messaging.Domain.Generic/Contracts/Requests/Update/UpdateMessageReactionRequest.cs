﻿using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Update;

public record UpdateMessageReactionRequest : RequestBase
{
    public Guid? MessageGuid { get; set; }
    public Guid? EntityGuid { get; set; }
}