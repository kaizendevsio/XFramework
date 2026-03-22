using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ByteSizeLib;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Messaging.Domain.Shared;
using Messaging.Integration.Drivers;
using XFramework.Core.Loggers;
using XFramework.Core.Patterns;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Services.Helpers;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Enums;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Services;
using Session = XFramework.Domain.Shared.Contracts.Session;

namespace IdentityServer.Api.Services;

/// <summary>
/// Unified authentication service implementing all IdentityServer operations.
/// Consolidates credential management, authentication, verification, and session management.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IDataContext _dataContext;
    private readonly ITenantService _tenantService;
    private readonly IJwtService _jwtService;
    private readonly IHelperService _helperService;
    private readonly CacheManager _cache;
    private readonly IMessagingServiceWrapper _messagingServiceWrapper;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IDataContext dataContext,
        ITenantService tenantService,
        IJwtService jwtService,
        IHelperService helperService,
        CacheManager cache,
        IMessagingServiceWrapper messagingServiceWrapper,
        ILogger<AuthService> logger)
    {
        _dataContext = dataContext;
        _tenantService = tenantService;
        _jwtService = jwtService;
        _helperService = helperService;
        _cache = cache;
        _messagingServiceWrapper = messagingServiceWrapper;
        _logger = logger;
    }

    #region Credential Management

    /// <inheritdoc />
    public async Task<Result<IdentityCredential>> CreateCredentialAsync(
        Create<IdentityCredential> request,
        CancellationToken ct = default)
    {
        try
        {
            // Hash password with BCrypt (workFactor 11) - SECURITY CRITICAL
            var hashPasswordByte = Encoding.ASCII.GetBytes(
                BCrypt.Net.BCrypt.HashPassword(inputKey: request.Model.Password, workFactor: 11));

            request.Model.PasswordByte = hashPasswordByte;

            // Add credential to database
            _dataContext.Add(request.Model);
            await _dataContext.SaveChangesAsync(ct);

            _logger.EntityCreated("IdentityCredential", request.Model.Id);

            return Result<IdentityCredential>.Success(request.Model);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("CreateCredential", "IdentityCredential", Guid.Empty, ex.Message, ex);
            return Result<IdentityCredential>.Failure(
                "An error occurred while creating the credential", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<IdentityCredential>> UpdateCredentialAsync(
        Patch<IdentityCredential> request,
        CancellationToken ct = default)
    {
        try
        {
            var credential = await _dataContext.Query<IdentityCredential>()
                .Where(c => c.Id == request.Model.Id)
                .FirstOrDefaultAsync(ct);

            if (credential == null)
            {
                return Result<IdentityCredential>.NotFound(
                    $"Credential with id {request.Model.Id} not found");
            }

            // Update credential properties (excluding password, which has its own method)
            credential.UserName = request.Model.UserName ?? credential.UserName;
            credential.IsEnabled = request.Model.IsEnabled;

            _dataContext.Update(credential);
            await _dataContext.SaveChangesAsync(ct);

            _logger.EntityUpdated("IdentityCredential", credential.Id);

            return Result<IdentityCredential>.Success(credential);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("UpdateCredential", "IdentityCredential", request.Model.Id, ex.Message, ex);
            return Result<IdentityCredential>.Failure(
                "An error occurred while updating the credential", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (request.CreadentialId == Guid.Empty)
            {
                _logger.ValidationFailed("ChangePassword", "Identifier is required");
                return Result.Failure("Identifier is required", 400);
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                _logger.ValidationFailed("ChangePassword", "Password is required");
                return Result.Failure("Password is required", 400);
            }

            var credential = await _dataContext.Query<IdentityCredential>()
                .Where(u => u.Id == request.CreadentialId)
                .FirstOrDefaultAsync(ct);

            if (credential == null)
            {
                _logger.EntityNotFound("IdentityCredential", request.CreadentialId);
                return Result.NotFound("User not found");
            }

            // Check verification if required
            if (request.RequireVerificationId)
            {
                var verification = await _dataContext.Query<IdentityVerification>()
                    .Where(i => i.VerificationType.Name == nameof(IdentityConstants.VerificationType.Sms))
                    .Where(i => i.CredentialId == request.CreadentialId)
                    .Where(i => i.Status == (int)GenericStatusType.Approved)
                    .Where(i => i.Id == request.VerificationId)
                    .Where(i => i.StatusUpdatedOn >= DateTime.UtcNow.AddMinutes(-10))
                    .FirstOrDefaultAsync(ct);

                if (verification == null)
                {
                    _logger.TokenValidationFailed(request.CreadentialId, "Invalid verification code or expired");
                    return Result.NotFound("Invalid verification code or expired");
                }
            }

            // Hash new password with BCrypt (workFactor 11) - SECURITY CRITICAL
            var hashPasswordByte = Encoding.ASCII.GetBytes(
                BCrypt.Net.BCrypt.HashPassword(inputKey: request.NewPassword, workFactor: 11));
            credential.PasswordByte = hashPasswordByte;

            _dataContext.Update(credential);
            await _dataContext.SaveChangesAsync(ct);

            _logger.PasswordChanged(request.CreadentialId);

            return Result.Success("Password reset request successful");
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("ChangePassword", "IdentityCredential", request.CreadentialId, ex.Message, ex);
            return Result.Failure("An error occurred while processing your request", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> VerifyPasswordAsync(
        VerifyPasswordRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Password))
            {
                _logger.ValidationFailed("VerifyPassword", "Password is required");
                return Result<bool>.Failure("Please provide a valid password", 400);
            }

            if (request.CredentialId == Guid.Empty)
            {
                _logger.ValidationFailed("VerifyPassword", "Identifier is required");
                return Result<bool>.Failure("An error occurred while processing your request", 400);
            }

            var user = await _dataContext.Query<IdentityCredential>()
                .Where(u => u.Id == request.CredentialId)
                .FirstOrDefaultAsync(ct);

            if (user == null)
            {
                _logger.EntityNotFound("IdentityCredential", request.CredentialId);
                return Result<bool>.NotFound("User not found");
            }

            // Verify password using BCrypt - SECURITY CRITICAL
            var isPasswordValid = BCrypt.Net.BCrypt.Verify(
                request.Password,
                Encoding.ASCII.GetString(user.PasswordByte));

            if (!isPasswordValid)
            {
                _logger.TokenValidationFailed(request.CredentialId, "Invalid password");
                return Result<bool>.Failure("Invalid password", 400);
            }

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("VerifyPassword", "IdentityCredential", request.CredentialId, ex.Message, ex);
            return Result<bool>.Failure("An error occurred while processing your request", 500);
        }
    }

    #endregion

    #region Authentication

    /// <inheritdoc />
    public async Task<Result<AuthenticateIdentityResponse>> AuthenticateAsync(
        AuthenticateIdentityRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId);

            // Validate inputs
            if (request.RoleId == Guid.Empty)
            {
                return Result<AuthenticateIdentityResponse>.Failure("Role is required", 400);
            }

            if (string.IsNullOrEmpty(request.UserName))
            {
                return Result<AuthenticateIdentityResponse>.Failure("Username is required", 400);
            }

            if (string.IsNullOrEmpty(request.Password))
            {
                return Result<AuthenticateIdentityResponse>.Failure("Password is required", 400);
            }

            // Validate authorization (multi-type user lookup) - SECURITY CRITICAL
            var originalCredential = await ValidateAuthorization(
                request, tenant, request.AuthorizationType, ct);

            if (originalCredential is null)
            {
                return Result<AuthenticateIdentityResponse>.NotFound(
                    "User or identity does not exist");
            }

            // Validate password - SECURITY CRITICAL
            var credential = await ValidatePassword(
                request, request.AuthorizationType, originalCredential, ct);

            if (credential == null)
            {
                // Log failed authentication attempt - SECURITY CRITICAL
                await CreateAuthorizationLog(
                    tenant.Id,
                    originalCredential.Id,
                    request.Metadata.IpAddress,
                    request.Metadata.Name,
                    request.Metadata.DeviceName,
                    request.Metadata.DeviceAgent,
                    AuthenticationState.WrongPassword,
                    null);

                await _dataContext.SaveChangesAsync(CancellationToken.None);

                return Result<AuthenticateIdentityResponse>.Failure("Wrong password", 400);
            }

            // Check roles - SECURITY CRITICAL
            var roleList = await GetRoleList(credential, ct);
            if (roleList is null || !roleList.Any(i => i.Type.Id == request.RoleId))
            {
                // Log unauthorized attempt - SECURITY CRITICAL
                await CreateAuthorizationLog(
                    tenant.Id,
                    credential.Id,
                    request.Metadata.IpAddress,
                    request.Metadata.Name,
                    request.Metadata.DeviceName,
                    request.Metadata.DeviceAgent,
                    AuthenticationState.Unauthorized,
                    null);

                await _dataContext.SaveChangesAsync(CancellationToken.None);

                return Result<AuthenticateIdentityResponse>.Failure(
                    "You do not have permission to access this resource", 403);
            }

            // Generate JWT token - SECURITY CRITICAL
            var token = new JwtToken();
            if (request.GenerateToken)
            {
                token = await _jwtService.GenerateToken(
                    request.UserName,
                    credential.Id,
                    roleList.Select(i => i.TypeId ?? Guid.Empty).ToList());
            }

            // Determine session type based on authorization type
            var sessionTypeId = await GetSessionTypeId(tenant.Id, request.AuthorizationType);

            // Create session - SECURITY CRITICAL
            var session = await CreateSession(
                tenant.Id,
                credential.Id,
                sessionTypeId,
                token);

            // Log successful authentication - SECURITY CRITICAL
            await CreateAuthorizationLog(
                tenant.Id,
                credential.Id,
                request.Metadata.IpAddress,
                request.Metadata.Name,
                request.Metadata.DeviceName,
                request.Metadata.DeviceAgent,
                AuthenticationState.Authenticated,
                token.SessionId);

            var saveResult = await _dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
            {
                _logger.OperationFailed("Authenticate", "SaveChanges", credential.Id, saveResult.Message, null);
                return Result<AuthenticateIdentityResponse>.Failure(
                    "Failed to persist authentication session", 500);
            }

            _logger.UserAuthenticated(credential.Id, request.Metadata.IpAddress);

            return Result<AuthenticateIdentityResponse>.Success(
                new AuthenticateIdentityResponse
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken,
                    SessionId = token.SessionId,
                    Identity = credential.IdentityInfo,
                    Credential = credential
                });
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("Authenticate", "IdentityCredential", Guid.Empty, ex.Message, ex);
            return Result<AuthenticateIdentityResponse>.Failure(
                "An error occurred during authentication", 500);
        }
    }

    #endregion

    #region Verification

    /// <inheritdoc />
    public async Task<Result<IdentityVerification>> CreateVerificationAsync(
        Create<IdentityVerification> request,
        CancellationToken ct = default)
    {
        try
        {
            var tenant = await _tenantService.GetTenant(
                request.Metadata.TenantId ?? request.Model.TenantId);

            var verificationType = await _dataContext.Query<IdentityVerificationType>()
                .Where(i => i.Id == request.Model.VerificationTypeId)
                .FirstOrDefaultAsync(ct);

            if (verificationType is null)
            {
                return Result<IdentityVerification>.NotFound(
                    $"Verification type with id {request.Model.VerificationTypeId} does not exist");
            }

            var identityCredential = await _dataContext.Query<IdentityCredential>()
                .Where(i => i.Id == request.Model.CredentialId)
                .FirstOrDefaultAsync(ct);

            if (identityCredential == null)
            {
                return Result<IdentityVerification>.NotFound(
                    $"Credential with id {request.Model.CredentialId} does not exist");
            }

            switch (verificationType.Name)
            {
                case nameof(IdentityConstants.VerificationType.Sms):
                    var messageTemplate = await _dataContext.Query<RegistryConfiguration>()
                        .Where(i => i.TenantId == tenant.Id)
                        .Where(i => i.Group.Name == "MessagingService_Otp")
                        .FirstOrDefaultAsync(ct);

                    if (messageTemplate is null)
                    {
                        return Result<IdentityVerification>.Failure(
                            "Unable to send message: OTP message template could not be found", 409);
                    }

                    // Generate OTP code
                    var otp = _helperService.GenerateRandomNumber(111111, 999999);
                    var message = messageTemplate.Value.Replace("|Value|", $"{otp}");

                    // Get phone contact via separate query (avoids ThenInclude)
                    var phoneContact = await _dataContext.Query<IdentityContact>()
                        .Include(c => c.Type)
                        .Where(c => c.CredentialId == identityCredential.Id)
                        .Where(c => c.Type.Name == "Phone")
                        .FirstOrDefaultAsync(ct);

                    var contact = phoneContact?.Value;

                    if (string.IsNullOrEmpty(contact))
                    {
                        return Result<IdentityVerification>.Failure(
                            $"Credential with id {request.Model.CredentialId} does not have a phone number", 502);
                    }

                    // Create verification entity
                    var verification = new IdentityVerification
                    {
                        Status = (short?)GenericStatusType.Pending,
                        StatusUpdatedOn = DateTime.UtcNow,
                        Token = $"{otp}",
                        Expiry = DateTime.UtcNow.AddMinutes((double)verificationType.DefaultExpiry),
                        CredentialId = identityCredential.Id,
                        VerificationTypeId = verificationType.Id
                    };

                    _dataContext.Add(verification);
                    await _dataContext.SaveChangesAsync(ct);

                    // Send SMS with OTP
                    await _messagingServiceWrapper.CreateDirectMessage(new()
                    {
                        MessageTransportType = MessageTransportType.Sms,
                        Sender = GenericSender.System,
                        Recipient = contact,
                        Subject = "One Time Password",
                        Intent = "OTP",
                        Message = message,
                        IsScheduled = false,
                        Metadata = request.Metadata
                    });

                    _logger.LogInformation(
                        "Verification created and SMS sent. VerificationId: {VerificationId}, CredentialId: {CredentialId}",
                        verification.Id, identityCredential.Id);

                    return Result<IdentityVerification>.Success(verification);
            }

            return Result<IdentityVerification>.Failure(
                "Verification type not supported", 500);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("CreateVerification", "IdentityVerification", Guid.Empty, ex.Message, ex);
            return Result<IdentityVerification>.Failure(
                "An error occurred while creating verification", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<IdentityVerification>> UpdateVerificationAsync(
        Patch<IdentityVerification> request,
        CancellationToken ct = default)
    {
        try
        {
            var verification = await _dataContext.Query<IdentityVerification>()
                .Where(i => i.Status == (int?)GenericStatusType.Pending)
                .Where(i => i.Token == request.Model.Token)
                .FirstOrDefaultAsync(ct);

            if (verification == null)
            {
                return Result<IdentityVerification>.NotFound(
                    $"Verification with token {request.Model.Token} does not exist");
            }

            // Update verification status to Approved
            verification.Status = (short?)GenericStatusType.Approved;
            verification.StatusUpdatedOn = DateTime.UtcNow;

            _dataContext.Update(verification);
            await _dataContext.SaveChangesAsync(ct);

            _logger.EntityUpdated("IdentityVerification", verification.Id);

            return Result<IdentityVerification>.Success(verification);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("UpdateVerification", "IdentityVerification", Guid.Empty, ex.Message, ex);
            return Result<IdentityVerification>.Failure(
                "An error occurred while updating verification", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<CheckVerificationResponse>> CheckVerificationAsync(
        CheckVerificationRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId);

            var identityCredential = await _dataContext.Query<IdentityCredential>()
                .Where(i => i.Id == request.CredentialId && i.TenantId == tenant.Id)
                .FirstOrDefaultAsync(ct);

            if (identityCredential is null)
            {
                return Result<CheckVerificationResponse>.NotFound(
                    $"Identity credential with id {request.CredentialId} does not exist");
            }

            var verificationType = await _dataContext.Query<IdentityVerificationType>()
                .Where(i => i.Id == request.VerificationTypeId)
                .FirstOrDefaultAsync(ct);

            if (verificationType is null)
            {
                return Result<CheckVerificationResponse>.NotFound(
                    $"Verification type with id {request.VerificationTypeId} does not exist");
            }

            // Check for approved and non-expired verification
            var anyVerification = await _dataContext.Query<IdentityVerification>()
                .Where(i => i.VerificationTypeId == verificationType.Id)
                .Where(i => i.CredentialId == identityCredential.Id)
                .Where(i => i.Status == (short?)GenericStatusType.Approved)
                .Where(i => i.Expiry > DateTime.UtcNow)
                .AnyAsync(ct);

            if (anyVerification)
            {
                return Result<CheckVerificationResponse>.Success(
                    new CheckVerificationResponse { IsVerified = true });
            }

            // Get last pending verification
            var lastVerification = await _dataContext.Query<IdentityVerification>()
                .Where(i => i.VerificationTypeId == verificationType.Id)
                .Where(i => i.CredentialId == identityCredential.Id)
                .Where(i => i.Status == (short?)GenericStatusType.Pending)
                .Where(i => i.Expiry > DateTime.UtcNow)
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (lastVerification == null)
            {
                return Result<CheckVerificationResponse>.NotFound(
                    $"No pending verification found for credential with id {request.CredentialId}");
            }

            return Result<CheckVerificationResponse>.Success(
                new CheckVerificationResponse
                {
                    IsVerified = false,
                    LastVerification = lastVerification
                });
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("CheckVerification", "IdentityVerification", Guid.Empty, ex.Message, ex);
            return Result<CheckVerificationResponse>.Failure(
                "An error occurred while checking verification", 500);
        }
    }

    #endregion

    #region File Storage

    /// <inheritdoc />
    public async Task<Result<StorageFile>> CreateFileAsync(
        Create<StorageFile> request,
        CancellationToken ct = default)
    {
        try
        {
            if (request.Model.FileBytes is null)
            {
                return Result<StorageFile>.Failure("Cannot upload empty file", 400);
            }

            var storageFileType = await _dataContext.Query<StorageFileType>()
                .Where(i => i.Id == request.Model.TypeId)
                .FirstOrDefaultAsync(ct);

            if (storageFileType == null)
            {
                return Result<StorageFile>.NotFound(
                    $"File type with id {request.Model.TypeId} not found");
            }

            var fileIdentifier = await _dataContext.Query<StorageFileIdentifier>()
                .Where(i => i.Id == request.Model.StorageFileIdentifierId)
                .FirstOrDefaultAsync(ct);

            if (fileIdentifier == null)
            {
                return Result<StorageFile>.NotFound(
                    $"File identifier with id {request.Model.StorageFileIdentifierId} not found");
            }

            // Get Azure Blob Storage connection string
            var connectionConfig = await _dataContext.Query<RegistryConfigurationGroup>()
                .Include(i => i.RegistryConfigurations)
                .Where(i => i.Name == "AzureBlobStorage")
                .Where(i => i.TenantId == request.Metadata.TenantId)
                .FirstOrDefaultAsync(ct);

            var connectionString = connectionConfig?.RegistryConfigurations
                .FirstOrDefault(i => i.Key == "ConnectionString")?.Value;

            if (string.IsNullOrEmpty(connectionString))
            {
                return Result<StorageFile>.Failure(
                    "Azure blob storage connection string not found", 500);
            }

            // Upload file to Azure Blob Storage
            var blobServiceClient = new BlobServiceClient(connectionString);
            var client = blobServiceClient.GetBlobContainerClient(request.Model.BlobContainer);
            var blob = client.GetBlobClient(
                request.Model.ContentPath.Replace($"{request.Model.BlobContainer}/", ""));

            await blob.UploadAsync(
                content: BinaryData.FromBytes(request.Model.FileBytes),
                options: new BlobUploadOptions
                {
                    HttpHeaders = new()
                    {
                        ContentType = request.Model.ContentType
                    }
                },
                cancellationToken: ct);

            request.Model.Type = storageFileType;
            request.Model.StorageFileIdentifier = fileIdentifier;
            request.Model.FileSize = (decimal?)ByteSize.FromBytes(request.Model.FileBytes.Length).KiloBytes;

            // Save file entity to database
            _dataContext.Add(request.Model);
            await _dataContext.SaveChangesAsync(ct);

            _logger.EntityCreated("StorageFile", request.Model.Id);

            return Result<StorageFile>.Success(request.Model);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("UploadFile", "StorageFile", Guid.Empty, ex.Message, ex);
            return Result<StorageFile>.Failure(
                "An error occurred while uploading the file", 500);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Validates user authorization with multi-type authentication support.
    /// Supports Username, Email, Phone, UsernameEmailPhone (combined), and Token authentication.
    /// </summary>
    private async Task<IdentityCredential?> ValidateAuthorization(
        AuthenticateIdentityRequest request,
        XFramework.Domain.Shared.Contracts.Tenant tenant,
        AuthorizationType authorizationType,
        CancellationToken ct)
    {
        IdentityCredential? result;

        reAuth:
        switch (authorizationType)
        {
            case AuthorizationType.Default:
                // Get default authorization type from registry
                var getDefaults = await _dataContext.Query<RegistryConfiguration>()
                    .Where(i => i.TenantId == tenant.Id && i.Key == "DefaultAuthorizeBy")
                    .FirstOrDefaultAsync(ct);

                if (getDefaults is null)
                {
                    throw new ArgumentException(
                        $"Unable to login: Tenant with id '{tenant.Id}' does not have 'DefaultAuthorizeBy' key in registry");
                }

                authorizationType = (AuthorizationType)int.Parse(getDefaults.Value);
                goto reAuth;

            case AuthorizationType.UsernameEmailPhone:
                // Try username first
                result = await _dataContext.Query<IdentityCredential>()
                    .Include(i => i.IdentityInfo)
                    .Include(i => i.IdentityRoles)
                    .Where(i => i.TenantId == tenant.Id && i.UserName == request.UserName)
                    .FirstOrDefaultAsync(ct);

                // Try email if username not found
                if (result is null)
                {
                    var emailContact = await _dataContext.Query<IdentityContact>()
                        .Include(c => c.Type)
                        .Where(i =>
                            i.Credential.TenantId == tenant.Id &&
                            i.Value == request.UserName &&
                            i.Type.Name == nameof(GenericContactType.Email))
                        .FirstOrDefaultAsync(ct);

                    if (emailContact != null)
                    {
                        result = await _dataContext.Query<IdentityCredential>()
                            .Include(i => i.IdentityInfo)
                            .Include(i => i.IdentityRoles)
                            .Where(i => i.Id == emailContact.CredentialId)
                            .FirstOrDefaultAsync(ct);
                    }
                }

                // Try phone if email not found
                if (result is null)
                {
                    var phoneContact = await _dataContext.Query<IdentityContact>()
                        .Include(c => c.Type)
                        .Where(i =>
                            i.Credential.TenantId == tenant.Id &&
                            i.Value == request.UserName.ValidatePhoneNumber(true) &&
                            i.Type.Name == nameof(GenericContactType.Phone))
                        .FirstOrDefaultAsync(ct);

                    if (phoneContact != null)
                    {
                        result = await _dataContext.Query<IdentityCredential>()
                            .Include(i => i.IdentityInfo)
                            .Include(i => i.IdentityRoles)
                            .Where(i => i.Id == phoneContact.CredentialId)
                            .FirstOrDefaultAsync(ct);
                    }
                }
                break;

            case AuthorizationType.Username:
                result = await _dataContext.Query<IdentityCredential>()
                    .Include(i => i.IdentityInfo)
                    .Include(i => i.IdentityRoles)
                    .Where(i => i.TenantId == tenant.Id && i.UserName == request.UserName)
                    .FirstOrDefaultAsync(ct);
                break;

            case AuthorizationType.Email:
                request.UserName?.ValidateEmailAddress();
                var emailContactForAuth = await _dataContext.Query<IdentityContact>()
                    .Include(c => c.Type)
                    .Where(i =>
                        i.Credential.TenantId == tenant.Id &&
                        i.Value == request.UserName &&
                        i.Type.Name == nameof(GenericContactType.Email))
                    .FirstOrDefaultAsync(ct);

                result = emailContactForAuth != null
                    ? await _dataContext.Query<IdentityCredential>()
                        .Include(i => i.IdentityInfo)
                        .Include(i => i.IdentityRoles)
                        .Where(i => i.Id == emailContactForAuth.CredentialId)
                        .FirstOrDefaultAsync(ct)
                    : null;
                break;

            case AuthorizationType.Phone:
                var phoneContactForAuth = await _dataContext.Query<IdentityContact>()
                    .Include(c => c.Type)
                    .Where(i =>
                        i.Credential.TenantId == tenant.Id &&
                        i.Value == request.UserName.ValidatePhoneNumber(true) &&
                        i.Type.Name == nameof(GenericContactType.Phone))
                    .FirstOrDefaultAsync(ct);

                result = phoneContactForAuth != null
                    ? await _dataContext.Query<IdentityCredential>()
                        .Include(i => i.IdentityInfo)
                        .Include(i => i.IdentityRoles)
                        .Where(i => i.Id == phoneContactForAuth.CredentialId)
                        .FirstOrDefaultAsync(ct)
                    : null;
                break;

            case AuthorizationType.Token:
                result = await _dataContext.Query<IdentityCredential>()
                    .Include(i => i.IdentityRoles)
                    .Include(i => i.IdentityInfo)
                    .Where(i => i.UserName == request.UserName)
                    .FirstOrDefaultAsync(ct);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(authorizationType));
        }

        return result;
    }

    /// <summary>
    /// Validates password using BCrypt verification.
    /// For token-based authentication, password validation is skipped.
    /// </summary>
    private async Task<IdentityCredential?> ValidatePassword(
        AuthenticateIdentityRequest request,
        AuthorizationType authorizationType,
        IdentityCredential credential,
        CancellationToken ct)
    {
        // Skip password validation for token-based authentication
        if (authorizationType == AuthorizationType.Token)
            return credential;

        // Verify password using BCrypt - SECURITY CRITICAL
        var hashPassword = Encoding.ASCII.GetString(credential.PasswordByte);
        return BCrypt.Net.BCrypt.Verify(request.Password, hashPassword) is false ? null : credential;
    }

    /// <summary>
    /// Retrieves the list of roles for a credential.
    /// </summary>
    private async Task<List<IdentityRole>?> GetRoleList(
        IdentityCredential credential,
        CancellationToken ct)
    {
        var roleList = await _dataContext.Query<IdentityRole>()
            .Include(i => i.Type)
            .Where(i => i.CredentialId == credential.Id)
            .ToListAsync(ct);

        return roleList.Any() ? roleList : null;
    }

    /// <summary>
    /// Creates an authorization log entry for audit tracking.
    /// Logs all authentication attempts (success, failure, unauthorized).
    /// </summary>
    private async Task CreateAuthorizationLog(
        Guid tenantId,
        Guid credentialId,
        string ipAddress,
        string loginSource,
        string deviceName,
        string deviceAgent,
        AuthenticationState authStatus,
        Guid? sessionId)
    {
        var authorizationLog = new AuthorizationLog
        {
            TenantId = tenantId,
            CredentialId = credentialId,
            Ipaddress = ipAddress,
            IsSuccess = authStatus == AuthenticationState.Authenticated,
            AuthStatus = authStatus,
            LoginSource = loginSource,
            DeviceName = deviceName,
            DeviceAgent = deviceAgent,
            SessionId = sessionId
        };

        _dataContext.Add(authorizationLog);
    }

    /// <summary>
    /// Creates a session entity for tracking user sessions.
    /// </summary>
    private async Task<Session> CreateSession(
        Guid tenantId,
        Guid credentialId,
        Guid? sessionTypeId,
        JwtToken token)
    {
        var session = new Session
        {
            Id = token.SessionId,
            TenantId = tenantId,
            SessionTypeId = sessionTypeId,
            CredentialId = credentialId,
            SessionData = JsonSerializer.Serialize(token)
        };

        _dataContext.Add(session);
        return session;
    }

    /// <summary>
    /// Gets the session type ID based on authorization type.
    /// User session for standard auth, Service session for token-based auth.
    /// </summary>
    private async Task<Guid?> GetSessionTypeId(
        Guid tenantId,
        AuthorizationType authorizationType)
    {
        Guid? sessionTypeId;

        if (authorizationType is not AuthorizationType.Token)
        {
            // User session type
            sessionTypeId = _cache.Get<Guid>("SessionTypeId:User");
            if (sessionTypeId is null || sessionTypeId == Guid.Empty)
            {
                var userSessionType = await _dataContext.Query<SessionType>()
                    .Where(i => i.TenantId == tenantId)
                    .Where(i => i.SystemReferenceId == IdentityConstants.SessionType.User)
                    .Where(i => i.Name == "User")
                    .FirstOrDefaultAsync(CancellationToken.None);

                var userSessionTypeId = userSessionType?.Id ?? Guid.Empty;
                await _cache.Set("SessionTypeId:User", userSessionTypeId);
                sessionTypeId = userSessionTypeId;
            }
        }
        else
        {
            // Service/Token session type
            sessionTypeId = _cache.Get<Guid>("SessionTypeId:Token");
            if (sessionTypeId is null)
            {
                var serviceSessionType = await _dataContext.Query<SessionType>()
                    .Where(i => i.TenantId == tenantId)
                    .Where(i => i.SystemReferenceId == IdentityConstants.SessionType.Service)
                    .Where(i => i.Name == "Service")
                    .FirstOrDefaultAsync(CancellationToken.None);

                var serviceSessionTypeId = serviceSessionType?.Id ?? Guid.Empty;
                await _cache.Set("SessionTypeId:Token", serviceSessionTypeId);
                sessionTypeId = serviceSessionTypeId;
            }
        }

        return sessionTypeId;
    }

    #endregion
}
