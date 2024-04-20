﻿namespace IdentityServer.Domain.Shared.Contracts.Requests;

public record CreateAffiliateSubscriptionRequest : RequestBase
{
    public Guid? SubscriptionEntityId { get; set; }
    public string? Value { get; set; }
    public string? VerificationToken { get; set; }
}