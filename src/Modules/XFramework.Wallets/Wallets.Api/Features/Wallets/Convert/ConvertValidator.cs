using FluentValidation;
using Wallets.Domain.Shared.Contracts.Requests;

namespace Wallets.Api.Features.Wallets.Convert;

/// <summary>
/// Validator for ConvertWalletRequest
/// </summary>
public class ConvertValidator : AbstractValidator<ConvertWalletRequest>
{
    public ConvertValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");

        RuleFor(x => x.SourceWalletTypeId)
            .NotEmpty().WithMessage("Source Wallet Type ID is required");

        RuleFor(x => x.TargetWalletTypeId)
            .NotEmpty().WithMessage("Target Wallet Type ID is required");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0");

        RuleFor(x => x.Fee)
            .GreaterThanOrEqualTo(0).WithMessage("Fee cannot be negative");

        RuleFor(x => x.TargetWalletTypeId)
            .NotEqual(x => x.SourceWalletTypeId)
            .WithMessage("Cannot convert to the same wallet type");
    }
}