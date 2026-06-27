using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Communications.Domain.Shared;
using Communications.Integration.Drivers;
using XFramework.Core.Loggers;
using XFramework.Core.Services;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.Services;
using Session = IdentityServer.Domain.Shared.Contracts.Session;

namespace IdentityServer.Api.Services;

/// <summary>
/// In-memory account lockout tracking.
/// Tracks failed login attempts and lockout expiration per credential.
/// </summary>
internal sealed class LockoutInfo
{
    public int FailedAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; }
}


/// <summary>
/// Unified authentication service implementing all IdentityServer operations.
/// Consolidates credential management, authentication, verification, and session management.
/// </summary>
public sealed class AuthService : IAuthService
{
    private const int MaxFailedLoginAttempts = 5;
    private const int LockoutDurationMinutes = 15;
    private const int DefaultSessionExpirationHours = 24;
    private const int RememberMeSessionExpirationDays = 30;
    private const int PasswordResetTokenExpirationMinutes = 30;

    private static readonly ConcurrentDictionary<Guid, LockoutInfo> LockoutCache = new();

    private readonly IDataContext _dataContext;
    private readonly ITenantResolver _tenantService;
    private readonly IJwtService _jwtService;
    private readonly IHelperService _helperService;
    private readonly CacheManager _cache;
    private readonly ICommunicationsServiceWrapper _communicationsServiceWrapper;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IDataContext dataContext,
        ITenantResolver tenantService,
        IJwtService jwtService,
        IHelperService helperService,
        CacheManager cache,
        ICommunicationsServiceWrapper communicationsServiceWrapper,
        ILogger<AuthService> logger)
    {
        _dataContext = dataContext;
        _tenantService = tenantService;
        _jwtService = jwtService;
        _helperService = helperService;
        _cache = cache;
        _communicationsServiceWrapper = communicationsServiceWrapper;
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
                    .Where(i => i.VerificationTypeId == IdentityConstants.VerificationType.Sms)
                    .Where(i => i.CredentialId == request.CreadentialId)
                    .Where(i => i.Status == (short?)GenericStatusType.Approved)
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
            var isPasswordValid = VerifyPasswordHash(request.Password, user.PasswordByte);

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

            // Check account lockout - SECURITY CRITICAL
            if (LockoutCache.TryGetValue(originalCredential.Id, out var lockoutInfo)
                && lockoutInfo.LockoutEnd.HasValue
                && lockoutInfo.LockoutEnd.Value > DateTime.UtcNow)
            {
                await CreateAuthorizationLog(
                    tenant.Id,
                    originalCredential.Id,
                    request.Metadata.IpAddress,
                    request.Metadata.Name,
                    request.Metadata.DeviceName,
                    request.Metadata.DeviceAgent,
                    AuthenticationState.Locked,
                    null);

                await _dataContext.SaveChangesAsync(CancellationToken.None);

                var remainingMinutes = (int)Math.Ceiling(
                    (lockoutInfo.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);

                _logger.MultipleFailedLogins(
                    request.UserName ?? string.Empty,
                    request.Metadata.IpAddress ?? string.Empty,
                    lockoutInfo.FailedAttempts);

                return Result<AuthenticateIdentityResponse>.Failure(
                    $"Account is locked due to multiple failed login attempts. Try again in {remainingMinutes} minute(s).", 423);
            }

            // Validate password - SECURITY CRITICAL
            var credential = await ValidatePassword(
                request, request.AuthorizationType, originalCredential, ct);

            if (credential == null)
            {
                // Track failed login attempt for lockout - SECURITY CRITICAL
                var info = LockoutCache.GetOrAdd(originalCredential.Id, _ => new LockoutInfo());
                info.FailedAttempts++;

                if (info.FailedAttempts >= MaxFailedLoginAttempts)
                {
                    info.LockoutEnd = DateTime.UtcNow.AddMinutes(LockoutDurationMinutes);
                    _logger.MultipleFailedLogins(
                        request.UserName ?? string.Empty,
                        request.Metadata.IpAddress ?? string.Empty,
                        info.FailedAttempts);
                }

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

            // Reset lockout on successful password validation - SECURITY CRITICAL
            LockoutCache.TryRemove(originalCredential.Id, out _);

            // Check roles - SECURITY CRITICAL
            var roleList = await GetRoleList(credential, ct);
            if (roleList is null || !roleList.Any(i => i.TypeId == request.RoleId))
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
                    roleList.Select(i => i.TypeId ?? Guid.Empty).ToList(),
                    tenant.Id);
            }

            // Determine session type based on authorization type
            var sessionTypeId = await GetSessionTypeId(tenant.Id, request.AuthorizationType);

            // Create session with expiration - SECURITY CRITICAL
            var sessionExpiresAt = request.RememberMe
                ? DateTime.UtcNow.AddDays(RememberMeSessionExpirationDays)
                : DateTime.UtcNow.AddHours(DefaultSessionExpirationHours);

            var session = await CreateSession(
                tenant.Id,
                credential.Id,
                sessionTypeId,
                token,
                sessionExpiresAt);

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
                _logger.OperationFailed("Authenticate", "SaveChanges", credential.Id, saveResult.Message ?? string.Empty, null);
                return Result<AuthenticateIdentityResponse>.Failure(
                    "Failed to persist authentication session", 500);
            }

            _logger.UserAuthenticated(credential.Id, request.Metadata.IpAddress ?? string.Empty);

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

            if (verificationType.DefaultExpiry is not { } defaultExpiryMinutes)
            {
                return Result<IdentityVerification>.Failure(
                    "Verification type does not have a default expiry", 409);
            }

            switch (verificationType.Id)
            {
                case var id when id == IdentityConstants.VerificationType.Sms:
                    var messageTemplate = await _dataContext.Query<RegistryConfiguration>()
                        .Where(i => i.TenantId == tenant.Id)
                        .Where(i => i.Group != null && i.Group.Name == "CommunicationsService_Otp")
                        .FirstOrDefaultAsync(ct);

                    if (string.IsNullOrEmpty(messageTemplate?.Value))
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
                        .Where(c => c.Type != null && c.Type.Name == "Phone")
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
                        Expiry = DateTime.UtcNow.AddMinutes(defaultExpiryMinutes),
                        CredentialId = identityCredential.Id,
                        VerificationTypeId = verificationType.Id
                    };

                    _dataContext.Add(verification);
                    await _dataContext.SaveChangesAsync(ct);

                    // Send SMS with OTP
                    var smsResult = await _communicationsServiceWrapper.CreateDirectMessageAsync(new()
                    {
                        MessageTransportType = MessageTransportType.Sms,
                        Sender = GenericSender.System,
                        Recipient = contact,
                        Subject = "One Time Password",
                        Intent = "OTP",
                        Message = message,
                        IsScheduled = false,
                        Metadata = request.Metadata
                    }, ct);

                    if (!smsResult.IsSuccess)
                    {
                        var statusCode = smsResult.HttpStatusCode == 0 ? 502 : (int)smsResult.HttpStatusCode;
                        return Result<IdentityVerification>.Failure(
                            smsResult.Message ?? "SMS verification message could not be queued",
                            statusCode);
                    }

                    _logger.LogInformation(
                        "Verification created and SMS sent. VerificationId: {VerificationId}, CredentialId: {CredentialId}",
                        verification.Id, identityCredential.Id);

                    return Result<IdentityVerification>.Success(verification);

                case var id when id == IdentityConstants.VerificationType.Email:
                    var emailMessageTemplate = await _dataContext.Query<RegistryConfiguration>()
                        .Where(i => i.TenantId == tenant.Id)
                        .Where(i => i.Group != null && i.Group.Name == "CommunicationsService_Otp")
                        .FirstOrDefaultAsync(ct);

                    if (string.IsNullOrEmpty(emailMessageTemplate?.Value))
                    {
                        return Result<IdentityVerification>.Failure(
                            "Unable to send message: OTP message template could not be found", 409);
                    }

                    // Generate OTP code for email
                    var emailOtp = _helperService.GenerateRandomNumber(111111, 999999);
                    var emailMessage = emailMessageTemplate.Value.Replace("|Value|", $"{emailOtp}");

                    // Get email contact via separate query
                    var emailContact = await _dataContext.Query<IdentityContact>()
                        .Include(c => c.Type)
                        .Where(c => c.CredentialId == identityCredential.Id)
                        .Where(c => c.Type != null && c.Type.Name == "Email")
                        .FirstOrDefaultAsync(ct);

                    var emailAddress = emailContact?.Value;

                    if (string.IsNullOrEmpty(emailAddress))
                    {
                        return Result<IdentityVerification>.Failure(
                            $"Credential with id {request.Model.CredentialId} does not have an email address", 502);
                    }

                    // Create verification entity for email
                    var emailVerification = new IdentityVerification
                    {
                        Status = (short?)GenericStatusType.Pending,
                        StatusUpdatedOn = DateTime.UtcNow,
                        Token = $"{emailOtp}",
                        Expiry = DateTime.UtcNow.AddMinutes(defaultExpiryMinutes),
                        CredentialId = identityCredential.Id,
                        VerificationTypeId = verificationType.Id
                    };

                    _dataContext.Add(emailVerification);
                    await _dataContext.SaveChangesAsync(ct);

                    // Send Email with OTP
                    var emailResult = await _communicationsServiceWrapper.CreateDirectMessageAsync(new()
                    {
                        MessageTransportType = MessageTransportType.Email,
                        Sender = GenericSender.System,
                        Recipient = emailAddress,
                        Subject = "Verification Code",
                        Intent = "OTP",
                        Message = emailMessage,
                        IsScheduled = false,
                        Metadata = request.Metadata
                    }, ct);

                    if (!emailResult.IsSuccess)
                    {
                        var statusCode = emailResult.HttpStatusCode == 0 ? 502 : (int)emailResult.HttpStatusCode;
                        return Result<IdentityVerification>.Failure(
                            emailResult.Message ?? "Email verification message could not be queued",
                            statusCode);
                    }

                    _logger.LogInformation(
                        "Verification created and email sent. VerificationId: {VerificationId}, CredentialId: {CredentialId}",
                        emailVerification.Id, identityCredential.Id);

                    return Result<IdentityVerification>.Success(emailVerification);
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
                .Where(i => i.Status == (short?)GenericStatusType.Pending)
                .Where(i => i.Token == request.Model.Token)
                .Where(i => i.Expiry > DateTime.UtcNow)
                .FirstOrDefaultAsync(ct);

            if (verification == null)
            {
                return Result<IdentityVerification>.NotFound(
                    "Verification token is invalid or expired");
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

    #region Helper Methods

    /// <summary>
    /// Validates user authorization with multi-type authentication support.
    /// Supports Username, Email, Phone, UsernameEmailPhone (combined), and Token authentication.
    /// </summary>
    private async Task<IdentityCredential?> ValidateAuthorization(
        AuthenticateIdentityRequest request,
        IdentityServer.Domain.Shared.Contracts.Tenant tenant,
        AuthorizationType authorizationType,
        CancellationToken ct)
    {
        IdentityCredential? result;
        var userName = request.UserName ?? string.Empty;

        reAuth:
        switch (authorizationType)
        {
            case AuthorizationType.Default:
                // Get default authorization type from registry
                var getDefaults = await _dataContext.Query<RegistryConfiguration>()
                    .IgnoreQueryFilters()
                    .Where(i => i.TenantId == tenant.Id && i.Key == "DefaultAuthorizeBy")
                    .FirstOrDefaultAsync(ct);

                if (getDefaults is null)
                {
                    throw new ArgumentException(
                        $"Unable to login: Tenant with id '{tenant.Id}' does not have 'DefaultAuthorizeBy' key in registry");
                }

                if (!int.TryParse(getDefaults.Value, out var defaultAuthorizationType))
                {
                    throw new ArgumentException(
                        $"Unable to login: Tenant with id '{tenant.Id}' has an invalid 'DefaultAuthorizeBy' value in registry");
                }

                authorizationType = (AuthorizationType)defaultAuthorizationType;
                goto reAuth;

            case AuthorizationType.UsernameEmailPhone:
                // Try username first
                result = await _dataContext.Query<IdentityCredential>()
                    .IgnoreQueryFilters()
                    .Include(i => i.IdentityInfo)
                    .Include(i => i.IdentityRoles)
                    .Where(i => i.TenantId == tenant.Id && i.UserName == userName)
                    .FirstOrDefaultAsync(ct);

                // Try email if username not found
                if (result is null)
                {
                    var emailContact = await _dataContext.Query<IdentityContact>()
                        .IgnoreQueryFilters()
                        .Include(c => c.Type)
                        .Where(i =>
                            i.Credential.TenantId == tenant.Id &&
                            i.Value == userName &&
                            i.Type != null &&
                            i.Type.Name == nameof(GenericContactType.Email))
                        .FirstOrDefaultAsync(ct);

                    if (emailContact != null)
                    {
                        result = await _dataContext.Query<IdentityCredential>()
                            .IgnoreQueryFilters()
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
                        .IgnoreQueryFilters()
                        .Include(c => c.Type)
                        .Where(i =>
                            i.Credential.TenantId == tenant.Id &&
                            i.Value == userName.ValidatePhoneNumber(true) &&
                            i.Type != null &&
                            i.Type.Name == nameof(GenericContactType.Phone))
                        .FirstOrDefaultAsync(ct);

                    if (phoneContact != null)
                    {
                        result = await _dataContext.Query<IdentityCredential>()
                            .IgnoreQueryFilters()
                            .Include(i => i.IdentityInfo)
                            .Include(i => i.IdentityRoles)
                            .Where(i => i.Id == phoneContact.CredentialId)
                            .FirstOrDefaultAsync(ct);
                    }
                }
                break;

            case AuthorizationType.Username:
                result = await _dataContext.Query<IdentityCredential>()
                    .IgnoreQueryFilters()
                    .Include(i => i.IdentityInfo)
                    .Include(i => i.IdentityRoles)
                    .Where(i => i.TenantId == tenant.Id && i.UserName == userName)
                    .FirstOrDefaultAsync(ct);
                break;

            case AuthorizationType.Email:
                if (!string.IsNullOrEmpty(userName))
                {
                    userName.ValidateEmailAddress();
                }

                var emailContactForAuth = await _dataContext.Query<IdentityContact>()
                    .IgnoreQueryFilters()
                    .Include(c => c.Type)
                    .Where(i =>
                        i.Credential.TenantId == tenant.Id &&
                        i.Value == userName &&
                        i.Type != null &&
                        i.Type.Name == nameof(GenericContactType.Email))
                    .FirstOrDefaultAsync(ct);

                result = emailContactForAuth != null
                    ? await _dataContext.Query<IdentityCredential>()
                        .IgnoreQueryFilters()
                        .Include(i => i.IdentityInfo)
                        .Include(i => i.IdentityRoles)
                        .Where(i => i.Id == emailContactForAuth.CredentialId)
                        .FirstOrDefaultAsync(ct)
                    : null;
                break;

            case AuthorizationType.Phone:
                var phoneContactForAuth = await _dataContext.Query<IdentityContact>()
                    .IgnoreQueryFilters()
                    .Include(c => c.Type)
                    .Where(i =>
                        i.Credential.TenantId == tenant.Id &&
                        i.Value == userName.ValidatePhoneNumber(true) &&
                        i.Type != null &&
                        i.Type.Name == nameof(GenericContactType.Phone))
                    .FirstOrDefaultAsync(ct);

                result = phoneContactForAuth != null
                    ? await _dataContext.Query<IdentityCredential>()
                        .IgnoreQueryFilters()
                        .Include(i => i.IdentityInfo)
                        .Include(i => i.IdentityRoles)
                        .Where(i => i.Id == phoneContactForAuth.CredentialId)
                        .FirstOrDefaultAsync(ct)
                    : null;
                break;

            case AuthorizationType.Token:
                result = await _dataContext.Query<IdentityCredential>()
                    .IgnoreQueryFilters()
                    .Include(i => i.IdentityRoles)
                    .Include(i => i.IdentityInfo)
                    .Where(i => i.UserName == userName)
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
        return VerifyPasswordHash(request.Password, credential.PasswordByte) ? credential : null;
    }

    private static bool VerifyPasswordHash(string? password, byte[]? passwordBytes)
    {
        if (string.IsNullOrEmpty(password) || passwordBytes is not { Length: > 0 })
        {
            return false;
        }

        var hashPassword = Encoding.ASCII.GetString(passwordBytes);
        return BCrypt.Net.BCrypt.Verify(password, hashPassword);
    }

    /// <summary>
    /// Retrieves the list of roles for a credential.
    /// </summary>
    private async Task<List<IdentityRole>?> GetRoleList(
        IdentityCredential credential,
        CancellationToken ct)
    {
        var roleList = await _dataContext.Query<IdentityRole>()
            .IgnoreQueryFilters()
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
        string? ipAddress,
        string? loginSource,
        string? deviceName,
        string? deviceAgent,
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
    /// Creates a session entity for tracking user sessions with expiration.
    /// </summary>
    private async Task<Session> CreateSession(
        Guid tenantId,
        Guid credentialId,
        Guid? sessionTypeId,
        JwtToken token,
        DateTime? expiresAt = null)
    {
        var session = new Session
        {
            Id = token.SessionId,
            TenantId = tenantId,
            SessionTypeId = sessionTypeId,
            CredentialId = credentialId,
            SessionData = JsonSerializer.Serialize(token),
            Status = CurrentSessionState.Active,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddHours(DefaultSessionExpirationHours)
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
                    .IgnoreQueryFilters()
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
                    .IgnoreQueryFilters()
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

    #region Logout & Refresh Token

    public async Task<Result> LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        try
        {
            var session = await _dataContext.Query<Session>()
                .Where(s => s.Id == request.SessionId)
                .Where(s => s.CredentialId == request.CredentialId)
                .FirstOrDefaultAsync(ct);

            if (session is null)
            {
                _logger.EntityNotFound("Session", request.SessionId);
                return Result.NotFound("Session not found");
            }

            if (session.Status == CurrentSessionState.Inactive)
            {
                return Result.Failure("Session is already inactive", 400);
            }

            session.Status = CurrentSessionState.Inactive;
            session.ModifiedAt = DateTime.UtcNow;
            _dataContext.Update(session);

            await CreateAuthorizationLog(
                session.TenantId,
                request.CredentialId,
                request.Metadata?.IpAddress ?? string.Empty,
                request.Metadata?.Name ?? string.Empty,
                request.Metadata?.DeviceName ?? string.Empty,
                request.Metadata?.DeviceAgent ?? string.Empty,
                AuthenticationState.NotAuthenticated,
                session.Id);

            await _dataContext.SaveChangesAsync(ct);

            _logger.UserLoggedOut(request.CredentialId);

            return Result.Success("Logged out successfully");
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("Logout", "Session", request.SessionId, ex.Message, ex);
            return Result.Failure("An error occurred during logout", 500);
        }
    }

    public async Task<Result<RefreshTokenResponse>> RefreshTokenAsync(
        RefreshTokenRequest request, CancellationToken ct = default)
    {
        try
        {
            var session = await _dataContext.Query<Session>()
                .Where(s => s.Id == request.SessionId)
                .Where(s => s.Status == CurrentSessionState.Active)
                .FirstOrDefaultAsync(ct);

            if (session is null)
            {
                _logger.EntityNotFound("Session", request.SessionId);
                return Result<RefreshTokenResponse>.NotFound("Session not found or inactive");
            }

            // Check session expiration - SECURITY CRITICAL
            if (session.ExpiresAt.HasValue && session.ExpiresAt.Value <= DateTime.UtcNow)
            {
                session.Status = CurrentSessionState.Expired;
                session.ModifiedAt = DateTime.UtcNow;
                _dataContext.Update(session);
                await _dataContext.SaveChangesAsync(ct);

                _logger.TokenValidationFailed(session.CredentialId, "Session has expired");
                return Result<RefreshTokenResponse>.Failure("Session has expired. Please log in again.", 401);
            }

            // Validate refresh token against stored session data
            var storedToken = JsonSerializer.Deserialize<JwtToken>(session.SessionData ?? "{}");
            if (storedToken is null || storedToken.RefreshToken != request.RefreshToken)
            {
                _logger.TokenValidationFailed(session.CredentialId, "Invalid refresh token");
                return Result<RefreshTokenResponse>.Failure("Invalid refresh token", 401);
            }

            // Decode expired access token to extract claims
            ClaimsPrincipal principal;
            try
            {
                var (claims, _) = await _jwtService.DecodeExpiredToken(request.AccessToken!);
                principal = claims;
            }
            catch
            {
                _logger.TokenValidationFailed(session.CredentialId, "Invalid access token");
                return Result<RefreshTokenResponse>.Failure("Invalid access token", 401);
            }

            var credentialIdClaim = principal.FindFirstValue(ClaimTypes.Name);
            if (!Guid.TryParse(credentialIdClaim, out var tokenCredentialId) ||
                tokenCredentialId != session.CredentialId)
            {
                _logger.TokenValidationFailed(session.CredentialId, "Access token does not match session credential");
                return Result<RefreshTokenResponse>.Failure("Invalid access token", 401);
            }

            // Generate new token pair from existing claims
            var newToken = await _jwtService.GenerateToken(principal.Claims.ToList());
            newToken.SessionId = session.Id;

            // Update session with new tokens
            session.SessionData = JsonSerializer.Serialize(newToken);
            session.ModifiedAt = DateTime.UtcNow;
            _dataContext.Update(session);
            await _dataContext.SaveChangesAsync(ct);

            // Extract expiration from the newly generated token
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(newToken.AccessToken);
            var expiresIn = (int)(jwt.ValidTo - DateTime.UtcNow).TotalSeconds;

            return Result<RefreshTokenResponse>.Success(new RefreshTokenResponse
            {
                AccessToken = newToken.AccessToken,
                RefreshToken = newToken.RefreshToken,
                SessionId = newToken.SessionId,
                ExpiresIn = expiresIn
            });
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("RefreshToken", "Session", request.SessionId, ex.Message, ex);
            return Result<RefreshTokenResponse>.Failure("An error occurred while refreshing the token", 500);
        }
    }

    #endregion

    #region Password Reset

    /// <inheritdoc />
    public async Task<Result> ForgotPasswordAsync(
        ForgotPasswordRequest request, CancellationToken ct = default)
    {
        try
        {
            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId);

            // Determine lookup method based on input
            IdentityCredential? credential = null;
            string? recipientAddress = null;
            MessageTransportType transportType;

            if (!string.IsNullOrEmpty(request.Email))
            {
                // Lookup credential by email contact
                var emailContact = await _dataContext.Query<IdentityContact>()
                    .Include(c => c.Type)
                    .Where(c => c.Credential.TenantId == tenant.Id)
                    .Where(c => c.Value == request.Email)
                    .Where(c => c.Type != null && c.Type.Name == nameof(GenericContactType.Email))
                    .FirstOrDefaultAsync(ct);

                if (emailContact != null)
                {
                    credential = await _dataContext.Query<IdentityCredential>()
                        .Where(c => c.Id == emailContact.CredentialId)
                        .FirstOrDefaultAsync(ct);
                    recipientAddress = emailContact.Value;
                }

                transportType = MessageTransportType.Email;
            }
            else if (!string.IsNullOrEmpty(request.Phone))
            {
                // Lookup credential by phone contact
                var phoneContact = await _dataContext.Query<IdentityContact>()
                    .Include(c => c.Type)
                    .Where(c => c.Credential.TenantId == tenant.Id)
                    .Where(c => c.Value == request.Phone)
                    .Where(c => c.Type != null && c.Type.Name == nameof(GenericContactType.Phone))
                    .FirstOrDefaultAsync(ct);

                if (phoneContact != null)
                {
                    credential = await _dataContext.Query<IdentityCredential>()
                        .Where(c => c.Id == phoneContact.CredentialId)
                        .FirstOrDefaultAsync(ct);
                    recipientAddress = phoneContact.Value;
                }

                transportType = MessageTransportType.Sms;
            }
            else
            {
                // Don't reveal whether the account exists — always return success
                return Result.Success("If an account exists with that contact information, a password reset link has been sent.");
            }

            // If no account found, still return success (don't reveal account existence)
            if (credential is null || string.IsNullOrEmpty(recipientAddress))
            {
                return Result.Success("If an account exists with that contact information, a password reset link has been sent.");
            }

            // Generate reset token (GUID for URL-safe token)
            var resetToken = Guid.NewGuid().ToString("N");

            // Get or create the PasswordReset verification type
            var verificationTypeId = IdentityConstants.VerificationType.Email;
            if (transportType == MessageTransportType.Sms)
            {
                verificationTypeId = IdentityConstants.VerificationType.Sms;
            }

            // Create verification entity with the reset token
            var verification = new IdentityVerification
            {
                Status = (short?)GenericStatusType.Pending,
                StatusUpdatedOn = DateTime.UtcNow,
                Token = resetToken,
                Expiry = DateTime.UtcNow.AddMinutes(PasswordResetTokenExpirationMinutes),
                CredentialId = credential.Id,
                VerificationTypeId = verificationTypeId
            };

            _dataContext.Add(verification);
            await _dataContext.SaveChangesAsync(ct);

            // Get message template for password reset
            var messageTemplate = await _dataContext.Query<RegistryConfiguration>()
                .Where(i => i.TenantId == tenant.Id)
                .Where(i => i.Group != null && i.Group.Name == "CommunicationsService_PasswordReset")
                .FirstOrDefaultAsync(ct);

            var message = messageTemplate?.Value?.Replace("|Token|", resetToken)
                ?? $"Your password reset token is: {resetToken}. This token expires in {PasswordResetTokenExpirationMinutes} minutes.";

            // Send reset token via appropriate transport
            var deliveryResult = await _communicationsServiceWrapper.CreateDirectMessageAsync(new()
            {
                MessageTransportType = transportType,
                Sender = GenericSender.System,
                Recipient = recipientAddress,
                Subject = "Password Reset Request",
                Intent = "PasswordReset",
                Message = message,
                IsScheduled = false,
                Metadata = request.Metadata
            }, ct);

            if (!deliveryResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Password reset token delivery failed. CredentialId: {CredentialId}, Transport: {Transport}, StatusCode: {StatusCode}, Message: {Message}",
                    credential.Id,
                    transportType,
                    deliveryResult.HttpStatusCode,
                    deliveryResult.Message);

                return Result.Success("If an account exists with that contact information, a password reset link has been sent.");
            }

            _logger.LogInformation(
                "Password reset token generated and sent. CredentialId: {CredentialId}, Transport: {Transport}",
                credential.Id, transportType);

            return Result.Success("If an account exists with that contact information, a password reset link has been sent.");
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("ForgotPassword", "IdentityCredential", Guid.Empty, ex.Message, ex);
            // Still return success to not reveal internal errors
            return Result.Success("If an account exists with that contact information, a password reset link has been sent.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken ct = default)
    {
        try
        {
            // Look up verification by token, must be pending and not expired
            var verification = await _dataContext.Query<IdentityVerification>()
                .Where(i => i.Token == request.Token)
                .Where(i => i.Status == (short?)GenericStatusType.Pending)
                .Where(i => i.Expiry > DateTime.UtcNow)
                .FirstOrDefaultAsync(ct);

            if (verification is null)
            {
                return Result.Failure("Invalid or expired reset token", 400);
            }

            // Look up the credential
            var credential = await _dataContext.Query<IdentityCredential>()
                .Where(c => c.Id == verification.CredentialId)
                .FirstOrDefaultAsync(ct);

            if (credential is null)
            {
                return Result.NotFound("Associated account not found");
            }

            // Hash new password with BCrypt (workFactor 11) - SECURITY CRITICAL
            var hashPasswordByte = Encoding.ASCII.GetBytes(
                BCrypt.Net.BCrypt.HashPassword(inputKey: request.NewPassword, workFactor: 11));
            credential.PasswordByte = hashPasswordByte;

            _dataContext.Update(credential);

            // Invalidate the token (mark verification as used)
            verification.Status = (short?)GenericStatusType.Approved;
            verification.StatusUpdatedOn = DateTime.UtcNow;
            _dataContext.Update(verification);

            await _dataContext.SaveChangesAsync(ct);

            // Clear any lockout on successful password reset
            LockoutCache.TryRemove(credential.Id, out _);

            _logger.PasswordChanged(credential.Id);

            return Result.Success("Password has been reset successfully");
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("ResetPassword", "IdentityCredential", Guid.Empty, ex.Message, ex);
            return Result.Failure("An error occurred while resetting the password", 500);
        }
    }

    #endregion
}
