using XFramework.Core.Patterns;

namespace Wallets.Api.Services;

public sealed record WalletFeeCalculation(
    decimal RequestedFee,
    decimal CalculatedFee,
    decimal AppliedFee,
    bool OverrideAllowed,
    Guid? FeeScheduleId);

public interface IWalletFeeCalculator
{
    Task<Result<WalletFeeCalculation>> CalculateAsync(
        Guid tenantId,
        WalletOperationType operationType,
        Guid? walletTypeId,
        Guid? currencyId,
        decimal amount,
        decimal? requestedFee,
        CancellationToken ct = default);
}

public sealed class WalletFeeCalculator(DbContext dbContext) : IWalletFeeCalculator
{
    public async Task<Result<WalletFeeCalculation>> CalculateAsync(
        Guid tenantId,
        WalletOperationType operationType,
        Guid? walletTypeId,
        Guid? currencyId,
        decimal amount,
        decimal? requestedFee,
        CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            return Result<WalletFeeCalculation>.Failure("Amount must be greater than zero", 400);
        }

        var now = DateTime.UtcNow;
        var schedule = await dbContext.Set<WalletFeeSchedule>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                !x.IsDeleted &&
                x.IsEnabled &&
                x.OperationType == operationType &&
                (x.WalletTypeId == null || x.WalletTypeId == walletTypeId) &&
                (x.CurrencyId == null || x.CurrencyId == currencyId) &&
                x.EffectiveAt <= now &&
                (x.ExpiresAt == null || x.ExpiresAt > now))
            .OrderByDescending(x => x.WalletTypeId.HasValue)
            .ThenByDescending(x => x.CurrencyId.HasValue)
            .ThenByDescending(x => x.EffectiveAt)
            .FirstOrDefaultAsync(ct);

        if (schedule is null)
        {
            if ((requestedFee ?? 0) != 0)
            {
                return Result<WalletFeeCalculation>.Failure(
                    "Requested fee requires an active wallet fee schedule",
                    400);
            }

            return Result<WalletFeeCalculation>.Success(new WalletFeeCalculation(
                0,
                0,
                0,
                false,
                null));
        }

        var calculatedFee = schedule.FixedFee + amount * schedule.PercentageFee / 100m;
        if (schedule.MinimumFee.HasValue)
        {
            calculatedFee = Math.Max(calculatedFee, schedule.MinimumFee.Value);
        }

        if (schedule.MaximumFee.HasValue)
        {
            calculatedFee = Math.Min(calculatedFee, schedule.MaximumFee.Value);
        }

        calculatedFee = decimal.Round(calculatedFee, 8, MidpointRounding.AwayFromZero);
        var suppliedFee = requestedFee ?? calculatedFee;

        if (!schedule.AllowRequestedFeeOverride && suppliedFee != calculatedFee)
        {
            return Result<WalletFeeCalculation>.Failure("Requested fee does not match the active fee schedule", 400);
        }

        return Result<WalletFeeCalculation>.Success(new WalletFeeCalculation(
            suppliedFee,
            calculatedFee,
            schedule.AllowRequestedFeeOverride && requestedFee.HasValue ? suppliedFee : calculatedFee,
            schedule.AllowRequestedFeeOverride,
            schedule.Id));
    }
}
