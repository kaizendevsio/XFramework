using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Wallets.Api.Services;

public sealed partial class WalletWorkflowService
{
    public async Task<Result<List<WalletPaymentGatewayOptionResponse>>> GetPaymentGatewayOptionsAsync(
        WalletPaymentGatewayOptionsRequest request, CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
            return Result<List<WalletPaymentGatewayOptionResponse>>.Failure(contextResult.Message!, contextResult.StatusCode);

        var context = contextResult.Data!;
        var feature = await EnsureFeatureAsync(context, TenantModuleFeatureKeys.Wallets, ct);
        if (!feature.IsSuccess) return Failure<List<WalletPaymentGatewayOptionResponse>>(feature);

        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);

        // Delegated reads use the trusted target tenant, not the caller's default query-filter tenant.
        var options = await dbContext.Set<PaymentGateway>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == context.TenantId && !x.IsDeleted && x.IsEnabled)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new WalletPaymentGatewayOptionResponse
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                GatewayCategoryId = x.GatewayCategoryId,
                CategoryName = x.PaymentGatewayCategory.TenantId == context.TenantId && !x.PaymentGatewayCategory.IsDeleted
                    ? x.PaymentGatewayCategory.Name
                    : null,
                ServiceCharge = x.ServiceCharge,
                ConvenienceFee = x.ConvenienceFee,
                Discount = x.Discount
            })
            .ToListAsync(ct);

        return Result<List<WalletPaymentGatewayOptionResponse>>.Success(options);
    }
}
