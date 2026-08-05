using FluentValidation;
using Wallets.Domain.Shared.Contracts.Requests;

namespace Wallets.Api.Features.AdvancedWallets;

public sealed class CreateDepositWorkflowValidator : AbstractValidator<CreateDepositWorkflowRequest>
{
    public CreateDepositWorkflowValidator()
    {
        RuleFor(x => x.CredentialId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.RequestedFee).GreaterThanOrEqualTo(0).When(x => x.RequestedFee.HasValue);
        RuleFor(x => x.IdempotencyKey).MaximumLength(200);
        RuleFor(x => x.ExternalReference).MaximumLength(200);
    }
}

public sealed class CreateWithdrawalWorkflowValidator : AbstractValidator<CreateWithdrawalWorkflowRequest>
{
    public CreateWithdrawalWorkflowValidator()
    {
        RuleFor(x => x.CredentialId).NotEmpty();
        RuleFor(x => x.WalletId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.RequestedFee).GreaterThanOrEqualTo(0).When(x => x.RequestedFee.HasValue);
        RuleFor(x => x.IdempotencyKey).MaximumLength(200);
        RuleFor(x => x.ExternalReference).MaximumLength(200);
    }
}

public sealed class IngestWalletPaymentWebhookValidator : AbstractValidator<IngestWalletPaymentWebhookRequest>
{
    public IngestWalletPaymentWebhookValidator()
    {
        RuleFor(x => x.ProviderKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ExternalEventId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ExternalReference).MaximumLength(200);
        RuleFor(x => x.RawPayloadJson).NotEmpty().MaximumLength(256 * 1024);
    }
}

public sealed class ResolveWalletCaseValidator : AbstractValidator<ResolveWalletCaseRequest>
{
    public ResolveWalletCaseValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).MaximumLength(200);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(4000);
    }
}

public sealed class BatchIncrementWalletValidator : AbstractValidator<BatchIncrementWalletRequest>
{
    public BatchIncrementWalletValidator()
    {
        RuleFor(x => x.Requests).NotEmpty().Must(x => x.Count <= 1000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(180);
    }
}

public sealed class BatchDecrementWalletValidator : AbstractValidator<BatchDecrementWalletRequest>
{
    public BatchDecrementWalletValidator()
    {
        RuleFor(x => x.Requests).NotEmpty().Must(x => x.Count <= 1000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(180);
    }
}

public sealed class BatchTransferWalletValidator : AbstractValidator<BatchTransferWalletRequest>
{
    public BatchTransferWalletValidator()
    {
        RuleFor(x => x.Requests).NotEmpty().Must(x => x.Count <= 1000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(180);
    }
}
