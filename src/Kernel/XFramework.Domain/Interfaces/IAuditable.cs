namespace XFramework.Domain.Interfaces;

/// <summary>
/// Interface for entities that require automatic audit tracking.
/// Implementing this interface enables the AuditInterceptor to automatically
/// populate audit fields (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy) on save operations.
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// Timestamp when the entity was created.
    /// Automatically populated by AuditInterceptor on insert.
    /// </summary>
    DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Timestamp when the entity was last updated.
    /// Automatically populated by AuditInterceptor on update.
    /// </summary>
    DateTime? UpdatedAt { get; set; }
    
    /// <summary>
    /// User ID of the entity creator.
    /// Automatically populated by AuditInterceptor on insert from HttpContext claims.
    /// </summary>
    string? CreatedBy { get; set; }
    
    /// <summary>
    /// User ID of the last modifier.
    /// Automatically populated by AuditInterceptor on update from HttpContext claims.
    /// </summary>
    string? UpdatedBy { get; set; }
}