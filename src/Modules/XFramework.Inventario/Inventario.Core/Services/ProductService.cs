using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Core.Patterns;
using XFramework.Core.Services.Caching;
using XFramework.Domain.Contexts;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace Inventario.Core.Services;

/// <summary>
/// Service for managing Product CRUD operations with caching and error handling
/// </summary>
public class ProductService
{
    private readonly AppDbContext _dbContext;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        AppDbContext dbContext,
        ICacheService cacheService,
        ILogger<ProductService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new product
    /// </summary>
    public async Task<Result<Product>> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Creating new product: {ProductName}", request.Name);

            var product = new Product
            {
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                StockQuantity = request.StockQuantity,
                CategoryId = request.CategoryId,
                SKU = request.SKU,
                Brand = request.Brand,
                Weight = request.Weight,
                Image = request.Image,
                IsAvailable = request.IsAvailable ?? true
            };

            _dbContext.Set<Product>().Add(product);
            await _dbContext.SaveChangesAsync(ct);

            // Cache the newly created product
            var cacheKey = $"products:{product.Id}";
            await _cacheService.SetAsync(cacheKey, product, 
                absoluteExpiration: TimeSpan.FromMinutes(10), 
                cancellationToken: ct);

            _logger.LogInformation("Product created successfully with ID: {ProductId}", product.Id);
            return Result<Product>.Success(product, 201, "Product created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product: {ProductName}", request.Name);
            return Result<Product>.Failure("An error occurred while creating the product", 500);
        }
    }

    /// <summary>
    /// Gets a product by ID with caching
    /// </summary>
    public async Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = $"products:{id}";

            // Try cache first
            var cached = await _cacheService.GetAsync<Product>(cacheKey, ct);
            if (cached.IsSuccess && cached.Data != null)
            {
                _logger.LogDebug("Product {ProductId} retrieved from cache", id);
                return Result<Product>.Success(cached.Data);
            }

            // Query database (NoTracking default from Phase 1.5)
            var product = await _dbContext.Set<Product>()
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

            if (product == null)
            {
                _logger.LogWarning("Product {ProductId} not found", id);
                return Result<Product>.NotFound($"Product with ID {id} not found");
            }

            // Cache the result
            await _cacheService.SetAsync(cacheKey, product,
                absoluteExpiration: TimeSpan.FromMinutes(10), 
                cancellationToken: ct);

            _logger.LogInformation("Product {ProductId} retrieved successfully", id);
            return Result<Product>.Success(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product {ProductId}", id);
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
        try
        {
            _logger.LogInformation("Retrieving products list - Page: {Page}, PageSize: {PageSize}", 
                request.Page, request.PageSize);

            var query = _dbContext.Set<Product>()
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted);

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

            _logger.LogInformation("Retrieved {Count} products (page {Page} of {TotalPages})", 
                products.Count, result.Page, result.TotalPages);

            return Result<PaginatedList<Product>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving products list");
            return Result<PaginatedList<Product>>.Failure("An error occurred retrieving products", 500);
        }
    }

    /// <summary>
    /// Updates an existing product
    /// </summary>
    public async Task<Result<Product>> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Updating product {ProductId}", id);

            // Use AsTracking for update operation
            var product = await _dbContext.Set<Product>()
                .AsTracking()
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

            if (product == null)
            {
                _logger.LogWarning("Product {ProductId} not found for update", id);
                return Result<Product>.NotFound($"Product with ID {id} not found");
            }

            // Update properties
            product.Name = request.Name;
            product.Description = request.Description;
            product.Price = request.Price;
            product.StockQuantity = request.StockQuantity;
            product.CategoryId = request.CategoryId;
            product.SKU = request.SKU;
            product.Brand = request.Brand;
            product.Weight = request.Weight;
            product.Image = request.Image;
            product.IsAvailable = request.IsAvailable ?? product.IsAvailable;

            // AuditInterceptor will handle ModifiedAt automatically
            await _dbContext.SaveChangesAsync(ct);

            // Invalidate cache
            var cacheKey = $"products:{id}";
            await _cacheService.RemoveAsync(cacheKey, ct);
            await _cacheService.RemoveByPrefixAsync("products:list:", ct);

            _logger.LogInformation("Product {ProductId} updated successfully", id);
            return Result<Product>.Success(product, "Product updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product {ProductId}", id);
            return Result<Product>.Failure("An error occurred updating the product", 500);
        }
    }

    /// <summary>
    /// Soft deletes a product
    /// </summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Deleting product {ProductId}", id);

            var product = await _dbContext.Set<Product>()
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

            if (product == null)
            {
                _logger.LogWarning("Product {ProductId} not found for deletion", id);
                return Result.NotFound($"Product with ID {id} not found");
            }

            // Soft delete (XDbContext handles IsDeleted flag and DeletedAt timestamp)
            _dbContext.Set<Product>().Remove(product);
            await _dbContext.SaveChangesAsync(ct);

            // Invalidate cache
            var cacheKey = $"products:{id}";
            await _cacheService.RemoveAsync(cacheKey, ct);
            await _cacheService.RemoveByPrefixAsync("products:list:", ct);

            _logger.LogInformation("Product {ProductId} deleted successfully", id);
            return Result.Success("Product deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product {ProductId}", id);
            return Result.Failure("An error occurred deleting the product", 500);
        }
    }
}

/// <summary>
/// Request for creating a product
/// </summary>
public record CreateProductRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public int StockQuantity { get; init; }
    public Guid CategoryId { get; init; }
    public string? SKU { get; init; }
    public string? Brand { get; init; }
    public decimal? Weight { get; init; }
    public string? Image { get; init; }
    public bool? IsAvailable { get; init; }
}

/// <summary>
/// Request for updating a product
/// </summary>
public record UpdateProductRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public int StockQuantity { get; init; }
    public Guid CategoryId { get; init; }
    public string? SKU { get; init; }
    public string? Brand { get; init; }
    public decimal? Weight { get; init; }
    public string? Image { get; init; }
    public bool? IsAvailable { get; init; }
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