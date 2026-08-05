using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using XFramework.Core.Loggers;
using XFramework.Core.Observability;
using XFramework.Core.Patterns;
using XFramework.Core.Services.Caching;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Products;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.Integration.Security;

namespace XFramework.Inventario.Api.Services;

/// <summary>
/// Service for managing Product CRUD operations with caching and error handling
/// </summary>
public class ProductService
{
    private readonly IDataContext _dataContext;
    private readonly AppDbContext? _db;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ProductService> _logger;
    private readonly ITrustedInvocationContextAccessor _trustedInvocationContextAccessor;

    public ProductService(
        IDataContext dataContext,
        ICacheService cacheService,
        ILogger<ProductService> logger,
        ITrustedInvocationContextAccessor trustedInvocationContextAccessor)
    {
        _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        _db = null;
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _trustedInvocationContextAccessor = trustedInvocationContextAccessor ?? throw new ArgumentNullException(nameof(trustedInvocationContextAccessor));
    }

    public ProductService(
        IDataContext dataContext,
        AppDbContext db,
        ICacheService cacheService,
        ILogger<ProductService> logger,
        ITrustedInvocationContextAccessor trustedInvocationContextAccessor)
    {
        _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _trustedInvocationContextAccessor = trustedInvocationContextAccessor ?? throw new ArgumentNullException(nameof(trustedInvocationContextAccessor));
    }

    /// <summary>
    /// Creates a new product
    /// </summary>
    public async Task<Result<Product>> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<Product>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var tenantId = tenantResult.Data;

        if (request.StockQuantity != 0)
        {
            return Result<Product>.Failure(
                "Initial stock must be posted through a stock movement with warehouse and location dimensions.",
                400);
        }

