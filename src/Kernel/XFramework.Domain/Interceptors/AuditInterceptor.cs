using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using XFramework.Domain.Interfaces;

namespace XFramework.Domain.Interceptors;

/// <summary>
/// EF Core interceptor that automatically populates audit fields for entities implementing IAuditable.
/// This interceptor triggers on SaveChanges operations and sets CreatedAt/CreatedBy on inserts
/// and UpdatedAt/UpdatedBy on updates, extracting user context from HttpContext claims.
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the AuditInterceptor.
    /// </summary>
    /// <param name="httpContextAccessor">Accessor for retrieving the current HttpContext and user claims.</param>
    public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Intercepts SaveChangesAsync to apply audit field population before persisting changes.
    /// </summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null) 
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var entries = context.ChangeTracker.Entries<IAuditable>();
        var userId = GetCurrentUserId();
        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;
                    
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Intercepts synchronous SaveChanges to apply audit field population before persisting changes.
    /// </summary>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        var context = eventData.Context;
        if (context == null) 
            return base.SavingChanges(eventData, result);

        var entries = context.ChangeTracker.Entries<IAuditable>();
        var userId = GetCurrentUserId();
        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;
                    
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }

        return base.SavingChanges(eventData, result);
    }

    /// <summary>
    /// Extracts the current user ID from HttpContext claims.
    /// Attempts to retrieve the 'sub' claim first (standard for JWT tokens),
    /// falls back to NameIdentifier claim, and defaults to "System" for unauthenticated requests.
    /// </summary>
    /// <returns>The user ID string or "System" if no authenticated user.</returns>
    private string? GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            // Extract user ID from claims (adjust claim type as needed)
            return httpContext.User.FindFirst("sub")?.Value 
                ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.User.FindFirst("userId")?.Value;
        }
        
        return "System"; // Default for non-authenticated operations
    }
}