using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Api.Services;

using XFramework.Integration.Security;

public sealed class PurchasingService(
    IDataContext dataContext,
    ITrustedInvocationContextAccessor trustedInvocationContextAccessor,
    StockPostingService stockPostingService,
    ProductVariationService productVariationService,
    ITenantModuleFeatureService featureService)
{
    private static readonly JsonSerializerOptions HashJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<List<Supplier>>> GetSuppliersAsync(
        GetSuppliersRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<Supplier>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsurePurchasingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<List<Supplier>>.Failure(featureResult.Message!, featureResult.StatusCode);

        var query = dataContext.Query<Supplier>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantResult.Data && !x.IsDeleted);

        if (!request.IncludeInactive)
            query = query.Where(x => x.IsActive);

        var suppliers = await query
            .OrderBy(x => x.Name)
            .Take(500)
            .ToListAsync(ct);

        return Result<List<Supplier>>.Success(suppliers);
    }

    public async Task<Result<Supplier>> CreateSupplierAsync(
        CreateSupplierRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<Supplier>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsurePurchasingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<Supplier>.Failure(featureResult.Message!, featureResult.StatusCode);

        var tenantId = tenantResult.Data;
        var code = NormalizeRequired(request.Code);
        var name = NormalizeRequired(request.Name);
        if (code is null || name is null)
            return Result<Supplier>.Failure("Supplier code and name are required.", 400);

        var duplicate = await dataContext.Query<Supplier>()
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted, ct);
        if (duplicate)
            return Result<Supplier>.Conflict("A supplier with the same code already exists.");

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            ContactName = NormalizeOptional(request.ContactName),
            Email = NormalizeOptional(request.Email),
            Phone = NormalizeOptional(request.Phone),
            IsActive = request.IsActive,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        dataContext.Add(supplier);
        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<Supplier>.Failure(saveResult.Message ?? "Supplier save failed.", saveResult.StatusCode);

        return Result<Supplier>.Success(supplier, 201, "Supplier created.");
    }

    public async Task<Result<List<PurchaseOrder>>> GetPurchaseOrdersAsync(
        GetPurchaseOrdersRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<PurchaseOrder>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsurePurchasingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<List<PurchaseOrder>>.Failure(featureResult.Message!, featureResult.StatusCode);

        var query = dataContext.Query<PurchaseOrder>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantResult.Data && !x.IsDeleted);

        if (request.Status is { } status)
            query = query.Where(x => x.Status == status);
        if (request.SupplierId is { } supplierId)
            query = query.Where(x => x.SupplierId == supplierId);

        var orders = await query
            .OrderByDescending(x => x.OrderDate)
            .Take(500)
            .ToListAsync(ct);

        return Result<List<PurchaseOrder>>.Success(orders);
    }

    public async Task<Result<PurchaseOrder>> GetPurchaseOrderAsync(
        GetPurchaseOrderRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<PurchaseOrder>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsurePurchasingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<PurchaseOrder>.Failure(featureResult.Message!, featureResult.StatusCode);

        var order = await LoadPurchaseOrder(tenantResult.Data, request.Id, ct);
        return order is null
            ? Result<PurchaseOrder>.NotFound("Purchase order not found.")
            : Result<PurchaseOrder>.Success(order);
    }

    public async Task<Result<PurchaseOrder>> CreatePurchaseOrderAsync(
        CreatePurchaseOrderRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<PurchaseOrder>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsurePurchasingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<PurchaseOrder>.Failure(featureResult.Message!, featureResult.StatusCode);

        if (request.Lines.Count == 0)
            return Result<PurchaseOrder>.Failure("At least one purchase order line is required.", 400);

        if (request.Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.PartiallyReceived or PurchaseOrderStatus.Cancelled)
            return Result<PurchaseOrder>.Failure("New purchase orders can only start as draft or open.", 400);

        var tenantId = tenantResult.Data;
        if (request.SupplierId is { } supplierId)
        {
            var supplierExists = await dataContext.Query<Supplier>()
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && x.Id == supplierId && !x.IsDeleted && x.IsActive, ct);
            if (!supplierExists)
                return Result<PurchaseOrder>.NotFound("Supplier not found.");
        }

        foreach (var line in request.Lines)
        {
            if (line.OrderedQuantity <= 0)
                return Result<PurchaseOrder>.Failure("Purchase order line quantities must be greater than zero.", 400);

            var productExists = await dataContext.Query<Product>()
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && x.Id == line.ProductId && !x.IsDeleted, ct);
            if (!productExists)
                return Result<PurchaseOrder>.NotFound("Product not found.");

            var variationResult = await productVariationService.ValidateProductVariationAsync(
                tenantId,
                line.ProductId,
                line.ProductVariationId,
                ct);
            if (!variationResult.IsSuccess)
                return Result<PurchaseOrder>.Failure(variationResult.Message!, variationResult.StatusCode);
        }

        var orderNumber = NormalizeOptional(request.OrderNumber) ?? GenerateDocumentNumber("PO");
        var duplicate = await dataContext.Query<PurchaseOrder>()
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.OrderNumber == orderNumber && !x.IsDeleted, ct);
        if (duplicate)
            return Result<PurchaseOrder>.Conflict("A purchase order with the same number already exists.");

        var now = DateTime.UtcNow;
        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = orderNumber,
            SupplierId = request.SupplierId,
            Status = request.Status,
            OrderDate = request.OrderDate ?? now,
            ExpectedDate = request.ExpectedDate,
            Notes = NormalizeOptional(request.Notes),
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        dataContext.Add(order);

        foreach (var lineRequest in request.Lines)
        {
            var line = new PurchaseOrderLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PurchaseOrderId = order.Id,
                ProductId = lineRequest.ProductId,
                ProductVariationId = lineRequest.ProductVariationId,
                OrderedQuantity = lineRequest.OrderedQuantity,
                ReceivedQuantity = 0,
                UnitCost = lineRequest.UnitCost,
                UnitOfMeasure = NormalizeOptional(lineRequest.UnitOfMeasure),
                Notes = NormalizeOptional(lineRequest.Notes),
                IsEnabled = true,
                CreatedAt = now,
                ConcurrencyStamp = Guid.NewGuid()
            };
            order.Lines.Add(line);
            dataContext.Add(line);
        }

        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<PurchaseOrder>.Failure(saveResult.Message ?? "Purchase order save failed.", saveResult.StatusCode);

        return Result<PurchaseOrder>.Success(order, 201, "Purchase order created.");
    }

    public async Task<Result<PurchaseOrder>> SetPurchaseOrderStatusAsync(
        SetPurchaseOrderStatusRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<PurchaseOrder>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsurePurchasingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<PurchaseOrder>.Failure(featureResult.Message!, featureResult.StatusCode);

        var order = await LoadPurchaseOrder(tenantResult.Data, request.PurchaseOrderId, ct);
        if (order is null)
            return Result<PurchaseOrder>.NotFound("Purchase order not found.");

        if (order.Status is PurchaseOrderStatus.Received && request.Status != PurchaseOrderStatus.Received)
            return Result<PurchaseOrder>.Conflict("Received purchase orders cannot be reopened.");

        if (request.Status is PurchaseOrderStatus.PartiallyReceived or PurchaseOrderStatus.Received)
            return Result<PurchaseOrder>.Failure("Receiving controls received purchase order statuses.", 400);

        order.Status = request.Status;
        order.ModifiedAt = DateTime.UtcNow;
        dataContext.Update(order);

        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<PurchaseOrder>.Failure(saveResult.Message ?? "Purchase order status update failed.", saveResult.StatusCode);

        return Result<PurchaseOrder>.Success(order);
    }

    public async Task<Result<List<ReceivingDocument>>> GetReceivingDocumentsAsync(
        GetReceivingDocumentsRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<ReceivingDocument>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsurePurchasingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<List<ReceivingDocument>>.Failure(featureResult.Message!, featureResult.StatusCode);

        var query = dataContext.Query<ReceivingDocument>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantResult.Data && !x.IsDeleted);

        if (request.PurchaseOrderId is { } purchaseOrderId)
            query = query.Where(x => x.PurchaseOrderId == purchaseOrderId);
        if (request.Status is { } status)
            query = query.Where(x => x.Status == status);

        var documents = await query
            .OrderByDescending(x => x.ReceivedAt)
            .Take(500)
            .ToListAsync(ct);

        return Result<List<ReceivingDocument>>.Success(documents);
    }

    public async Task<Result<ReceivingDocument>> ReceiveAsync(
        ReceiveInventoryRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<ReceivingDocument>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsurePurchasingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<ReceivingDocument>.Failure(featureResult.Message!, featureResult.StatusCode);

        if (request.Lines.Count == 0)
            return Result<ReceivingDocument>.Failure("At least one receiving line is required.", 400);

        var tenantId = tenantResult.Data;
        var idempotencyKey = NormalizeOptional(request.IdempotencyKey);
        var requestHash = ComputeRequestHash(tenantId, request);
        var replay = await FindReceivingReplay(tenantId, idempotencyKey, requestHash, ct);
        if (replay is not null)
            return replay;

        var warehouseExists = await dataContext.Query<Warehouse>()
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == request.WarehouseId && !x.IsDeleted, ct);
        if (!warehouseExists)
            return Result<ReceivingDocument>.NotFound("Warehouse not found.");

        var locationExists = await dataContext.Query<InventoryLocation>()
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == request.LocationId && x.WarehouseId == request.WarehouseId && !x.IsDeleted, ct);
        if (!locationExists)
            return Result<ReceivingDocument>.NotFound("Location not found.");

        PurchaseOrder? purchaseOrder = null;
        var purchaseOrderLines = new List<PurchaseOrderLine>();
        if (request.PurchaseOrderId is { } purchaseOrderId)
        {
            purchaseOrder = await LoadPurchaseOrder(tenantId, purchaseOrderId, ct);
            if (purchaseOrder is null)
                return Result<ReceivingDocument>.NotFound("Purchase order not found.");
            if (purchaseOrder.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Received)
                return Result<ReceivingDocument>.Conflict("Purchase order is not open for receiving.");

            purchaseOrderLines = purchaseOrder.Lines;
        }

        if (request.SupplierId is { } supplierId)
        {
            var supplierExists = await dataContext.Query<Supplier>()
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && x.Id == supplierId && !x.IsDeleted && x.IsActive, ct);
            if (!supplierExists)
                return Result<ReceivingDocument>.NotFound("Supplier not found.");
        }

        var receiptNumber = NormalizeOptional(request.ReceiptNumber) ?? GenerateDocumentNumber("RCV");
        var duplicateReceipt = await dataContext.Query<ReceivingDocument>()
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.ReceiptNumber == receiptNumber && !x.IsDeleted, ct);
        if (duplicateReceipt)
            return Result<ReceivingDocument>.Conflict("A receiving document with the same receipt number already exists.");

        var now = DateTime.UtcNow;
        var document = new ReceivingDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReceiptNumber = receiptNumber,
            PurchaseOrderId = request.PurchaseOrderId,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            SupplierId = request.SupplierId ?? purchaseOrder?.SupplierId,
            Status = ReceivingDocumentStatus.Posted,
            ReceivedAt = request.ReceivedAt ?? now,
            ReferenceNumber = NormalizeOptional(request.ReferenceNumber),
            Notes = NormalizeOptional(request.Notes),
            IdempotencyKey = idempotencyKey,
            RequestHash = idempotencyKey is null ? null : requestHash,
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        dataContext.Add(document);

        for (var i = 0; i < request.Lines.Count; i++)
        {
            var lineRequest = request.Lines[i];
            var lineResult = await StageReceivingLine(
                tenantId,
                document,
                lineRequest,
                purchaseOrderLines,
                idempotencyKey,
                i,
                ct);
            if (!lineResult.IsSuccess)
                return Result<ReceivingDocument>.Failure(lineResult.Message!, lineResult.StatusCode);

            document.Lines.Add(lineResult.Data!);
        }

        if (purchaseOrder is not null)
        {
            ApplyPurchaseOrderStatus(purchaseOrder, purchaseOrderLines);
            purchaseOrder.ModifiedAt = now;
            dataContext.Update(purchaseOrder);
        }

        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<ReceivingDocument>.Failure(saveResult.Message ?? "Receiving save failed.", saveResult.StatusCode);

        return Result<ReceivingDocument>.Success(document, 201, "Receiving document posted.");
    }

    private async Task<Result<ReceivingLine>> StageReceivingLine(
        Guid tenantId,
        ReceivingDocument document,
        ReceivingLineRequest lineRequest,
        List<PurchaseOrderLine> purchaseOrderLines,
        string? receivingIdempotencyKey,
        int lineIndex,
        CancellationToken ct)
    {
        if (lineRequest.Quantity <= 0)
            return Result<ReceivingLine>.Failure("Receiving quantity must be greater than zero.", 400);

        var product = await dataContext.Query<Product>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.Id == lineRequest.ProductId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);
        if (product is null)
            return Result<ReceivingLine>.NotFound("Product not found.");

        PurchaseOrderLine? purchaseOrderLine = null;
        if (document.PurchaseOrderId is not null)
        {
            if (lineRequest.PurchaseOrderLineId is not { } purchaseOrderLineId)
                return Result<ReceivingLine>.Failure("Receiving against a purchase order requires purchase order line id.", 400);

            purchaseOrderLine = purchaseOrderLines.FirstOrDefault(x => x.Id == purchaseOrderLineId);
            if (purchaseOrderLine is null)
                return Result<ReceivingLine>.NotFound("Purchase order line not found.");
            if (purchaseOrderLine.ProductId != lineRequest.ProductId)
                return Result<ReceivingLine>.Failure("Receiving line product does not match the purchase order line.", 400);

            if (purchaseOrderLine.ProductVariationId != lineRequest.ProductVariationId)
                return Result<ReceivingLine>.Failure("Receiving line variant does not match the purchase order line.", 400);

            var remaining = purchaseOrderLine.OrderedQuantity - purchaseOrderLine.ReceivedQuantity;
            if (lineRequest.Quantity > remaining)
                return Result<ReceivingLine>.Conflict("Receiving quantity exceeds the remaining purchase order quantity.");
        }

        var variationResult = await productVariationService.ValidateProductVariationAsync(
            tenantId,
            lineRequest.ProductId,
            lineRequest.ProductVariationId,
            ct);
        if (!variationResult.IsSuccess)
            return Result<ReceivingLine>.Failure(variationResult.Message!, variationResult.StatusCode);

        var lotResult = await ResolveLot(tenantId, document, lineRequest, ct);
        if (!lotResult.IsSuccess)
            return Result<ReceivingLine>.Failure(lotResult.Message!, lotResult.StatusCode);

        var stockResult = await stockPostingService.StageAsync(
            new PostStockMovementRequest
            {
                Metadata = new() { RequestedTenantId = tenantId },
                ProductId = lineRequest.ProductId,
                ProductVariationId = lineRequest.ProductVariationId,
                WarehouseId = document.WarehouseId,
                LocationId = document.LocationId,
                LotId = lotResult.Data?.Id,
                MovementType = InventoryMovementType.Receipt,
                Quantity = lineRequest.Quantity,
                UnitOfMeasure = NormalizeOptional(lineRequest.UnitOfMeasure),
                ReferenceType = "receiving",
                ReferenceId = document.Id,
                Reason = $"Receiving {document.ReceiptNumber}",
                IdempotencyKey = receivingIdempotencyKey is null ? null : $"{receivingIdempotencyKey}:line:{lineIndex}"
            },
            lotResult.Data,
            ct);
        if (!stockResult.IsSuccess)
            return Result<ReceivingLine>.Failure(stockResult.Message!, stockResult.StatusCode);

        if (purchaseOrderLine is not null)
        {
            purchaseOrderLine.ReceivedQuantity += lineRequest.Quantity;
            purchaseOrderLine.ModifiedAt = DateTime.UtcNow;
            dataContext.Update(purchaseOrderLine);
        }

        var line = new ReceivingLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReceivingDocumentId = document.Id,
            PurchaseOrderLineId = lineRequest.PurchaseOrderLineId,
            ProductId = lineRequest.ProductId,
            ProductVariationId = lineRequest.ProductVariationId,
            LotId = lotResult.Data?.Id,
            StockBalanceId = stockResult.Data!.StockBalanceId,
            Quantity = lineRequest.Quantity,
            UnitCost = lineRequest.UnitCost,
            UnitOfMeasure = NormalizeOptional(lineRequest.UnitOfMeasure),
            LotNumber = lotResult.Data?.LotNumber ?? NormalizeOptional(lineRequest.LotNumber),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        dataContext.Add(line);
        return Result<ReceivingLine>.Success(line);
    }

    private async Task<Result<InventoryLot?>> ResolveLot(
        Guid tenantId,
        ReceivingDocument document,
        ReceivingLineRequest lineRequest,
        CancellationToken ct)
    {
        if (lineRequest.LotId is { } lotId)
        {
            var existing = await dataContext.Query<InventoryLot>()
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId && x.Id == lotId && !x.IsDeleted)
                .FirstOrDefaultAsync(ct);
            if (existing is null)
                return Result<InventoryLot?>.NotFound("Lot not found.");
            if (existing.ProductId != lineRequest.ProductId)
                return Result<InventoryLot?>.Failure("Lot does not belong to the receiving product.", 400);
            if (existing.ProductVariationId != lineRequest.ProductVariationId)
                return Result<InventoryLot?>.Failure("Lot does not belong to the receiving variant.", 400);

            return Result<InventoryLot?>.Success(existing);
        }

        var lotNumber = NormalizeOptional(lineRequest.LotNumber);
        if (lotNumber is null)
            return Result<InventoryLot?>.Success(null);

        var existingLot = await dataContext.Query<InventoryLot>()
            .IgnoreQueryFilters()
            .Where(x =>
                x.TenantId == tenantId &&
                x.ProductId == lineRequest.ProductId &&
                x.ProductVariationId == lineRequest.ProductVariationId &&
                x.LotNumber == lotNumber &&
                !x.IsDeleted)
            .FirstOrDefaultAsync(ct);
        if (existingLot is not null)
            return Result<InventoryLot?>.Success(existingLot);

        if (lineRequest.ManufacturedAt is { } manufacturedAt &&
            lineRequest.ExpiresAt is { } expiresAt &&
            manufacturedAt > expiresAt)
        {
            return Result<InventoryLot?>.Failure("Lot manufacture date must be before expiration date.", 400);
        }

        var lot = new InventoryLot
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = lineRequest.ProductId,
            ProductVariationId = lineRequest.ProductVariationId,
            LotNumber = lotNumber,
            SupplierReference = NormalizeOptional(lineRequest.SupplierReference) ?? document.ReferenceNumber,
            SourceReferenceType = "receiving",
            SourceReferenceId = document.Id,
            ReceivedAt = document.ReceivedAt,
            ManufacturedAt = lineRequest.ManufacturedAt,
            ExpiresAt = lineRequest.ExpiresAt,
            UnitCost = lineRequest.UnitCost,
            Status = InventoryLotStatus.Available,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        dataContext.Add(lot);
        return Result<InventoryLot?>.Success(lot);
    }

    private async Task<Result<ReceivingDocument>?> FindReceivingReplay(
        Guid tenantId,
        string? idempotencyKey,
        string requestHash,
        CancellationToken ct)
    {
        if (idempotencyKey is null)
            return null;

        var existing = await dataContext.Query<ReceivingDocument>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.IdempotencyKey == idempotencyKey && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);
        if (existing is null)
            return null;

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            return Result<ReceivingDocument>.Conflict("Idempotency key was already used with a different receiving request.");

        existing.Lines = await dataContext.Query<ReceivingLine>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.ReceivingDocumentId == existing.Id && !x.IsDeleted)
            .ToListAsync(ct);
        return Result<ReceivingDocument>.Success(existing, "Receiving document already processed.");
    }

    private async Task<PurchaseOrder?> LoadPurchaseOrder(Guid tenantId, Guid purchaseOrderId, CancellationToken ct)
    {
        var order = await dataContext.Query<PurchaseOrder>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.Id == purchaseOrderId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);
        if (order is null)
            return null;

        order.Lines = await dataContext.Query<PurchaseOrderLine>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.PurchaseOrderId == purchaseOrderId && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
        return order;
    }

    private static void ApplyPurchaseOrderStatus(PurchaseOrder order, List<PurchaseOrderLine> lines)
    {
        if (lines.All(x => x.ReceivedQuantity >= x.OrderedQuantity))
        {
            order.Status = PurchaseOrderStatus.Received;
            return;
        }

        order.Status = lines.Any(x => x.ReceivedQuantity > 0)
            ? PurchaseOrderStatus.PartiallyReceived
            : PurchaseOrderStatus.Open;
    }

    private Result<Guid> GetCurrentTenantId(RequestBase? request)
    {
        var tenantId = trustedInvocationContextAccessor.Current?.EffectiveTenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result<Guid>.Unauthorized("Authentication is required for purchasing operations.");
        return Result<Guid>.Success(tenantId.Value);
    }

    private static string GenerateDocumentNumber(string prefix) =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

    private static string? NormalizeRequired(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<Result> EnsurePurchasingEnabledAsync(Guid tenantId, CancellationToken ct) =>
        await featureService.EnsureEnabledAsync(
            tenantId,
            TenantModuleFeatureKeys.Inventario,
            TenantModuleFeatureKeys.PurchasingSubFeature,
            ct);

    private static string ComputeRequestHash(Guid tenantId, ReceiveInventoryRequest request)
    {
        var hashPayload = new
        {
            tenantId,
            request.ReceiptNumber,
            request.PurchaseOrderId,
            request.WarehouseId,
            request.LocationId,
            request.SupplierId,
            request.ReceivedAt,
            request.ReferenceNumber,
            request.Notes,
            lines = request.Lines.Select(x => new
            {
                x.PurchaseOrderLineId,
                x.ProductId,
                x.ProductVariationId,
                x.Quantity,
                x.UnitCost,
                x.UnitOfMeasure,
                x.LotId,
                x.LotNumber,
                x.SupplierReference,
                x.ManufacturedAt,
                x.ExpiresAt
            }).ToList()
        };

        var json = JsonSerializer.Serialize(hashPayload, HashJsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}
