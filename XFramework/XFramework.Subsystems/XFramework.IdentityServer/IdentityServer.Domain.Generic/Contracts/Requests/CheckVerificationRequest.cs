﻿namespace IdentityServer.Domain.Generic.Contracts.Requests;

using TRequest = CheckVerificationRequest;
using TResponse = QueryResponse<CheckVerificationResponse>;

public record CheckVerificationRequest : RequestBase, 
    IRequest<TResponse>, 
    IStreamflowRequest<TRequest, TResponse>
{
    public Guid CredentialId { get; init; }
    public Guid VerificationTypeId { get; init; }
};
