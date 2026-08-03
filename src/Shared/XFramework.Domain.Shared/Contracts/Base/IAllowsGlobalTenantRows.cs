namespace XFramework.Domain.Shared.Contracts.Base;

/// <summary>
/// Marks tenant-scoped reference entities whose immutable global seed rows use <see cref="Guid.Empty"/>.
/// </summary>
public interface IAllowsGlobalTenantRows;
