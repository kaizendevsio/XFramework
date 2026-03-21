using Community.Domain.Shared.Contracts.Requests;
using Microsoft.Extensions.Logging;
using XFramework.Core.Patterns;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;
using Community.Domain.Shared.Enums;
using XFramework.Core.Loggers;
using System.Net;
using Mapster;

namespace Community.Api.Services;

/// <summary>
/// Service for managing community operations including identity management and connections.
/// Consolidates all community operation logic previously handled by MediatR command handlers.
/// </summary>
public sealed class CommunityService : ICommunityService
{
    private readonly IDataContext _dataContext;
    private readonly ITenantService _tenantService;
    private readonly ILogger<CommunityService> _logger;

    public CommunityService(
        IDataContext dataContext,
        ITenantService tenantService,
        ILogger<CommunityService> logger)
    {
        _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> CreateCommunityIdentityAsync(
        CreateCommunityIdentityRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId);

            // Fetch credential with identity info
            var credential = await _dataContext.Query<IdentityCredential>()
                .Where(i => i.TenantId == tenant.Id)
                .Include(i => i.IdentityInfo)
                .Where(i => i.Id == request.CredentialId)
                .FirstOrDefaultAsync(cancellationToken);

            if (credential == null)
            {
                _logger.CommunityCredentialNotFound(request.CredentialId);
                return Result<CmdResponse>.NotFound($"Credential with Id {request.CredentialId} does not exist");
            }

            // Fetch community identity type
            var communityIdentityType = await _dataContext.Query<CommunityIdentityType>()
                .Where(i => i.Id == request.CommunityIdentityTypeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (communityIdentityType == null)
            {
                _logger.CommunityIdentityTypeNotFound(request.CommunityIdentityTypeId);
                return Result<CmdResponse>.NotFound($"Community identity entity with Id {request.CommunityIdentityTypeId} does not exist");
            }

            // Fetch file types
            var identityFileTypes = await _dataContext.Query<CommunityIdentityFileType>().ToListAsync(cancellationToken);
            var storageFileTypes = await _dataContext.Query<StorageFileType>().ToListAsync(cancellationToken);

            var pngType = storageFileTypes.FirstOrDefault(i => i.Id == new Guid("af6b9396-ba01-4f88-a5d0-e0cfbc038146"));

            // Create community identity entity
            var entity = new CommunityIdentity
            {
                Credential = credential,
                HandleName = string.IsNullOrEmpty(request.HandleName)
                    ? $"{credential.IdentityInfo.FirstName} {credential.IdentityInfo.LastName}"
                    : request.HandleName,
                Tagline = request.Tagline,
                Alias = request.Alias,
                Status = (int)CommunityIdentityStatus.Active,
                LastActive = DateTime.UtcNow,
                Type = communityIdentityType,
                CommunityIdentityFiles =
                {
                    // Profile Photo
                    new()
                    {
                        Type = identityFileTypes.FirstOrDefault(i => i.Id == new Guid("996dd417-170c-4ac9-b565-62caf4ab5ccf")),
                        Storage = new()
                        {
                            ContentPath = "",
                            Type = pngType
                        }
                    },
                    // Cover Photo
                    new()
                    {
                        Type = identityFileTypes.FirstOrDefault(i => i.Id == new Guid("8716ec30-b061-45cc-ad5b-77bda960d90e")),
                        Storage = new()
                        {
                            ContentPath = "",
                            Type = pngType
                        }
                    }
                }
            };

            _dataContext.Add(entity);
            await _dataContext.SaveChangesAsync(cancellationToken);

            _logger.CommunityIdentityCreated(request.CredentialId);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = "Community identity created successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.CommunityIdentityCreationError(request.CredentialId, ex);
            return Result<CmdResponse>.Failure("An error occurred while creating community identity", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> UpdateCommunityIdentityAsync(
        UpdateCommunityIdentityRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Fetch existing community identity
            var communityIdentity = await _dataContext.Query<CommunityIdentity>()
                .Where(i => i.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (communityIdentity == null)
            {
                _logger.CommunityIdentityNotFound(request.Id);
                return Result<CmdResponse>.NotFound($"Community identity with id {request.Id} does not exist");
            }

            // Map request to entity using Mapster
            communityIdentity = request.Adapt(communityIdentity);

            // Update credential if provided
            if (request.CredentialId != Guid.Empty)
            {
                var credential = await _dataContext.Query<IdentityCredential>()
                    .Where(i => i.Id == request.CredentialId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (credential == null)
                {
                    _logger.CommunityCredentialNotFound(request.CredentialId);
                    return Result<CmdResponse>.NotFound($"Credential with id {request.CredentialId} does not exist");
                }

                communityIdentity.Credential = credential;
            }

            // Update community identity type if provided
            if (request.CommunityIdentityTypeId != Guid.Empty)
            {
                var communityIdentityType = await _dataContext.Query<CommunityIdentityType>()
                    .Where(i => i.Id == request.CommunityIdentityTypeId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (communityIdentityType == null)
                {
                    _logger.CommunityIdentityTypeNotFound(request.CommunityIdentityTypeId);
                    return Result<CmdResponse>.NotFound($"Community Identity Type with id {request.CommunityIdentityTypeId} does not exist");
                }

                communityIdentity.Type = communityIdentityType;
            }

            _dataContext.Update(communityIdentity);
            await _dataContext.SaveChangesAsync(cancellationToken);

            _logger.CommunityIdentityUpdated(request.Id);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = "Community identity updated successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.CommunityIdentityUpdateError(request.Id, ex);
            return Result<CmdResponse>.Failure("An error occurred while updating community identity", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<CommunityConnection>>> GetConnectionListAsync(
        GetCommunityConnectionListRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Fetch connection type
            var connectionType = await _dataContext.Query<CommunityConnectionType>()
                .Where(i => i.Id == request.ConnectionTypeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (connectionType == null)
            {
                _logger.CommunityConnectionTypeNotFound(request.ConnectionTypeId);
                return Result<List<CommunityConnection>>.NotFound($"Connection entity with id {request.ConnectionTypeId} does not exist");
            }

            // Fetch community identity
            var communityIdentity = await _dataContext.Query<CommunityIdentity>()
                .Where(i => i.Id == request.CommunityIdentityId)
                .FirstOrDefaultAsync(cancellationToken);

            if (communityIdentity == null)
            {
                _logger.CommunityIdentityNotFound(request.CommunityIdentityId);
                return Result<List<CommunityConnection>>.NotFound($"Community identity with id {request.CommunityIdentityId} does not exist");
            }

            // Fetch connection list
            var connectionList = await _dataContext.Query<CommunityConnection>()
                .Include(i => i.SourceSocialMediaIdentity)
                .Include(i => i.TargetSocialMediaIdentity)
                .Where(i => i.TypeId == connectionType.Id)
                .Where(i => i.SourceSocialMediaIdentityId == communityIdentity.Id ||
                           i.TargetSocialMediaIdentityId == communityIdentity.Id)
                .Take(request.Limit)
                .ToListAsync(cancellationToken);

            if (!connectionList.Any())
            {
                _logger.CommunityNoConnectionsFound(request.CommunityIdentityId);
                return Result<List<CommunityConnection>>.Success([]);
            }

            _logger.CommunityConnectionsRetrieved(connectionList.Count, request.CommunityIdentityId);

            return Result<List<CommunityConnection>>.Success(connectionList);
        }
        catch (Exception ex)
        {
            _logger.CommunityConnectionsError(request.CommunityIdentityId, ex);
            return Result<List<CommunityConnection>>.Failure("An error occurred while retrieving connections", 500);
        }
    }
}
