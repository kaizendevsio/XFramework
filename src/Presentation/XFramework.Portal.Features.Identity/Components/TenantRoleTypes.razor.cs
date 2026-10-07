using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Integration.Drivers;
using MemoryPack;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.DataContext;
using XFramework.Portal.Shared;

namespace XFramework.Portal.Features.Identity.Components;

public partial class TenantRoleTypes
{
    [Parameter] public Guid TenantId { get; set; }
    [Parameter] public string? TenantName { get; set; }
    [Inject] private IServiceProvider Services { get; set; } = default!;
    [Inject] private IIdentityServerServiceWrapper IdentityServer { get; set; } = default!;
    [Inject] private IPortalTenantContext TenantContext { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ToastService ToastService { get; set; } = default!;
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private List<IdentityRoleType> _roles = [];
    private List<IdentityRoleTypeGroup> _groups = [];
    private Guid? _loadedTenantId;
    private Guid _draftTenantId;
    private Guid? _draftProfileTenantId;
    private int _contextRevision;
    private int _draftRevision;
    private bool _disposed;
    private bool _accessChecked;
    private bool _canManage;
    private bool _loading;
    private bool _loadFailed;
    private bool _saving;
    private bool _roleOpen;
    private bool _groupOpen;
    private bool _returnToRole;
    private string _roleName = "";
    private short _roleLevel;
    private string _groupId = "";
    private string _groupName = "";
    private string _groupDescription = "";
    private string? _roleError;
    private string? _groupError;

    private List<SelectOption<string>> GroupOptions => _groups
        .Where(group => group.TenantId == TenantId && group.IsEnabled && !group.IsDeleted)
        .Select(group => new SelectOption<string>(group.Id.ToString(), group.Name)).ToList();

    protected override void OnInitialized() => TenantContext.OnChanged += OnTenantContextChanged;

    protected override async Task OnParametersSetAsync()
    {
        await ResolveAccessAsync();
        if (!_canManage || TenantId == Guid.Empty)
        {
            InvalidateDrafts();
            _roles = [];
            _groups = [];
            _loadedTenantId = null;
            return;
        }
        if (_loadedTenantId == TenantId) return;
        InvalidateDrafts();
        _roles = [];
        _groups = [];
        _loadedTenantId = TenantId;
        await ReloadRolesAsync();
    }

    private async Task ResolveAccessAsync()
    {
        _canManage = AuthenticationStateTask is not null
            && PortalAccess.CanManageTenants((await AuthenticationStateTask).User);
        _accessChecked = true;
    }

    private void OnTenantContextChanged()
    {
        // Invalidate synchronously so a queued save cannot use the old draft.
        InvalidateDrafts();
        _ = InvokeAsync(async () =>
        {
            if (_disposed) return;
            StateHasChanged();
            await ReloadRolesAsync();
            if (!_disposed) StateHasChanged();
        });
    }

    private void InvalidateDrafts()
    {
        _contextRevision++;
        _roleOpen = _groupOpen = _returnToRole = false;
        _roleError = _groupError = null;
    }

    private bool IsCurrent(int revision, Guid tenantId) =>
        !_disposed && revision == _contextRevision && tenantId == TenantId;

    private bool DraftIsCurrent => IsCurrent(_draftRevision, _draftTenantId)
        && _draftTenantId != Guid.Empty && _draftProfileTenantId == TenantContext.SelectedTenantId;

    private void CaptureDraft()
    {
        _draftTenantId = TenantId;
        _draftProfileTenantId = TenantContext.SelectedTenantId;
        _draftRevision = _contextRevision;
    }

    private async Task ReloadRolesAsync()
    {
        var tenantId = TenantId;
        var revision = _contextRevision;
        await ResolveAccessAsync();
        if (!_canManage || tenantId == Guid.Empty || !IsCurrent(revision, tenantId)) return;
        _loading = true;
        _loadFailed = false;
        try
        {
            // Explicit metadata keeps an All Tenants/profile selection out of the route's read scope.
            var reads = new RemoteDataContext(Services, new RequestMetadata { RequestedTenantId = tenantId });
            var roles = await reads.Query<IdentityRoleType>().IgnoreQueryFilters().NoCache()
                .Where(role => role.TenantId == tenantId && !role.IsDeleted)
                .Include(role => role.Group).OrderBy(role => role.Name).Take(1_000).ToListAsync();
            var groups = await reads.Query<IdentityRoleTypeGroup>().IgnoreQueryFilters().NoCache()
                .Where(group => group.TenantId == tenantId && !group.IsDeleted && group.IsEnabled)
                .OrderBy(group => group.Name).Take(1_000).ToListAsync();
            if (!IsCurrent(revision, tenantId)) return;
            _roles = roles;
            _groups = groups;
        }
        catch
        {
            if (!IsCurrent(revision, tenantId)) return;
            _roles = [];
            _groups = [];
            _loadFailed = true;
        }
        finally
        {
            if (IsCurrent(revision, tenantId)) _loading = false;
        }
    }

    private void OpenRoleDialog()
    {
        if (!_canManage || _loading || _saving || _loadFailed || TenantId == Guid.Empty) return;
        CaptureDraft();
        _roleName = _groupId = "";
        _roleLevel = 0;
        _roleError = null;
        _roleOpen = true;
    }

    private void OpenGroupDialog(bool fromRole)
    {
        if (!_canManage || _loading || _saving || _loadFailed || TenantId == Guid.Empty) return;
        if (fromRole && (!_roleOpen || !DraftIsCurrent)) return;
        if (!fromRole) CaptureDraft();
        _returnToRole = fromRole;
        _roleOpen = false;
        _groupName = _groupDescription = "";
        _groupError = null;
        _groupOpen = true;
    }

    private void SetRoleOpen(bool open)
    {
        if (!_saving) _roleOpen = open && DraftIsCurrent;
    }

    private void SetGroupOpen(bool open)
    {
        if (_saving) return;
        _groupOpen = open && DraftIsCurrent;
        if (!_groupOpen)
        {
            _roleOpen = _returnToRole && DraftIsCurrent;
            _returnToRole = false;
        }
    }

    private async Task<bool> CanSaveAsync()
    {
        if (_saving || !DraftIsCurrent) return false;
        await ResolveAccessAsync();
        if (!_canManage) InvalidateDrafts();
        return _canManage && !_saving && DraftIsCurrent;
    }

    private async Task SaveRoleAsync()
    {
        if (!_roleOpen || !await CanSaveAsync()) return;
        _roleError = null;
        var name = _roleName.Trim();
        if (name.Length is 0 or > 100)
        {
            _roleError = "Name is required and must be 100 characters or fewer.";
            return;
        }
        if (!Guid.TryParse(_groupId, out var groupId) || !_groups.Any(group =>
                group.Id == groupId && group.TenantId == _draftTenantId && group.IsEnabled && !group.IsDeleted))
        {
            _roleError = "Choose an active role group belonging to this tenant.";
            return;
        }
        var role = new IdentityRoleType
        {
            Id = Guid.NewGuid(), TenantId = _draftTenantId, Name = name,
            GroupId = groupId, RoleLevel = _roleLevel, IsEnabled = true,
            SystemReferenceId = Guid.NewGuid()
        };
        _saving = true;
        try
        {
            var result = await CreateAsync(role);
            if (!DraftIsCurrent) return;
            if (!result.IsSuccess) { _roleError = SaveError(result); return; }
            _roleOpen = false;
            await ReloadRolesAsync();
            if (!DraftIsCurrent) return;
            ToastService.Success("Role type created.", "Saved");
            ConfigureRole(role.Id);
        }
        catch
        {
            if (DraftIsCurrent) _roleError = "Role type could not be created. Reload before trying again.";
        }
        finally { _saving = false; }
    }

    private async Task SaveGroupAsync()
    {
        if (!_groupOpen || !await CanSaveAsync()) return;
        _groupError = null;
        var name = _groupName.Trim();
        if (name.Length == 0) { _groupError = "Name is required."; return; }
        var group = new IdentityRoleTypeGroup
        {
            Id = Guid.NewGuid(), TenantId = _draftTenantId, Name = name,
            Description = _groupDescription.Trim(), IsEnabled = true, SystemReferenceId = Guid.NewGuid()
        };
        _saving = true;
        try
        {
            var result = await CreateAsync(group);
            if (!DraftIsCurrent) return;
            if (!result.IsSuccess) { _groupError = SaveError(result); return; }
            _groupId = group.Id.ToString();
            await ReloadRolesAsync();
            if (!DraftIsCurrent) return;
            // Include the new group even when the bounded lookup page was full or refresh failed.
            if (!_groups.Any(item => item.Id == group.Id)) _groups.Add(group);
            _groupOpen = false;
            _roleOpen = _returnToRole;
            _returnToRole = false;
            ToastService.Success("Role group created.", "Saved");
        }
        catch
        {
            if (DraftIsCurrent) _groupError = "Role group could not be created. Reload before trying again.";
        }
        finally { _saving = false; }
    }

    private async Task<DataContextResult> CreateAsync<T>(T entity) where T : BaseModel
    {
        // Role definitions use IdentityServer's existing allowlisted mutation contract, not dedicated create requests.
        var request = new SaveChangesRequest
        {
            Metadata = new RequestMetadata { RequestId = Guid.NewGuid(), RequestedTenantId = entity.TenantId },
            Changes = [new ChangeEntry
            {
                EntityTypeName = typeof(T).Name, Operation = ChangeOperation.Add,
                SerializedEntity = MemoryPackSerializer.Serialize(entity)
            }]
        };
        var bytes = await IdentityServer.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request));
        return MemoryPackSerializer.Deserialize<DataContextResult>(bytes)
            ?? DataContextResult.Failure("No response was received.");
    }

    private static string SaveError(DataContextResult result) => result.StatusCode switch
    {
        401 or 403 => "You are not authorized to create roles or groups for this tenant.",
        409 => "A conflicting record already exists. Reload and check the existing records.",
        400 or 422 => "The server rejected this record. Check the name and tenant role group, then reload.",
        _ => "The record could not be created. Reload before trying again."
    };

    private string GroupLabel(IdentityRoleType role) =>
        _groups.FirstOrDefault(group => group.Id == role.GroupId)?.Name ?? role.Group?.Name ?? "Unknown group";

    private void ConfigureRole(Guid roleId) => Navigation.NavigateTo($"/identity/tenants/{TenantId}/role-types/{roleId}");

    public void Dispose()
    {
        _disposed = true;
        InvalidateDrafts();
        TenantContext.OnChanged -= OnTenantContextChanged;
    }
}
