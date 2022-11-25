﻿using Messaging.Domain.Generic.Contracts.Requests.Delete;

namespace Messaging.Core.DataAccess.Commands.Entity.Message;

public class DeleteMessageReactionCmd : DeleteMessageReactionRequest, IRequest<CmdResponse<DeleteMessageReactionCmd>>
{
    
}