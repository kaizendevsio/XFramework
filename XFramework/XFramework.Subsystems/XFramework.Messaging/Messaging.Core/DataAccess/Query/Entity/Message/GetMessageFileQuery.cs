﻿using Messaging.Domain.Generic.Contracts.Requests.Get;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Entity.Message;

public class GetMessageFileQuery : GetMessageFileRequest, IRequest<QueryResponse<MessageFileResponse>>
{
    
}