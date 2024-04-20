using System;
using System.Collections.Generic;
using XFramework.Domain.Shared.Contracts.Requests;

namespace PaymentGateways.Domain.Shared.Contracts.Requests.Create;

public record CreateWithdrawalRequest : TransactionRequestBase
{
    public Guid? CredentialGuid { get; set; }
    public Guid? WalletEntityGuid { get; set; }
    public Guid? GatewayGuid { get; set; }
    public decimal? Amount { get; set; }
    public Dictionary<string, string> CustomFields { get; set; }
}