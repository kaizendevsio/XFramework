using FluentValidation;
using POS.Domain.Shared.Contracts.Requests;

namespace POS.Api.Features.Scanner.Claim;

public sealed class ClaimPosScannerPairingValidator : AbstractValidator<ClaimPosScannerPairingRequest>
{
    public ClaimPosScannerPairingValidator()
    {
        RuleFor(x => x).Must(x =>
                (string.IsNullOrEmpty(x.Challenge) && IsPairingCode(x.PairingCode)) ||
                (string.IsNullOrEmpty(x.PairingCode) && IsChallenge(x.Challenge)))
            .WithMessage("Enter exactly six digits or use a valid pairing QR, not both.");
    }

    internal static bool IsPairingCode(string? value) =>
        value is { Length: 6 } && value.All(c => c is >= '0' and <= '9');

    internal static bool IsChallenge(string? value) =>
        value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}
