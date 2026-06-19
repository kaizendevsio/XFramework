using FluentValidation;
using Wallets.Domain.Shared.Contracts.Requests;

namespace Wallets.Api.Features.Wallets.Create;

/// <summary>
/// Validator for CreateWalletRequest
/// </summary>
public class CreateWalletValidator : AbstractValidator<CreateWalletRequest>
{
    public CreateWalletValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");

        RuleFor(x => x.WalletTypeId)
            .NotEmpty().WithMessage("Wallet Type ID is required");

        RuleFor(x => x)
            .Must(x => x.Metadata.TenantId.GetValueOrDefault() != Guid.Empty ||
                       x.TenantId.GetValueOrDefault() != Guid.Empty)
            .WithMessage("Tenant ID is required");

        RuleFor(x => x)
            .Must(x =>
                !x.TenantId.HasValue ||
                x.Metadata.TenantId.GetValueOrDefault() == Guid.Empty ||
                x.TenantId.Value == x.Metadata.TenantId.GetValueOrDefault())
            .WithMessage("Tenant ID does not match request metadata");

        RuleFor(x => x.InitialBalance)
            .GreaterThanOrEqualTo(0).WithMessage("Initial balance cannot be negative");
    }
}
