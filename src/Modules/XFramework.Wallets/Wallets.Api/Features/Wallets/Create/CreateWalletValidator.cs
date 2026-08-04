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

        RuleFor(x => x.InitialBalance)
            .GreaterThanOrEqualTo(0).WithMessage("Initial balance cannot be negative");
    }
}
