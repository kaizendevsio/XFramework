using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Inventario.Integration.Drivers;
using Microsoft.AspNetCore.Components;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Locations;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Warehouses;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.Portal.Shared;

namespace XFramework.Portal.Features.Inventario.Components;

public partial class InventarioSetupWizard
{
    [Parameter, EditorRequired] public InventarioSetupResponse State { get; set; } = default!;
    [Parameter] public string TenantName { get; set; } = "";
    [Parameter] public EventCallback<InventarioSetupResponse> Completed { get; set; }
    [Parameter] public EventCallback Cancelled { get; set; }
    [Inject] private IInventarioServiceWrapper Inventario { get; set; } = default!;
    [Inject] private IPortalTenantContext TenantContext { get; set; } = default!;
    private Guid _tenantId, _completionRequestId;
    private bool _saving, _disposed;
    private string? _error;
    private string _mode = "basic", _warehouseId = "", _locationId = "", _parentLocationId = "";
    private string _warehouseCode = "MAIN", _warehouseName = "Main Warehouse", _warehouseDescription = "", _address = "", _city = "", _region = "", _postalCode = "", _countryCode = "";
    private string _locationCode = "MAIN", _locationName = "Main Location", _locationDescription = "", _locationType = "Bin", _currency = "PHP";
    private int _threshold = 5;
    private bool _isDefault = true, _pickable = true;
    private bool Advanced => _mode == "advanced";
    private List<SelectOption<string>> WarehouseOptions => [new("", "New warehouse"), .. State.Warehouses.Where(x => x.IsEnabled).Select(x => new SelectOption<string>(x.Id.ToString(), $"{x.Code} - {x.Name}"))];
    private IEnumerable<InventoryLocation> WarehouseLocations => State.Locations.Where(x => x.IsEnabled && x.WarehouseId.ToString() == _warehouseId);
    private List<SelectOption<string>> LocationOptions => [new("", "New location"), .. WarehouseLocations.Select(x => new SelectOption<string>(x.Id.ToString(), $"{x.Code} - {x.Name}"))];
    private List<SelectOption<string>> ParentOptions => [new("", "No parent location"), .. WarehouseLocations.Select(x => new SelectOption<string>(x.Id.ToString(), $"{x.Code} - {x.Name}"))];
    private string WarehouseSummary => !State.WarehousingEnabled ? "Unchanged (feature disabled)" : State.Warehouses.FirstOrDefault(x => x.Id.ToString() == _warehouseId)?.Name ?? $"{_warehouseCode} - {_warehouseName} (new)";
    private string LocationSummary => !State.WarehousingEnabled ? "Unchanged (feature disabled)" : State.Locations.FirstOrDefault(x => x.Id.ToString() == _locationId)?.Name ?? $"{_locationCode} - {_locationName} (new)";

    protected override void OnParametersSet()
    {
        if (_tenantId == State.TenantId) return;
        _tenantId = State.TenantId;
        _completionRequestId = Guid.NewGuid();
        _mode = State.HasExistingConfiguration || !State.WarehousingEnabled || State.Mode == InventarioSetupMode.Advanced ? "advanced" : "basic";
        _threshold = State.LowStockThreshold;
        _currency = State.DefaultCurrency;
        var warehouse = State.Warehouses.FirstOrDefault(x => x.IsEnabled && x.IsDefault) ?? State.Warehouses.FirstOrDefault(x => x.IsEnabled);
        _warehouseId = warehouse?.Id.ToString() ?? "";
        _locationId = State.Locations.FirstOrDefault(x => x.IsEnabled && x.WarehouseId == warehouse?.Id)?.Id.ToString() ?? "";
    }

    private void SelectWarehouse(string value) { _warehouseId = value; _locationId = ""; _parentLocationId = ""; }
    private bool Check(bool valid, string message) { _error = valid ? null : message; return valid; }
    private bool ValidateMode() => Check(Advanced || (!State.HasExistingConfiguration && State.WarehousingEnabled), "Use advanced setup for existing configuration or disabled warehousing.");
    private bool ValidateWarehouse() => Check(!State.WarehousingEnabled || _warehouseId != "" || (!string.IsNullOrWhiteSpace(_warehouseCode) && _warehouseCode.Length <= 50 && !string.IsNullOrWhiteSpace(_warehouseName) && _warehouseName.Length <= 200), "Warehouse code and name are required.");
    private bool ValidateLocation() => Check(!State.WarehousingEnabled || _locationId != "" || (!string.IsNullOrWhiteSpace(_locationCode) && _locationCode.Length <= 50 && !string.IsNullOrWhiteSpace(_locationName) && _locationName.Length <= 200), "Location code and name are required.");
    private bool ValidateDefaults() => Check(_threshold >= 0 && CurrencyCatalog.GetAllCurrencyCodes().Contains(_currency.Trim().ToUpperInvariant()), "Enter a non-negative threshold and supported currency code.");
    private bool ValidateAll() => ValidateMode() && ValidateWarehouse() && ValidateLocation() && ValidateDefaults();
    private Task CancelAsync() => _saving ? Task.CompletedTask : Cancelled.InvokeAsync();

    private async Task ConfirmAsync()
    {
        if (_saving || _disposed || !State.CanManage || TenantContext.SelectedTenantId != _tenantId || !ValidateAll()) return;
        var tenantId = _tenantId;
        var request = new CompleteInventarioSetupRequest
        {
            Metadata = new() { RequestedTenantId = tenantId }, CompletionRequestId = _completionRequestId,
            ExpectedConcurrencyStamp = State.ConcurrencyStamp, Mode = Advanced ? InventarioSetupMode.Advanced : InventarioSetupMode.Basic,
            LowStockThreshold = _threshold, DefaultCurrency = _currency.Trim().ToUpperInvariant(),
            ExistingWarehouseId = State.WarehousingEnabled && Guid.TryParse(_warehouseId, out var w) ? w : null,
            ExistingLocationId = State.WarehousingEnabled && Guid.TryParse(_locationId, out var l) ? l : null,
            Warehouse = State.WarehousingEnabled && _warehouseId == "" ? new CreateWarehouseRequest { Code = _warehouseCode, Name = _warehouseName, Description = _warehouseDescription, AddressLine = _address, City = _city, Region = _region, PostalCode = _postalCode, CountryCode = _countryCode, IsDefault = _isDefault } : null,
            Location = State.WarehousingEnabled && _locationId == "" ? new CreateInventoryLocationRequest { Code = _locationCode, Name = _locationName, Description = _locationDescription, LocationType = Enum.Parse<InventoryLocationType>(_locationType), ParentLocationId = Guid.TryParse(_parentLocationId, out var p) ? p : null, IsPickable = _pickable } : null
        };
        _saving = true;
        try
        {
            var result = await Inventario.CompleteInventarioSetup(request);
            if (_disposed || TenantContext.SelectedTenantId != tenantId || _tenantId != tenantId) return;
            if (result.IsSuccess && result.Response is { } response && response.TenantId == tenantId) await Completed.InvokeAsync(response);
            else _error = result.HttpStatusCode == System.Net.HttpStatusCode.Conflict ? "Configuration changed. Reload setup before confirming." : "Setup could not be saved. Check the details and try again.";
        }
        catch { if (!_disposed && TenantContext.SelectedTenantId == tenantId) _error = "Setup could not be saved. Try again."; }
        finally { if (!_disposed && _tenantId == tenantId) _saving = false; }
    }

    public void Dispose() => _disposed = true;
}