        using var activity = ActivitySources.Product.StartActivity("Product.Create");
        activity?.SetTag("product.name", request.Name);
        activity?.SetTag("product.price", request.Price);
        activity?.SetTag("product.category_id", request.CategoryId);
        activity?.SetTag("tenant.id", tenantId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var categoryExists = await _dataContext.Query<ProductCategory>()
                .AnyAsync(c => c.Id == request.CategoryId && c.TenantId == tenantId, ct);
            if (!categoryExists)
            {
                _logger.EntityNotFound("ProductCategory", request.CategoryId);
                return Result<Product>.NotFound("Product category not found");
            }

            var normalizedSku = NormalizeSku(request.SKU);
            if (normalizedSku is not null)
            {
                var skuExists = await _dataContext.Query<Product>()
                    .AnyAsync(p => p.TenantId == tenantId && !p.IsDeleted && p.SKU == normalizedSku, ct);
                if (skuExists)
                {
                    return Result<Product>.Failure("A product with the same SKU already exists for this tenant.", 409);
                }
            }

            var productId = Guid.NewGuid();
            activity?.SetTag("product.id", productId);

            _logger.EntityCreating("Product", productId, tenantId);

            var product = new Product
            {
                Id = productId,
                TenantId = tenantId,
                IsEnabled = true,
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                StockQuantity = 0,
                CategoryId = request.CategoryId,
                SKU = normalizedSku,
                Brand = request.Brand,
                Weight = request.Weight,
                Image = request.Image,
                IsAvailable = request.IsAvailable ?? true
            };

            _dataContext.Add(product);

            var saveResult = await _dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result<Product>.Failure(saveResult.Message ?? "Product save failed", saveResult.StatusCode);

            await TrySetProductCacheAsync(tenantId, product, ct);

            stopwatch.Stop();

            // Record metrics
            XFrameworkMetrics.ProductsCreated.Add(1,
                new KeyValuePair<string, object?>("category_id", request.CategoryId.ToString()));
            XFrameworkMetrics.ProductCreationDuration.Record(stopwatch.ElapsedMilliseconds,
                new KeyValuePair<string, object?>("result", "success"));

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("operation.duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.EntityCreated("Product", product.Id);
            return Result<Product>.Success(product, 201, "Product created successfully");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            XFrameworkMetrics.ProductCreationDuration.Record(stopwatch.ElapsedMilliseconds,
                new KeyValuePair<string, object?>("result", "error"));

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);
            activity?.SetTag("exception.message", ex.Message);
            activity?.SetTag("exception.stacktrace", ex.StackTrace);

            _logger.OperationFailed("Create", "Product", Guid.Empty, ex.Message, ex);
            return Result<Product>.Failure("An error occurred while creating the product", 500);
        }
    }

    /// <summary>
    /// Gets a product by ID with caching
    /// </summary>
    public async Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId();
        if (!tenantResult.IsSuccess)
            return Result<Product>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var tenantId = tenantResult.Data;

        try
        {
            var cacheKey = BuildProductCacheKey(tenantId, id);
            var cached = await TryGetProductCacheAsync(cacheKey, ct);
            if (cached is not null)
            {
                _logger.CacheHit(cacheKey);
                return Result<Product>.Success(cached);
            }

            _logger.CacheMiss(cacheKey);

            var product = await _dataContext.Query<Product>()
                .Include(p => p.Category)
                .Where(p => p.Id == id && p.TenantId == tenantId && !p.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (product == null)
            {
                _logger.EntityNotFound("Product", id);
                return Result<Product>.NotFound($"Product with ID {id} not found");
            }

            await TrySetProductCacheAsync(tenantId, product, ct);

            _logger.EntityRetrieved("Product", id);
            return Result<Product>.Success(product);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("Retrieve", "Product", id, ex.Message, ex);
            return Result<Product>.Failure("An error occurred retrieving the product", 500);
        }
    }

    /// <summary>
    /// Gets a paginated list of products with optional filtering
    /// </summary>
    public async Task<Result<PaginatedList<Product>>> GetListAsync(
        GetProductsRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId();
        if (!tenantResult.IsSuccess)
            return Result<PaginatedList<Product>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var tenantId = tenantResult.Data;

        try
        {
            _logger.EntityListing("Product", request.Page, request.PageSize);

            var query = _dataContext.Query<Product>()
                .Include(p => p.Category)
                .Where(p => p.TenantId == tenantId && !p.IsDeleted);

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var searchLower = request.Search.ToLower();
                query = query.Where(p =>
                    p.Name!.ToLower().Contains(searchLower) ||
                    (p.Description != null && p.Description.ToLower().Contains(searchLower)) ||
                    (p.SKU != null && p.SKU.ToLower().Contains(searchLower)));
            }

            // Apply category filter
            if (request.CategoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == request.CategoryId.Value);
            }

            // Apply availability filter
            if (request.IsAvailable.HasValue)
            {
                query = query.Where(p => p.IsAvailable == request.IsAvailable.Value);
            }

            // Get total count
            var totalCount = await query.CountAsync(ct);

            // Apply pagination
            var products = await query
                .OrderBy(p => p.Name)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);

            var result = new PaginatedList<Product>
            {
                Items = products,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
            };

            _logger.EntityQueryCompleted(products.Count, "Product");

            return Result<PaginatedList<Product>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("List", "Product", Guid.Empty, ex.Message, ex);
            return Result<PaginatedList<Product>>.Failure("An error occurred retrieving products", 500);
        }
    }

    public async Task<Result<List<SellableProductCatalogItem>>> SearchSellableProductsAsync(
        SearchSellableProductsRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<SellableProductCatalogItem>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var tenantId = tenantResult.Data;

        try
        {
            if (!request.IncludeBaseProducts && !request.IncludeVariants)
                return Result<List<SellableProductCatalogItem>>.Success([]);

            var page = request.Page <= 0 ? 1 : request.Page;
            var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 100);
            var productQuery = BuildSellableProductQuery(tenantId, request.CategoryId, request.IsAvailable);
            var search = NormalizeSearch(request.Search);
            IQueryable<SellableProductCatalogRow>? rows = null;

            if (request.IncludeBaseProducts)
                rows = BuildBaseCatalogRows(productQuery, search);

            if (request.IncludeVariants)
            {
                var variantRows = BuildVariantCatalogRows(productQuery, tenantId, search);
                rows = rows is null ? variantRows : rows.Concat(variantRows);
            }

            if (rows is null)
                return Result<List<SellableProductCatalogItem>>.Success([]);

            var pageRows = await rows
                .OrderBy(x => x.ProductName)
                .ThenBy(x => x.ProductVariationId.HasValue)
                .ThenBy(x => x.VariantTypeName)
                .ThenBy(x => x.VariantName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
            var items = pageRows.Select(ToCatalogItem).ToList();

            _logger.EntityQueryCompleted(items.Count, "SellableProductCatalog");
            return Result<List<SellableProductCatalogItem>>.Success(items);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("Search", "SellableProductCatalog", Guid.Empty, ex.Message, ex);
            return Result<List<SellableProductCatalogItem>>.Failure(
                "An error occurred searching sellable products",
                500);
        }
    }

    public async Task<Result<SellableProductDetail>> GetSellableProductAsync(
        GetSellableProductRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<SellableProductDetail>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var tenantId = tenantResult.Data;

        try
        {
            var product = await BuildSellableProductQuery(tenantId, categoryId: null, isAvailable: null)
                .Where(p => p.Id == request.ProductId)
                .Select(p => new
                {
                    p.Id,
                    Name = p.Name ?? string.Empty,
                    p.Description,
                    p.SKU,
                    p.Brand,
                    p.Image,
                    p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    p.IsAvailable,
                    p.Price
                })
                .FirstOrDefaultAsync(ct);

            if (product is null)
            {
                _logger.EntityNotFound("Product", request.ProductId);
                return Result<SellableProductDetail>.NotFound("Product not found");
            }

            var variationRows = await BuildSellableVariationRowsQuery(tenantId, request.ProductId)
                .OrderBy(x => x.VariantTypeName)
                .ThenBy(x => x.VariantName)
                .ToListAsync(ct);
            var variations = variationRows.Select(ToVariationItem).ToList();

            return Result<SellableProductDetail>.Success(new SellableProductDetail(
                product.Id,
                product.Name,
                product.Description,
                product.SKU,
                product.Brand,
                product.Image,
                product.CategoryId,
                product.CategoryName,
                product.IsAvailable,
                product.Price,
                variations));
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("Retrieve", "SellableProduct", request.ProductId, ex.Message, ex);
            return Result<SellableProductDetail>.Failure(
                "An error occurred retrieving the sellable product",
                500);
        }
    }

    public async Task<Result<List<SellableProductVariationItem>>> GetProductVariationsAsync(
        GetProductVariationsRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<SellableProductVariationItem>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var tenantId = tenantResult.Data;

        try
        {
            var productExists = await BuildSellableProductQuery(tenantId, categoryId: null, isAvailable: null)
                .AnyAsync(p => p.Id == request.ProductId, ct);
            if (!productExists)
            {
                _logger.EntityNotFound("Product", request.ProductId);
                return Result<List<SellableProductVariationItem>>.NotFound("Product not found");
            }

            var variationRows = await BuildSellableVariationRowsQuery(tenantId, request.ProductId)
                .OrderBy(x => x.VariantTypeName)
                .ThenBy(x => x.VariantName)
                .ToListAsync(ct);
            var variations = variationRows.Select(ToVariationItem).ToList();

            return Result<List<SellableProductVariationItem>>.Success(variations);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("List", "ProductVariation", request.ProductId, ex.Message, ex);
            return Result<List<SellableProductVariationItem>>.Failure(
                "An error occurred retrieving product variations",
                500);
        }
    }

    /// <summary>
    /// Updates an existing product
    /// </summary>
    public async Task<Result<Product>> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<Product>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var tenantId = tenantResult.Data;

        try
        {
            _logger.EntityUpdating("Product", id);

            var product = await _dataContext.Query<Product>()
                .Where(p => p.Id == id && p.TenantId == tenantId && !p.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (product == null)
            {
                _logger.EntityNotFound("Product", id);
                return Result<Product>.NotFound($"Product with ID {id} not found");
            }

            var categoryExists = await _dataContext.Query<ProductCategory>()
                .AnyAsync(c => c.Id == request.CategoryId && c.TenantId == tenantId, ct);
            if (!categoryExists)
            {
                _logger.EntityNotFound("ProductCategory", request.CategoryId);
                return Result<Product>.NotFound("Product category not found");
            }

            var normalizedSku = NormalizeSku(request.SKU);
            if (normalizedSku is not null)
            {
                var skuExists = await _dataContext.Query<Product>()
                    .AnyAsync(p =>
                        p.TenantId == tenantId &&
                        !p.IsDeleted &&
                        p.Id != id &&
                        p.SKU == normalizedSku,
                        ct);
                if (skuExists)
                {
                    return Result<Product>.Failure("A product with the same SKU already exists for this tenant.", 409);
                }
            }

            // Update properties
            product.Name = request.Name;
            product.Description = request.Description;
            product.Price = request.Price;
            product.CategoryId = request.CategoryId;
            product.SKU = normalizedSku;
            product.Brand = request.Brand;
            product.Weight = request.Weight;
            product.Image = request.Image;
            product.IsAvailable = request.IsAvailable ?? product.IsAvailable;

            // Attach as Modified and save
            _dataContext.Update(product);
            var saveResult = await _dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result<Product>.Failure(saveResult.Message ?? "Product update failed", saveResult.StatusCode);

            await TryInvalidateProductCacheAsync(tenantId, id, ct);

            _logger.EntityUpdated("Product", id);
            return Result<Product>.Success(product, "Product updated successfully");
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("Update", "Product", id, ex.Message, ex);
            return Result<Product>.Failure("An error occurred updating the product", 500);
        }
    }

    /// <summary>
    /// Soft deletes a product
    /// </summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId();
        if (!tenantResult.IsSuccess)
            return Result.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var tenantId = tenantResult.Data;

        try
        {
            _logger.EntityDeleting("Product", id);

            var product = await _dataContext.Query<Product>()
                .Where(p => p.Id == id && p.TenantId == tenantId && !p.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (product == null)
            {
                _logger.EntityNotFound("Product", id);
                return Result.NotFound($"Product with ID {id} not found");
            }

            var dependency = await FindDeleteDependencyAsync(tenantId, id, ct);
            if (dependency is not null)
                return Result.Conflict($"Product cannot be deleted while {dependency} exists.");

            // Soft delete (XDbContext handles IsDeleted flag and DeletedAt timestamp)
            _dataContext.Remove(product);
            var saveResult = await _dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result.Failure(saveResult.Message ?? "Product delete failed", saveResult.StatusCode);

            await TryInvalidateProductCacheAsync(tenantId, id, ct);

            _logger.EntityDeleted("Product", id);
            return Result.Success("Product deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("Delete", "Product", id, ex.Message, ex);
            return Result.Failure("An error occurred deleting the product", 500);
        }
    }

    private Result<Guid> GetCurrentTenantId(RequestBase? request = null)
    {
        var tenantId = _trustedInvocationContextAccessor.Current?.EffectiveTenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result<Guid>.Unauthorized("Authentication is required for product catalog operations");
        return Result<Guid>.Success(tenantId.Value);
    }

    private static string BuildProductCacheKey(Guid tenantId, Guid productId) =>
        $"inventario:tenant:{tenantId}:product:{productId}";

    private static string BuildProductListCachePrefix(Guid tenantId) =>
        $"inventario:tenant:{tenantId}:products:";

    private async Task<Product?> TryGetProductCacheAsync(string cacheKey, CancellationToken ct)
    {
        try
        {
            var cached = await _cacheService.GetAsync<Product>(cacheKey, ct);
            if (!cached.IsSuccess)
                _logger.LogWarning("Product cache read failed for {CacheKey}: {Message}", cacheKey, cached.Message);
            return cached.IsSuccess ? cached.Data : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product cache read failed for {CacheKey}", cacheKey);
            return null;
        }
    }

    private async Task TrySetProductCacheAsync(Guid tenantId, Product product, CancellationToken ct)
    {
        var cacheKey = BuildProductCacheKey(tenantId, product.Id);
        try
        {
            var cached = await _cacheService.SetAsync(
                cacheKey,
                product,
                absoluteExpiration: TimeSpan.FromMinutes(10),
                cancellationToken: ct);
            if (!cached.IsSuccess)
            {
                _logger.LogWarning("Product cache write failed for {CacheKey}: {Message}", cacheKey, cached.Message);
                return;
            }

            _logger.CacheSetting(cacheKey, 10);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product cache write failed for {CacheKey}", cacheKey);
        }
    }

    private async Task TryInvalidateProductCacheAsync(Guid tenantId, Guid productId, CancellationToken ct)
    {
        var cacheKey = BuildProductCacheKey(tenantId, productId);
        var listPrefix = BuildProductListCachePrefix(tenantId);
        try
        {
            var removed = await _cacheService.RemoveAsync(cacheKey, ct);
            if (!removed.IsSuccess)
                _logger.LogWarning("Product cache removal failed for {CacheKey}: {Message}", cacheKey, removed.Message);
            else
                _logger.CacheInvalidated(cacheKey);

            var prefixRemoved = await _cacheService.RemoveByPrefixAsync(listPrefix, ct);
            if (!prefixRemoved.IsSuccess)
                _logger.LogWarning("Product cache prefix removal failed for {CachePrefix}: {Message}", listPrefix, prefixRemoved.Message);
            else
                _logger.CacheCleared($"{listPrefix}*");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product cache invalidation failed for product {ProductId}", productId);
        }
    }

    private async Task<string?> FindDeleteDependencyAsync(Guid tenantId, Guid productId, CancellationToken ct)
    {
        if (await _dataContext.Query<StockBalance>().AnyAsync(x =>
                x.TenantId == tenantId &&
                x.ProductId == productId &&
                (x.OnHandQuantity != 0 || x.ReservedQuantity != 0), ct))
        {
            return "stock remains on hand or reserved";
        }

        if (await _dataContext.Query<ReservationAllocation>().AnyAsync(x =>
                x.TenantId == tenantId &&
                x.ProductId == productId &&
                x.Status == ReservationAllocationStatus.Reserved, ct))
        {
            return "active reservation allocations";
        }

        if (await _dataContext.Query<InventoryLot>().AnyAsync(x =>
                x.TenantId == tenantId && x.ProductId == productId, ct))
        {
            return "inventory lots";
        }

        if (await _dataContext.Query<ProductVariation>().AnyAsync(x =>
                x.TenantId == tenantId && x.ProductId == productId, ct))
        {
            return "product variants";
        }

        if (await _dataContext.Query<InventoryReorderRule>().AnyAsync(x =>
                x.TenantId == tenantId && x.ProductId == productId && x.IsActive, ct))
        {
            return "active reorder rules";
        }

        if (await _dataContext.Query<PurchaseOrderLine>().AnyAsync(x =>
                x.TenantId == tenantId &&
                x.ProductId == productId &&
                x.ReceivedQuantity < x.OrderedQuantity &&
                x.PurchaseOrder != null &&
                x.PurchaseOrder.Status != PurchaseOrderStatus.Cancelled &&
                x.PurchaseOrder.Status != PurchaseOrderStatus.Received, ct))
        {
            return "open purchase-order lines";
        }

        return null;
    }

    private IQueryable<Product> BuildSellableProductQuery(
        Guid tenantId,
        Guid? categoryId,
        bool? isAvailable)
    {
        var query = CatalogDb.Set<Product>()
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.IsEnabled);

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (isAvailable.HasValue)
            query = query.Where(p => p.IsAvailable == isAvailable.Value);

        return query;
    }

    private static IQueryable<SellableProductCatalogRow> BuildBaseCatalogRows(
        IQueryable<Product> products,
        string? search)
    {
        if (search is not null)
        {
            products = products.Where(product =>
                (product.Name != null && product.Name.ToLower().Contains(search)) ||
                (product.SKU != null && product.SKU.ToLower().Contains(search)) ||
                (product.Brand != null && product.Brand.ToLower().Contains(search)));
        }

        return products.Select(product => new SellableProductCatalogRow
        {
            ProductId = product.Id,
            ProductVariationId = null,
            DisplayName = product.Name ?? string.Empty,
            ProductName = product.Name ?? string.Empty,
            VariantName = null,
            ProductVariationTypeId = null,
            VariantTypeName = null,
            SKU = product.SKU,
            Brand = product.Brand,
            Image = product.Image,
            CategoryId = product.CategoryId,
            CategoryName = product.Category != null ? product.Category.Name : null,
            IsAvailable = product.IsAvailable,
            Price = product.Price
        });
    }

    private IQueryable<SellableProductCatalogRow> BuildVariantCatalogRows(
        IQueryable<Product> products,
        Guid tenantId,
        string? search)
    {
        var variations = CatalogDb.Set<ProductVariation>()
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && !v.IsDeleted && v.IsEnabled);
        var variationTypes = CatalogDb.Set<ProductVariationType>()
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted);

        var rows =
            from product in products
            join variation in variations on product.Id equals variation.ProductId
            join variationType in variationTypes
                on variation.ProductVariationTypeId equals (Guid?)variationType.Id into variationTypeJoin
            from variationType in variationTypeJoin.DefaultIfEmpty()
            select new { product, variation, variationType };

        if (search is not null)
        {
            rows = rows.Where(row =>
                (row.product.Name != null && row.product.Name.ToLower().Contains(search)) ||
                (row.product.SKU != null && row.product.SKU.ToLower().Contains(search)) ||
                (row.product.Brand != null && row.product.Brand.ToLower().Contains(search)) ||
                (row.variation.Name != null && row.variation.Name.ToLower().Contains(search)) ||
                (row.variationType != null && row.variationType.Name != null && row.variationType.Name.ToLower().Contains(search)) ||
                (row.variation.VariationType != null && row.variation.VariationType.ToLower().Contains(search)));
        }

        return rows.Select(row => new SellableProductCatalogRow
        {
            ProductId = row.product.Id,
            ProductVariationId = row.variation.Id,
            DisplayName = (row.product.Name ?? string.Empty) + " - " + (row.variation.Name ?? string.Empty),
            ProductName = row.product.Name ?? string.Empty,
            VariantName = row.variation.Name,
            ProductVariationTypeId = row.variation.ProductVariationTypeId,
            VariantTypeName = row.variationType != null ? row.variationType.Name : row.variation.VariationType,
            SKU = row.product.SKU,
            Brand = row.product.Brand,
            Image = row.product.Image,
            CategoryId = row.product.CategoryId,
            CategoryName = row.product.Category != null ? row.product.Category.Name : null,
            IsAvailable = row.product.IsAvailable,
            Price = row.variation.Price
        });
    }

    private static SellableProductCatalogItem ToCatalogItem(SellableProductCatalogRow row) => new(
        row.ProductId,
        row.ProductVariationId,
        row.DisplayName,
        row.ProductName,
        row.VariantName,
        row.ProductVariationTypeId,
        row.VariantTypeName,
        row.SKU,
        row.Brand,
        row.Image,
        row.CategoryId,
        row.CategoryName,
        row.IsAvailable,
        row.Price);

    private sealed class SellableProductCatalogRow
    {
        public Guid ProductId { get; init; }
        public Guid? ProductVariationId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string ProductName { get; init; } = string.Empty;
        public string? VariantName { get; init; }
        public Guid? ProductVariationTypeId { get; init; }
        public string? VariantTypeName { get; init; }
        public string? SKU { get; init; }
        public string? Brand { get; init; }
        public string? Image { get; init; }
        public Guid CategoryId { get; init; }
        public string? CategoryName { get; init; }
        public bool IsAvailable { get; init; }
        public decimal Price { get; init; }
    }

    private IQueryable<SellableProductVariationRow> BuildSellableVariationRowsQuery(
        Guid tenantId,
        Guid productId)
    {
        var products = BuildSellableProductQuery(tenantId, categoryId: null, isAvailable: null)
            .Where(p => p.Id == productId);
        var variations = CatalogDb.Set<ProductVariation>()
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && !v.IsDeleted && v.IsEnabled);
        var variationTypes = CatalogDb.Set<ProductVariationType>()
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted);

        return
            from product in products
            join variation in variations on product.Id equals variation.ProductId
            join variationType in variationTypes
                on variation.ProductVariationTypeId equals (Guid?)variationType.Id into variationTypeJoin
            from variationType in variationTypeJoin.DefaultIfEmpty()
            select new SellableProductVariationRow
            {
                ProductVariationId = variation.Id,
                ProductId = product.Id,
                ProductVariationTypeId = variation.ProductVariationTypeId,
                VariantTypeName = variationType != null ? variationType.Name : variation.VariationType,
                VariantName = variation.Name ?? string.Empty,
                Price = variation.Price,
                BaseProductPrice = product.Price,
                PriceDelta = variation.Price - product.Price
            };
    }

    private static SellableProductVariationItem ToVariationItem(SellableProductVariationRow row) => new(
        row.ProductVariationId,
        row.ProductId,
        row.ProductVariationTypeId,
        row.VariantTypeName,
        row.VariantName,
        row.Price,
        row.BaseProductPrice,
        row.PriceDelta);

    private sealed class SellableProductVariationRow
    {
        public Guid ProductVariationId { get; init; }
        public Guid ProductId { get; init; }
        public Guid? ProductVariationTypeId { get; init; }
        public string? VariantTypeName { get; init; }
        public string VariantName { get; init; } = string.Empty;
        public decimal Price { get; init; }
        public decimal BaseProductPrice { get; init; }
        public decimal PriceDelta { get; init; }
    }

    private static string? NormalizeSku(string? sku) =>
        string.IsNullOrWhiteSpace(sku) ? null : sku.Trim();

    private static string? NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();

    private AppDbContext CatalogDb => _db
        ?? throw new InvalidOperationException(
            "AppDbContext is required for sellable catalog read operations.");
}

/// <summary>
/// Request for listing products with pagination and filtering
/// </summary>
public record GetProductsRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Search { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? IsAvailable { get; init; }
}

/// <summary>
/// Paginated list wrapper
/// </summary>
public class PaginatedList<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
