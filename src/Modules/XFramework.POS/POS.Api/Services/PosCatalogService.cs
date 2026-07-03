using Inventario.Integration.Drivers;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Contexts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;

namespace POS.Api.Services;

public sealed class PosCatalogService(
    IInventarioServiceWrapper inventario,
    AppDbContext db,
    IPosRequestContextResolver contextResolver)
{
    public async Task<Result<List<PosCatalogItemResponse>>> SearchAsync(
        SearchPosCatalogRequest request,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
            return Result<List<PosCatalogItemResponse>>.Failure(contextResult.Message!, contextResult.StatusCode);

        var context = contextResult.Data!;
        var warehouseId = request.WarehouseId;
        var locationId = request.LocationId;

        if (request.RegisterId is { } registerId)
        {
            var register = await db.Set<PosRegister>()
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.TenantId == context.TenantId &&
                    item.Id == registerId &&
                    !item.IsDeleted,
                    ct);

            if (register is null)
                return Result<List<PosCatalogItemResponse>>.NotFound("POS register was not found");

            warehouseId ??= register.DefaultWarehouseId;
            locationId ??= register.DefaultLocationId;
        }

        var response = await inventario.SearchSellableProducts(new SearchSellableProductsRequest
        {
            Search = request.Search,
            CategoryId = request.CategoryId,
            IsAvailable = request.IsAvailable,
            IncludeBaseProducts = request.IncludeBaseProducts,
            IncludeVariants = request.IncludeVariants,
            Page = request.Page,
            PageSize = request.PageSize,
            Metadata = context.Metadata
        });

        if (!response.IsSuccess)
            return Result<List<PosCatalogItemResponse>>.Failure(
                response.Message ?? "Inventario catalog search failed",
                (int)response.HttpStatusCode);

        var items = response.Response?
            .Select(item => new PosCatalogItemResponse
            {
                ProductId = item.ProductId,
                ProductVariationId = item.ProductVariationId,
                DisplayName = item.DisplayName,
                ProductName = item.ProductName,
                VariantName = item.VariantName,
                SKU = item.SKU,
                Brand = item.Brand,
                Image = item.Image,
                CategoryId = item.CategoryId,
                CategoryName = item.CategoryName,
                IsAvailable = item.IsAvailable,
                Price = item.Price
            })
            .ToList() ?? [];

        if (warehouseId.HasValue || locationId.HasValue)
        {
            var availableItems = new List<PosCatalogItemResponse>(items.Count);
            foreach (var item in items)
            {
                var balanceResponse = await inventario.GetStockBalances(new GetStockBalancesRequest
                {
                    ProductId = item.ProductId,
                    ProductVariationId = item.ProductVariationId,
                    WarehouseId = warehouseId,
                    LocationId = locationId,
                    Metadata = context.Metadata
                });

                if (!balanceResponse.IsSuccess)
                    return Result<List<PosCatalogItemResponse>>.Failure(
                        balanceResponse.Message ?? "Inventario stock balance lookup failed",
                        (int)balanceResponse.HttpStatusCode);

                var availableQuantity = balanceResponse.Response?.Sum(balance => balance.AvailableQuantity);
                var enrichedItem = item with
                {
                    AvailableQuantity = availableQuantity,
                    IsAvailable = item.IsAvailable && availableQuantity.GetValueOrDefault() > 0
                };

                if (request.IsAvailable != true || enrichedItem.IsAvailable)
                    availableItems.Add(enrichedItem);
            }

            items = availableItems;
        }

        return Result<List<PosCatalogItemResponse>>.Success(items);
    }
}
