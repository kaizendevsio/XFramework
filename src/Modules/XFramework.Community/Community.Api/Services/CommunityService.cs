using Community.Domain.Shared;
using XFramework.Domain.Shared.DataContext;
using XFramework.Core.Loggers;
using Storage.Domain.Shared.Contracts.Requests;
using Storage.Integration.Drivers;

namespace Community.Api.Services;

/// <summary>
/// Service for managing community operations including identity management and connections.
/// Consolidates all community operation logic previously handled by MediatR command handlers.
/// </summary>
public sealed class CommunityService : ICommunityService
{
    private readonly IDataContext _dataContext;
    private readonly ICommunityRequestContext _requestContext;
    private readonly IStorageServiceWrapper _storageServiceWrapper;
    private readonly ILogger<CommunityService> _logger;

    public CommunityService(
        IDataContext dataContext,
        ICommunityRequestContext requestContext,
        IStorageServiceWrapper storageServiceWrapper,
        ILogger<CommunityService> logger)
    {
        _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        _requestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
        _storageServiceWrapper = storageServiceWrapper ?? throw new ArgumentNullException(nameof(storageServiceWrapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> CreateCommunityIdentityAsync(
        CreateCommunityIdentityRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requesterResult = await _requestContext.GetRequiredAsync(request.Metadata, cancellationToken);
            if (!requesterResult.IsSuccess)
                return CommunitySecurity.ToFailure<CommunityRequester, CmdResponse>(requesterResult);

            var requester = requesterResult.Data!;
            if (CommunitySecurity.IsSpoofed(request.CredentialId, requester.CredentialId))
                return Result<CmdResponse>.Forbidden("Credential ID does not match authenticated user");

            var existingIdentity = await _dataContext.Query<CommunityIdentity>()
                .Where(i => i.CredentialId == requester.CredentialId)
                .Where(i => i.TenantId == requester.TenantId)
                .Where(i => !i.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingIdentity is not null)
                return Result<CmdResponse>.Conflict("Authenticated credential already has a community identity");

            // Fetch credential with identity info
            var credential = await _dataContext.Query<IdentityCredential>()
                .Where(i => i.TenantId == requester.TenantId)
                .Include(i => i.IdentityInfo)
                .Where(i => i.Id == requester.CredentialId)
                .FirstOrDefaultAsync(cancellationToken);

            if (credential == null)
            {
                _logger.CommunityCredentialNotFound(requester.CredentialId);
                return Result<CmdResponse>.NotFound($"Credential with Id {requester.CredentialId} does not exist");
            }

            // Fetch community identity type
            var communityIdentityType = await _dataContext.Query<CommunityIdentityType>()
                .Where(i => i.TenantId == requester.TenantId)
                .Where(i => i.Id == request.CommunityIdentityTypeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (communityIdentityType == null)
            {
                _logger.CommunityIdentityTypeNotFound(request.CommunityIdentityTypeId);
                return Result<CmdResponse>.NotFound($"Community identity entity with Id {request.CommunityIdentityTypeId} does not exist");
            }

            // Fetch file types
            var identityFileTypes = await _dataContext.Query<CommunityIdentityFileType>().ToListAsync(cancellationToken);

            var profilePhotoType = identityFileTypes.FirstOrDefault(i => i.Id == CommunityIdentityFileTypes.ProfilePhoto);
            var coverPhotoType = identityFileTypes.FirstOrDefault(i => i.Id == CommunityIdentityFileTypes.CoverPhoto);

            if (profilePhotoType is null || coverPhotoType is null)
            {
                _logger.LogError(
                    "Required community identity file seed data is missing. ProfilePhotoTypeFound={ProfilePhotoTypeFound}, CoverPhotoTypeFound={CoverPhotoTypeFound}",
                    profilePhotoType is not null,
                    coverPhotoType is not null);

                return Result<CmdResponse>.Failure("Required community identity file seed data is missing", 500);
            }

            var fallbackHandleName = credential.IdentityInfo is null
                ? "Community User"
                : $"{credential.IdentityInfo.FirstName} {credential.IdentityInfo.LastName}".Trim();

            // Create community identity entity
            var entity = new CommunityIdentity
            {
                TenantId = requester.TenantId,
                CredentialId = requester.CredentialId,
                Credential = credential,
                HandleName = string.IsNullOrWhiteSpace(request.HandleName)
                    ? fallbackHandleName
                    : request.HandleName.Trim(),
                Tagline = request.Tagline,
                Alias = request.Alias,
                Status = (int)CommunityIdentityStatus.Active,
                LastActive = DateTime.UtcNow,
                Type = communityIdentityType
            };

            _dataContext.Add(entity);
            var saveResult = await _dataContext.SaveChangesAsync(cancellationToken);
            if (CommunitySecurity.SaveFailure(saveResult, "CreateCommunityIdentity") is { } saveFailure)
                return saveFailure;

            _logger.CommunityIdentityCreated(requester.CredentialId);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = "Community identity created successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.CommunityIdentityCreationError(request.CredentialId == Guid.Empty ? Guid.Empty : request.CredentialId, ex);
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
            var requesterResult = await _requestContext.GetRequiredIdentityAsync(request.Metadata, cancellationToken);
            if (!requesterResult.IsSuccess)
                return CommunitySecurity.ToFailure<CommunityRequester, CmdResponse>(requesterResult);

            var requester = requesterResult.Data!;
            if (requester.IdentityId != request.Id)
                return Result<CmdResponse>.Forbidden("You can only update your own community identity");

            if (CommunitySecurity.IsSpoofed(request.CredentialId, requester.CredentialId))
                return Result<CmdResponse>.Forbidden("Credential ID does not match authenticated user");

            // Fetch existing community identity
            var communityIdentity = await _dataContext.Query<CommunityIdentity>()
                .Where(i => i.TenantId == requester.TenantId)
                .Where(i => i.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (communityIdentity == null)
            {
                _logger.CommunityIdentityNotFound(request.Id);
                return Result<CmdResponse>.NotFound($"Community identity with id {request.Id} does not exist");
            }

            // Update community identity type if provided
            if (request.CommunityIdentityTypeId != Guid.Empty)
            {
                var communityIdentityType = await _dataContext.Query<CommunityIdentityType>()
                    .Where(i => i.TenantId == requester.TenantId)
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
            var saveResult = await _dataContext.SaveChangesAsync(cancellationToken);
            if (CommunitySecurity.SaveFailure(saveResult, "UpdateCommunityIdentity") is { } saveFailure)
                return saveFailure;

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
            var requesterResult = await _requestContext.GetRequiredIdentityAsync(request.Metadata, cancellationToken);
            if (!requesterResult.IsSuccess)
                return CommunitySecurity.ToFailure<CommunityRequester, List<CommunityConnection>>(requesterResult);

            var requester = requesterResult.Data!;
            var requestedIdentityId = request.CommunityIdentityId == Guid.Empty
                ? requester.IdentityId
                : request.CommunityIdentityId;

            if (requestedIdentityId != requester.IdentityId)
                return Result<List<CommunityConnection>>.Forbidden("You can only retrieve your own connections");

            // Fetch connection type
            var connectionType = await _dataContext.Query<CommunityConnectionType>()
                .Where(i => i.TenantId == requester.TenantId)
                .Where(i => i.Id == request.ConnectionTypeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (connectionType == null)
            {
                _logger.CommunityConnectionTypeNotFound(request.ConnectionTypeId);
                return Result<List<CommunityConnection>>.NotFound($"Connection entity with id {request.ConnectionTypeId} does not exist");
            }

            // Fetch community identity
            var communityIdentity = await _dataContext.Query<CommunityIdentity>()
                .Where(i => i.TenantId == requester.TenantId)
                .Where(i => i.Id == requestedIdentityId)
                .FirstOrDefaultAsync(cancellationToken);

            if (communityIdentity == null)
            {
                _logger.CommunityIdentityNotFound(requestedIdentityId);
                return Result<List<CommunityConnection>>.NotFound($"Community identity with id {requestedIdentityId} does not exist");
            }

            // Fetch connection list
            var connectionList = await _dataContext.Query<CommunityConnection>()
                .Include(i => i.SourceSocialMediaIdentity)
                .Include(i => i.TargetSocialMediaIdentity)
                .Where(i => i.TenantId == requester.TenantId)
                .Where(i => i.TypeId == connectionType.Id)
                .Where(i => i.SourceSocialMediaIdentityId == communityIdentity.Id ||
                           i.TargetSocialMediaIdentityId == communityIdentity.Id)
                .Take(request.Limit)
                .ToListAsync(cancellationToken);

            if (!connectionList.Any())
            {
                _logger.CommunityNoConnectionsFound(requestedIdentityId);
                return Result<List<CommunityConnection>>.Success([]);
            }

            _logger.CommunityConnectionsRetrieved(connectionList.Count, requestedIdentityId);

            return Result<List<CommunityConnection>>.Success(connectionList);
        }
        catch (Exception ex)
        {
            _logger.CommunityConnectionsError(request.CommunityIdentityId, ex);
            return Result<List<CommunityConnection>>.Failure("An error occurred while retrieving connections", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<CmdResponse>> UpdateIdentityFileAsync(
        UpdateIdentityFileRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requesterResult = await _requestContext.GetRequiredIdentityAsync(request.Metadata, cancellationToken);
            if (!requesterResult.IsSuccess)
                return CommunitySecurity.ToFailure<CommunityRequester, CmdResponse>(requesterResult);

            var requester = requesterResult.Data!;
            if (CommunitySecurity.IsSpoofed(request.IdentityId, requester.IdentityId)
                || CommunitySecurity.IsSpoofed(request.RequestingIdentityId, requester.IdentityId))
            {
                return Result<CmdResponse>.Forbidden("You can only update your own identity files");
            }

            var storageFileResult = await _storageServiceWrapper.ValidateStorageFileReference(new ValidateStorageFileReferenceRequest
            {
                Metadata = request.Metadata,
                StorageFileId = request.StorageFileId,
                RequireAvailable = true
            });

            if (!storageFileResult.IsSuccess || storageFileResult.Response is null)
                return Result<CmdResponse>.NotFound($"Storage file with Id {request.StorageFileId} does not exist");

            if (!storageFileResult.Response.IsValid)
                return Result<CmdResponse>.Failure(storageFileResult.Response.Message ?? "Storage file is not available", 400);

            var file = await _dataContext.Query<CommunityIdentityFile>()
                .Where(f => f.TenantId == requester.TenantId)
                .Where(f => f.Id == request.FileId && f.IdentityId == requester.IdentityId && !f.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (file == null)
                return Result<CmdResponse>.NotFound($"Identity file with Id {request.FileId} does not exist");

            file.StorageId = request.StorageFileId;
            file.ModifiedAt = DateTime.UtcNow;

            _dataContext.Update(file);
            var saveResult = await _dataContext.SaveChangesAsync(cancellationToken);
            if (CommunitySecurity.SaveFailure(saveResult, "UpdateIdentityFile") is { } saveFailure)
                return saveFailure;

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Identity file updated successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("UpdateIdentityFile", "CommunityIdentityFile", request.FileId, ex.Message, ex);
            return Result<CmdResponse>.Failure("An error occurred while updating identity file", 500);
        }
    }
}
