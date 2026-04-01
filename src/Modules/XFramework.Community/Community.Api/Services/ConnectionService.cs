using Community.Domain.Shared.Contracts;
using Community.Domain.Shared.Contracts.Requests;
using Microsoft.Extensions.Logging;
using XFramework.Core.Loggers;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Community.Api.Services;

/// <summary>
/// Service for managing community connections (follow/unfollow/block).
/// </summary>
public sealed class ConnectionService : IConnectionService
{
    private readonly IDataContext _dataContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ConnectionService> _logger;

    public ConnectionService(
        IDataContext dataContext,
        INotificationService notificationService,
        ILogger<ConnectionService> logger)
    {
        _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> CreateConnectionAsync(
        CreateConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Prevent self-connections
            if (request.SourceIdentityId == request.TargetIdentityId)
            {
                return Result<CmdResponse>.Failure("Cannot create a connection to yourself", 400);
            }

            // Check if either party has blocked the other
            if (await IsBlockedAsync(request.SourceIdentityId, request.TargetIdentityId, cancellationToken))
                return Result<CmdResponse>.Forbidden("Cannot create connection — a block exists between these identities");

            // Validate source identity exists
            var sourceIdentity = await _dataContext.Query<CommunityIdentity>()
                .Where(i => i.Id == request.SourceIdentityId)
                .FirstOrDefaultAsync(cancellationToken);

            if (sourceIdentity == null)
            {
                _logger.CommunityIdentityNotFound(request.SourceIdentityId);
                return Result<CmdResponse>.NotFound($"Source identity with Id {request.SourceIdentityId} does not exist");
            }

            // Validate target identity exists
            var targetIdentity = await _dataContext.Query<CommunityIdentity>()
                .Where(i => i.Id == request.TargetIdentityId)
                .FirstOrDefaultAsync(cancellationToken);

            if (targetIdentity == null)
            {
                _logger.CommunityIdentityNotFound(request.TargetIdentityId);
                return Result<CmdResponse>.NotFound($"Target identity with Id {request.TargetIdentityId} does not exist");
            }

            // Validate connection type exists
            var connectionType = await _dataContext.Query<CommunityConnectionType>()
                .Where(i => i.Id == request.TypeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (connectionType == null)
            {
                _logger.CommunityConnectionTypeNotFound(request.TypeId);
                return Result<CmdResponse>.NotFound($"Connection type with Id {request.TypeId} does not exist");
            }

            // Check for duplicate connections (same source + target + type)
            var existingConnection = await _dataContext.Query<CommunityConnection>()
                .Where(i => i.SourceSocialMediaIdentityId == request.SourceIdentityId)
                .Where(i => i.TargetSocialMediaIdentityId == request.TargetIdentityId)
                .Where(i => i.TypeId == request.TypeId)
                .Where(i => !i.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingConnection != null)
            {
                return Result<CmdResponse>.Conflict("A connection of this type already exists between these identities");
            }

            // Create the connection
            var entity = new CommunityConnection
            {
                SourceSocialMediaIdentityId = request.SourceIdentityId,
                TargetSocialMediaIdentityId = request.TargetIdentityId,
                TypeId = request.TypeId,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            };

            _dataContext.Add(entity);
            await _dataContext.SaveChangesAsync(cancellationToken);

            _logger.CommunityConnectionCreated(entity.Id, request.SourceIdentityId, request.TargetIdentityId);

            // Create a follow notification for the target identity
            await _notificationService.CreateNotificationAsync(
                request.TargetIdentityId,
                request.SourceIdentityId,
                Community.Domain.Shared.Enums.NotificationType.Follow,
                entity.Id,
                $"{sourceIdentity.HandleName ?? "Someone"} started following you",
                cancellationToken);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Created,
                Message = $"Connection created successfully with Id {entity.Id}"
            }, 201);
        }
        catch (Exception ex)
        {
            _logger.CommunityConnectionCreateError(request.SourceIdentityId, request.TargetIdentityId, ex);
            return Result<CmdResponse>.Failure("An error occurred while creating the connection", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> DeleteConnectionAsync(
        DeleteConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Find the connection
            var connection = await _dataContext.Query<CommunityConnection>()
                .Where(i => i.Id == request.Id)
                .Where(i => !i.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (connection == null)
            {
                return Result<CmdResponse>.NotFound($"Connection with Id {request.Id} does not exist");
            }

            // Validate the requester owns the connection (is the source)
            if (connection.SourceSocialMediaIdentityId != request.RequestingIdentityId)
            {
                return Result<CmdResponse>.Forbidden("You can only delete your own connections");
            }

            // Soft delete
            connection.IsDeleted = true;
            connection.DeletedAt = DateTime.UtcNow;
            connection.IsEnabled = false;

            _dataContext.Update(connection);
            await _dataContext.SaveChangesAsync(cancellationToken);

            _logger.CommunityConnectionDeleted(request.Id);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Connection deleted successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.CommunityConnectionDeleteError(request.Id, ex);
            return Result<CmdResponse>.Failure("An error occurred while deleting the connection", 500);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsBlockedAsync(Guid identityA, Guid identityB, CancellationToken cancellationToken = default)
    {
        return await _dataContext.Query<CommunityConnection>()
            .Where(c => c.TypeId == Community.Domain.Shared.CommunityConnectionTypes.Block)
            .Where(c => !c.IsDeleted && c.IsEnabled)
            .Where(c =>
                (c.SourceSocialMediaIdentityId == identityA && c.TargetSocialMediaIdentityId == identityB) ||
                (c.SourceSocialMediaIdentityId == identityB && c.TargetSocialMediaIdentityId == identityA))
            .AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<HashSet<Guid>> GetBlockedIdentityIdsAsync(Guid identityId, CancellationToken cancellationToken = default)
    {
        var blocked = await _dataContext.Query<CommunityConnection>()
            .Where(c => c.TypeId == Community.Domain.Shared.CommunityConnectionTypes.Block)
            .Where(c => !c.IsDeleted && c.IsEnabled)
            .Where(c => c.SourceSocialMediaIdentityId == identityId || c.TargetSocialMediaIdentityId == identityId)
            .ToListAsync(cancellationToken);

        return blocked.Select(c => c.SourceSocialMediaIdentityId == identityId
            ? c.TargetSocialMediaIdentityId
            : c.SourceSocialMediaIdentityId).ToHashSet();
    }
}
