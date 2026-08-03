using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.Json;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Communications.Domain.Shared;
using Storage.Domain.Shared.Contracts.Requests;
using Storage.Domain.Shared.Contracts.Responses;
using Storage.Integration.Drivers;
using IdentityServer.Api.Infrastructure;
using Npgsql;
using XFramework.Core.Loggers;
using XFramework.Core.RateLimiting;
using XFramework.Core.Services;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.Security;
using XFramework.Integration.Services;
using Session = IdentityServer.Domain.Shared.Contracts.Session;

namespace IdentityServer.Api.Services;

/// <summary>
/// Unified authentication service implementing all IdentityServer operations.
/// Consolidates credential management, authentication, verification, and session management.
/// </summary>
public sealed class AuthService : IAuthService, IPasswordResetProcessor
{
    private const int MaxFailedLoginAttempts = 5;
    private const int LockoutDurationMinutes = 15;
    private const int DefaultSessionExpirationHours = 24;
    private const int RememberMeSessionExpirationDays = 30;
    private const int PasswordResetTokenExpirationMinutes = 30;
    private const int MaxVerificationAttempts = 5;
    private const string PasswordResetRequestConstraint = "UX_PasswordResetOutbox_Tenant_Request";
    private static readonly TimeSpan AvatarCompensationTimeout = TimeSpan.FromSeconds(5);

    private static readonly JwtSecurityTokenHandler TokenHandler = new();
    private static readonly byte[] DummyPasswordHash = Encoding.ASCII.GetBytes(
        BCrypt.Net.BCrypt.HashPassword("identityserver-timing-equalizer", workFactor: 11));

    private readonly IDataContext _dataContext;
    private readonly DbContext _dbContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantResolver _tenantService;
    private readonly IJwtService _jwtService;
    private readonly TimeProvider _timeProvider;
    private readonly IDistributedSecurityRateLimiter _securityRateLimiter;
    private readonly ITrustedServiceInvocationResolver _trustedServiceInvocationResolver;
    private readonly CacheManager _cache;
    private readonly IStorageServiceWrapper _storageServiceWrapper;
    private readonly IIdentityAuthorizationService _authorizationService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IDataContext dataContext,
        DbContext dbContext,
        IServiceScopeFactory scopeFactory,
        IHttpContextAccessor httpContextAccessor,
        ITenantResolver tenantService,
        IJwtService jwtService,
        TimeProvider timeProvider,
        IDistributedSecurityRateLimiter securityRateLimiter,
        ITrustedServiceInvocationResolver trustedServiceInvocationResolver,
        CacheManager cache,
        IStorageServiceWrapper storageServiceWrapper,
        IIdentityAuthorizationService authorizationService,
        ILogger<AuthService> logger)
    {
        _dataContext = dataContext;
        _dbContext = dbContext;
        _scopeFactory = scopeFactory;
        _httpContextAccessor = httpContextAccessor;
        _tenantService = tenantService;
        _jwtService = jwtService;
        _timeProvider = timeProvider;
        _securityRateLimiter = securityRateLimiter;
        _trustedServiceInvocationResolver = trustedServiceInvocationResolver;
        _cache = cache;
        _storageServiceWrapper = storageServiceWrapper;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    #region Credential Management

    /// <inheritdoc />
    public async Task<Result<CredentialAdministrationResponse>> CreateCredentialAsync(
        CreateCredentialRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Password))
                return Result<CredentialAdministrationResponse>.Failure("Password is required", 400);

            if (!IdentityPasswordPolicy.IsWithinBcryptByteLimit(request.Password))
                return Result<CredentialAdministrationResponse>.Failure("Password must not exceed 72 UTF-8 bytes", 400);

            if (request.Metadata.TenantId is not { } tenantId || tenantId == Guid.Empty)
                return Result<CredentialAdministrationResponse>.Failure("Tenant context is required", 403);

            var authorization = await _authorizationService.AuthorizeCredentialOperationAsync(
                request.Metadata,
                tenantId,
                targetCredentialId: null,
                IdentityAuthorizationConstants.Create,
                allowSelf: false,
                ct);
            if (!authorization.IsSuccess)
                return Result<CredentialAdministrationResponse>.Failure(authorization.Message!, authorization.StatusCode);

            var identityExists = await _dataContext.Query<IdentityInformation>()
                .IgnoreQueryFilters()
                .Where(identity => identity.Id == request.IdentityInfoId)
                .Where(identity => identity.TenantId == tenantId)
                .Where(identity => !identity.IsDeleted && identity.IsEnabled)
                .AnyAsync(ct);

            if (!identityExists)
                return Result<CredentialAdministrationResponse>.NotFound("Identity information not found");

            var credential = new IdentityCredential
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                IdentityInfoId = request.IdentityInfoId,
                UserName = request.UserName?.Trim(),
                UserAlias = request.UserAlias?.Trim(),
                Password = request.Password,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };

            // Hash password with BCrypt (workFactor 11) - SECURITY CRITICAL
            var hashPasswordByte = Encoding.ASCII.GetBytes(
                BCrypt.Net.BCrypt.HashPassword(inputKey: credential.Password, workFactor: 11));

            credential.PasswordByte = hashPasswordByte;

            // Add credential to database
            _dataContext.Add(credential);
            var saveResult = await _dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result<CredentialAdministrationResponse>.Failure("Credential could not be created", saveResult.StatusCode);

            _logger.EntityCreated("IdentityCredential", credential.Id);

            return Result<CredentialAdministrationResponse>.Success(
                CreateCredentialAdministrationResponse(credential));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("CreateCredential", "IdentityCredential", Guid.Empty, ex.Message, ex);
            return Result<CredentialAdministrationResponse>.Failure(
                "An error occurred while creating the credential", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<CredentialAdministrationResponse>> UpdateCredentialAsync(
        UpdateCredentialRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (request.Metadata.TenantId is not { } tenantId || tenantId == Guid.Empty)
                return Result<CredentialAdministrationResponse>.Failure("Tenant context is required", 403);

            if (request.CredentialId is not { } credentialId || credentialId == Guid.Empty)
                return Result<CredentialAdministrationResponse>.Failure("Credential ID is required", 400);

            if (request.ExpectedConcurrencyStamp == Guid.Empty)
                return Result<CredentialAdministrationResponse>.Failure("Expected concurrency stamp is required", 400);

            var authorization = await _authorizationService.AuthorizeCredentialOperationAsync(
                request.Metadata,
                tenantId,
                credentialId,
                IdentityAuthorizationConstants.Update,
                allowSelf: false,
                ct);
            if (!authorization.IsSuccess)
                return Result<CredentialAdministrationResponse>.Failure(authorization.Message!, authorization.StatusCode);

            var credential = await _dataContext.Query<IdentityCredential>()
                .Where(c => c.Id == credentialId)
                .Where(c => c.TenantId == tenantId)
                .Where(c => !c.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (credential == null)
            {
                return Result<CredentialAdministrationResponse>.NotFound(
                    $"Credential with id {credentialId} not found");
            }

            if (credential.ConcurrencyStamp != request.ExpectedConcurrencyStamp)
            {
                return Result<CredentialAdministrationResponse>.Conflict(
                    "Credential was modified by another operation");
            }

            var disabling = request.IsEnabled == false && credential.IsEnabled;
            await using var transaction = disabling && _dbContext.Database.CurrentTransaction is null
                ? await _dbContext.Database.BeginTransactionAsync(ct)
                : null;

            // Update credential properties (excluding password, which has its own method)
            _dataContext.Update(credential);
            _dbContext.Entry(credential)
                .Property(item => item.ConcurrencyStamp)
                .OriginalValue = request.ExpectedConcurrencyStamp;
            credential.UserName = request.UserName?.Trim() ?? credential.UserName;
            credential.UserAlias = request.UserAlias?.Trim() ?? credential.UserAlias;
            credential.IsEnabled = request.IsEnabled ?? credential.IsEnabled;
            credential.ModifiedAt = DateTime.UtcNow;
            credential.ConcurrencyStamp = Guid.NewGuid();

            if (disabling)
                await RevokeActiveSessionsAsync(tenantId, credential.Id, ct);

            var saveResult = await _dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result<CredentialAdministrationResponse>.Failure("Credential could not be updated", saveResult.StatusCode);

            if (transaction is not null)
                await transaction.CommitAsync(ct);

            _logger.EntityUpdated("IdentityCredential", credential.Id);

            return Result<CredentialAdministrationResponse>.Success(
                CreateCredentialAdministrationResponse(credential));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.OperationFailed(
                "UpdateCredential",
                "IdentityCredential",
                request.CredentialId ?? Guid.Empty,
                ex.Message,
                ex);
            return Result<CredentialAdministrationResponse>.Failure(
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

            if (request.Metadata.TenantId is not { } tenantId || tenantId == Guid.Empty)
                return Result.Forbidden("Tenant context is required");

            var authorization = await _authorizationService.AuthorizeCredentialOperationAsync(
                request.Metadata,
                tenantId,
                request.CreadentialId,
                IdentityAuthorizationConstants.Update,
                allowSelf: true,
                ct);
            if (!authorization.IsSuccess)
                return authorization;

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                _logger.ValidationFailed("ChangePassword", "Password is required");
                return Result.Failure("Password is required", 400);
            }

            if (!IdentityPasswordPolicy.IsWithinBcryptByteLimit(request.NewPassword))
                return Result.Failure("Password must not exceed 72 UTF-8 bytes", 400);

            var credential = await _dataContext.Query<IdentityCredential>()
                .IgnoreQueryFilters()
                .Where(u => u.Id == request.CreadentialId)
                .Where(u => u.TenantId == tenantId)
                .Where(u => !u.IsDeleted && u.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (credential == null)
            {
                _logger.EntityNotFound("IdentityCredential", request.CreadentialId);
                return Result.NotFound("User not found");
            }

            var verification = await _dataContext.Query<IdentityVerification>()
                .IgnoreQueryFilters()
                .Where(i => i.TenantId == tenantId)
                .Where(i => i.VerificationTypeId == IdentityConstants.VerificationType.Sms)
                .Where(i => i.CredentialId == request.CreadentialId)
                .Where(i => i.Purpose == IdentityConstants.VerificationPurpose.ContactVerification)
                .Where(i => i.Status == (short?)GenericStatusType.Approved)
                .Where(i => i.Id == request.VerificationId)
                .Where(i => i.StatusUpdatedOn >= DateTime.UtcNow.AddMinutes(-10))
                .Where(i => i.ConsumedAt == null)
                .Where(i => !i.IsDeleted && i.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (verification == null)
            {
                _logger.TokenValidationFailed(request.CreadentialId, "Invalid verification code or expired");
                return Result.NotFound("Invalid verification code or expired");
            }

            await using var transaction = _dbContext.Database.CurrentTransaction is null
                ? await _dbContext.Database.BeginTransactionAsync(ct)
                : null;

            // Hash new password with BCrypt (workFactor 11) - SECURITY CRITICAL
            var hashPasswordByte = Encoding.ASCII.GetBytes(
                BCrypt.Net.BCrypt.HashPassword(inputKey: request.NewPassword, workFactor: 11));
            _dataContext.Update(credential);
            _dataContext.Update(verification);
            credential.PasswordByte = hashPasswordByte;
            credential.FailedLoginAttempts = 0;
            credential.LockoutEnd = null;
            credential.ConcurrencyStamp = Guid.NewGuid();

            verification.ConsumedAt = DateTimeOffset.UtcNow;
            verification.ConcurrencyStamp = Guid.NewGuid();

            await RevokeActiveSessionsAsync(tenantId, credential.Id, ct);

            var saveResult = await _dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result.Failure("Password could not be changed", saveResult.StatusCode);

            if (transaction is not null)
                await transaction.CommitAsync(ct);

            _logger.PasswordChanged(request.CreadentialId);

            return Result.Success("Password reset request successful");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
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

            if (!IdentityPasswordPolicy.IsWithinBcryptByteLimit(request.Password))
                return Result<bool>.Failure("Invalid credentials", 401);

            if (request.CredentialId == Guid.Empty)
            {
                _logger.ValidationFailed("VerifyPassword", "Identifier is required");
                return Result<bool>.Failure("An error occurred while processing your request", 400);
            }

            if (request.Metadata.TenantId is not { } tenantId || tenantId == Guid.Empty)
                return Result<bool>.Forbidden("Tenant context is required");

            var authorization = await _authorizationService.AuthorizeCredentialOperationAsync(
                request.Metadata,
                tenantId,
                request.CredentialId,
                IdentityAuthorizationConstants.View,
                allowSelf: true,
                ct);
            if (!authorization.IsSuccess)
                return Result<bool>.Failure(authorization.Message!, authorization.StatusCode);

            var user = await _dataContext.Query<IdentityCredential>()
                .IgnoreQueryFilters()
                .Where(u => u.Id == request.CredentialId)
                .Where(u => u.TenantId == tenantId)
                .Where(u => !u.IsDeleted && u.IsEnabled)
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
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
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
            if (request.AuthorizationType == AuthorizationType.Token)
                return Result<AuthenticateIdentityResponse>.Failure(
                    "Service token authentication is not supported by this endpoint", 400);

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

            if (!IdentityPasswordPolicy.IsWithinBcryptByteLimit(request.Password))
                return Result<AuthenticateIdentityResponse>.Failure("Invalid credentials", 401);

            var rateLimitDecision = await AcquireAuthenticationRateLimitAsync(request, ct);
            if (!rateLimitDecision.IsAllowed)
                return Result<AuthenticateIdentityResponse>.Failure("Too many requests.", 429);

            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId, ct);
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Validate authorization (multi-type user lookup) - SECURITY CRITICAL
            var originalCredential = await ValidateAuthorization(
                request, tenant, request.AuthorizationType, ct);

            if (originalCredential is null)
            {
                _ = VerifyPasswordHash(request.Password, DummyPasswordHash);
                return Result<AuthenticateIdentityResponse>.Failure("Invalid credentials", 401);
            }

            await using var authenticationTransaction = _dbContext.Database.CurrentTransaction is null
                ? await _dbContext.Database.BeginTransactionAsync(ct)
                : null;

            var credential = await LockCredentialForAuthenticationAsync(originalCredential.Id, ct);
            var identity = originalCredential.IdentityInfo;

            if (credential is null
                || credential.TenantId != tenant.Id
                || credential.IsDeleted
                || !credential.IsEnabled
                || identity is null)
            {
                _ = VerifyPasswordHash(request.Password, DummyPasswordHash);
                return Result<AuthenticateIdentityResponse>.Failure("Invalid credentials", 401);
            }

            // Check account lockout - SECURITY CRITICAL
            if (credential.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
            {
                _ = VerifyPasswordHash(request.Password, DummyPasswordHash);
                await CreateAuthorizationLog(
                    tenant.Id,
                    credential.Id,
                    request.Metadata.IpAddress,
                    request.Metadata.Name,
                    request.Metadata.DeviceName,
                    request.Metadata.DeviceAgent,
                    AuthenticationState.Locked,
                    null);

                var lockedLogSave = await _dataContext.SaveChangesAsync(ct);
                if (!lockedLogSave.IsSuccess)
                    return Result<AuthenticateIdentityResponse>.Failure("Authentication state could not be persisted", lockedLogSave.StatusCode);

                if (authenticationTransaction is not null)
                    await authenticationTransaction.CommitAsync(ct);

                _logger.MultipleFailedLogins(
                    credential.Id,
                    credential.FailedLoginAttempts);

                return Result<AuthenticateIdentityResponse>.Failure("Invalid credentials", 401);
            }

            // Validate password - SECURITY CRITICAL
            if (!VerifyPasswordHash(request.Password, credential.PasswordByte))
            {
                // Track failed login attempt for lockout - SECURITY CRITICAL
                credential.FailedLoginAttempts++;

                if (credential.FailedLoginAttempts >= MaxFailedLoginAttempts)
                {
                    credential.LockoutEnd = now.AddMinutes(LockoutDurationMinutes);
                    _logger.MultipleFailedLogins(
                        credential.Id,
                        credential.FailedLoginAttempts);
                }

                credential.ConcurrencyStamp = Guid.NewGuid();

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

                var failedAttemptSave = await _dataContext.SaveChangesAsync(ct);
                if (!failedAttemptSave.IsSuccess)
                {
                    _logger.OperationFailed("Authenticate", "IdentityCredential", credential.Id, failedAttemptSave.Message ?? string.Empty, null);
                    return Result<AuthenticateIdentityResponse>.Failure("Authentication state could not be persisted", 409);
                }

                if (authenticationTransaction is not null)
                    await authenticationTransaction.CommitAsync(ct);

                return Result<AuthenticateIdentityResponse>.Failure("Invalid credentials", 401);
            }

            // Reset lockout on successful password validation - SECURITY CRITICAL
            credential.FailedLoginAttempts = 0;
            credential.LockoutEnd = null;
            credential.ConcurrencyStamp = Guid.NewGuid();

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

                var unauthorizedLogSave = await _dataContext.SaveChangesAsync(ct);
                if (!unauthorizedLogSave.IsSuccess)
                    return Result<AuthenticateIdentityResponse>.Failure("Authentication state could not be persisted", unauthorizedLogSave.StatusCode);

                if (authenticationTransaction is not null)
                    await authenticationTransaction.CommitAsync(ct);

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

            var expiresIn = request.GenerateToken
                ? GetAccessTokenLifetimeSeconds(token)
                : 0;

            if (request.GenerateToken)
            {
                var sessionTypeId = await GetSessionTypeId(tenant.Id, request.AuthorizationType, ct);
                var sessionExpiresAt = request.RememberMe
                    ? now.AddDays(RememberMeSessionExpirationDays)
                    : now.AddHours(DefaultSessionExpirationHours);

                CreateSession(
                    tenant.Id,
                    credential.Id,
                    sessionTypeId,
                    token,
                    sessionExpiresAt);
            }

            // Log successful authentication - SECURITY CRITICAL
            await CreateAuthorizationLog(
                tenant.Id,
                credential.Id,
                request.Metadata.IpAddress,
                request.Metadata.Name,
                request.Metadata.DeviceName,
                request.Metadata.DeviceAgent,
                AuthenticationState.Authenticated,
                request.GenerateToken ? token.SessionId : null);

            var saveResult = await _dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
            {
                _logger.OperationFailed("Authenticate", "SaveChanges", credential.Id, saveResult.Message ?? string.Empty, null);
                return Result<AuthenticateIdentityResponse>.Failure(
                    "Failed to persist authentication session", 500);
            }

            if (authenticationTransaction is not null)
                await authenticationTransaction.CommitAsync(ct);

            _logger.UserAuthenticated(credential.Id);

            return Result<AuthenticateIdentityResponse>.Success(
                new AuthenticateIdentityResponse
                {
                    AccessToken = token.AccessToken,
                    TokenType = request.GenerateToken ? "Bearer" : null,
                    ExpiresIn = expiresIn,
                    RefreshToken = token.RefreshToken,
                    SessionId = request.GenerateToken ? token.SessionId : null,
                    Identity = ToAuthenticatedIdentityResponse(identity),
                    Credential = ToAuthenticatedCredentialResponse(credential)
                });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("Authenticate", "IdentityCredential", Guid.Empty, ex.Message, ex);
            return Result<AuthenticateIdentityResponse>.Failure(
                "An error occurred during authentication", 500);
        }
    }

    #endregion

    private static AuthenticatedIdentityResponse? ToAuthenticatedIdentityResponse(
        IdentityInformation? identity) => identity is null
        ? null
        : new AuthenticatedIdentityResponse
        {
            Id = identity.Id,
            TenantId = identity.TenantId,
            FirstName = identity.FirstName,
            MiddleName = identity.MiddleName,
            LastName = identity.LastName,
            Suffix = identity.Suffix,
            IdentityName = identity.IdentityName,
            IdentityDescription = identity.IdentityDescription,
            BirthDate = identity.BirthDate,
            Gender = identity.Gender,
            IsVerified = identity.IsVerified,
            CivilStatus = identity.CivilStatus
        };

    private static AuthenticatedCredentialResponse ToAuthenticatedCredentialResponse(
        IdentityCredential credential) => new()
        {
            Id = credential.Id,
            TenantId = credential.TenantId,
            IdentityInfoId = credential.IdentityInfoId,
            UserName = credential.UserName,
            UserAlias = credential.UserAlias,
            LogInStatus = credential.LogInStatus,
            IsOnline = credential.IsOnline,
            LastSeen = credential.LastSeen,
            OnlineSince = credential.OnlineSince,
            StatusMessage = credential.StatusMessage,
            LastActivityType = credential.LastActivityType,
            Device = credential.Device,
            Location = credential.Location,
            AvatarStorageFileId = credential.AvatarStorageFileId,
            AvatarUrl = credential.AvatarUrl,
            AvatarUpdatedAt = credential.AvatarUpdatedAt
        };

    private static int GetAccessTokenLifetimeSeconds(JwtToken token)
    {
        if (string.IsNullOrWhiteSpace(token.AccessToken)
            || string.IsNullOrWhiteSpace(token.RefreshToken)
            || token.SessionId == Guid.Empty)
        {
            throw new InvalidOperationException("The generated authentication token is incomplete.");
        }

        var jwt = TokenHandler.ReadJwtToken(token.AccessToken);
        var lifetimeSeconds = (jwt.ValidTo - jwt.ValidFrom).TotalSeconds;

        if (lifetimeSeconds <= 0 || lifetimeSeconds > int.MaxValue)
            throw new InvalidOperationException("The generated access token has an invalid lifetime.");

        return checked((int)lifetimeSeconds);
    }

    #region Verification

    /// <inheritdoc />
    public async Task<Result<IdentityVerification>> CreateVerificationAsync(
        Create<IdentityVerification> request,
        CancellationToken ct = default)
    {
        try
        {
            var rateLimitDecision = await AcquireSecurityRateLimitAsync(
                StrictSecurityRateLimitPolicyMap.Verification,
                request.Metadata,
                $"{request.Metadata.TenantId:D}:{request.Model.CredentialId:D}:{request.Model.VerificationTypeId:D}",
                "verification issuance",
                ct);
            if (!rateLimitDecision.IsAllowed)
                return Result<IdentityVerification>.Failure("Too many requests.", 429);

            var tenant = await _tenantService.GetTenant(
                request.Metadata.TenantId ?? request.Model.TenantId, ct);

            var verificationType = await _dataContext.Query<IdentityVerificationType>()
                .IgnoreQueryFilters()
                .Where(i => i.Id == request.Model.VerificationTypeId)
                .Where(i => !i.IsDeleted && i.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (verificationType is null)
            {
                return Result<IdentityVerification>.NotFound(
                    $"Verification type with id {request.Model.VerificationTypeId} does not exist");
            }

            var identityCredential = await _dataContext.Query<IdentityCredential>()
                .Where(i => i.Id == request.Model.CredentialId)
                .Where(i => i.TenantId == tenant.Id)
                .Where(i => !i.IsDeleted && i.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (identityCredential == null)
            {
                return Result<IdentityVerification>.NotFound(
                    $"Credential with id {request.Model.CredentialId} does not exist");
            }

            var authorization = await _authorizationService.AuthorizeCredentialOperationAsync(
                request.Metadata,
                tenant.Id,
                identityCredential.Id,
                IdentityAuthorizationConstants.Create,
                allowSelf: true,
                ct);
            if (!authorization.IsSuccess)
                return Result<IdentityVerification>.Failure(authorization.Message!, authorization.StatusCode);

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
                    var otp = RandomNumberGenerator.GetInt32(100000, 1000000);
                    var message = messageTemplate.Value.Replace("|Value|", $"{otp}");

                    // Get phone contact via separate query (avoids ThenInclude)
                    var phoneContact = await _dataContext.Query<IdentityContact>()
                        .Include(c => c.Type)
                        .Where(c => c.CredentialId == identityCredential.Id)
                        .Where(c => c.TenantId == tenant.Id && !c.IsDeleted && c.IsEnabled)
                        .Where(c => c.Type != null && !c.Type.IsDeleted && c.Type.IsEnabled)
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
                        TenantId = tenant.Id,
                        Status = (short?)GenericStatusType.Pending,
                        StatusUpdatedOn = DateTime.UtcNow,
                        TokenHash = HashVerificationCode(otp.ToString()),
                        Purpose = IdentityConstants.VerificationPurpose.ContactVerification,
                        Expiry = DateTime.UtcNow.AddMinutes(defaultExpiryMinutes),
                        CredentialId = identityCredential.Id,
                        VerificationTypeId = verificationType.Id,
                        IsEnabled = true,
                        ConcurrencyStamp = Guid.NewGuid()
                    };

                    _dataContext.Add(verification);
                    QueueVerificationDelivery(
                        verification,
                        request.Metadata,
                        MessageTransportType.Sms,
                        contact,
                        "One Time Password",
                        "OTP",
                        message);
                    var smsSaveResult = await _dataContext.SaveChangesAsync(ct);
                    if (!smsSaveResult.IsSuccess)
                        return Result<IdentityVerification>.Failure("Verification could not be created", smsSaveResult.StatusCode);

                    _logger.LogInformation(
                        "Verification created and SMS delivery queued. VerificationId: {VerificationId}, CredentialId: {CredentialId}",
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
                    var emailOtp = RandomNumberGenerator.GetInt32(100000, 1000000);
                    var emailMessage = emailMessageTemplate.Value.Replace("|Value|", $"{emailOtp}");

                    // Get email contact via separate query
                    var emailContact = await _dataContext.Query<IdentityContact>()
                        .Include(c => c.Type)
                        .Where(c => c.CredentialId == identityCredential.Id)
                        .Where(c => c.TenantId == tenant.Id && !c.IsDeleted && c.IsEnabled)
                        .Where(c => c.Type != null && !c.Type.IsDeleted && c.Type.IsEnabled)
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
                        TenantId = tenant.Id,
                        Status = (short?)GenericStatusType.Pending,
                        StatusUpdatedOn = DateTime.UtcNow,
                        TokenHash = HashVerificationCode(emailOtp.ToString()),
                        Purpose = IdentityConstants.VerificationPurpose.ContactVerification,
                        Expiry = DateTime.UtcNow.AddMinutes(defaultExpiryMinutes),
                        CredentialId = identityCredential.Id,
                        VerificationTypeId = verificationType.Id,
                        IsEnabled = true,
                        ConcurrencyStamp = Guid.NewGuid()
                    };

                    _dataContext.Add(emailVerification);
                    QueueVerificationDelivery(
                        emailVerification,
                        request.Metadata,
                        MessageTransportType.Email,
                        emailAddress,
                        "Verification Code",
                        "OTP",
                        emailMessage);
                    var emailSaveResult = await _dataContext.SaveChangesAsync(ct);
                    if (!emailSaveResult.IsSuccess)
                        return Result<IdentityVerification>.Failure("Verification could not be created", emailSaveResult.StatusCode);

                    _logger.LogInformation(
                        "Verification created and email delivery queued. VerificationId: {VerificationId}, CredentialId: {CredentialId}",
                        emailVerification.Id, identityCredential.Id);

                    return Result<IdentityVerification>.Success(emailVerification);
            }

            return Result<IdentityVerification>.Failure(
                "Verification type not supported", 500);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
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
                .IgnoreQueryFilters()
                .Where(i => i.Id == request.Model.Id)
                .Where(i => i.Purpose == IdentityConstants.VerificationPurpose.ContactVerification)
                .Where(i => i.Status == (short?)GenericStatusType.Pending)
                .Where(i => i.Expiry > DateTime.UtcNow)
                .Where(i => i.ConsumedAt == null)
                .Where(i => !i.IsDeleted && i.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (verification == null || verification.FailedAttempts >= MaxVerificationAttempts)
            {
                return Result<IdentityVerification>.Failure("Verification code is invalid or expired", 400);
            }

            if (!VerifyVerificationCode(request.Model.Token, verification.TokenHash))
            {
                var deniedAt = _timeProvider.GetUtcNow();
                var deniedStatus = (short?)GenericStatusType.AccessDenied;
                await _dbContext.Set<IdentityVerification>()
                    .IgnoreQueryFilters()
                    .Where(item => item.Id == verification.Id)
                    .Where(item => item.Purpose == IdentityConstants.VerificationPurpose.ContactVerification)
                    .Where(item => item.Status == (short?)GenericStatusType.Pending)
                    .Where(item => item.Expiry > deniedAt.UtcDateTime)
                    .Where(item => item.ConsumedAt == null)
                    .Where(item => !item.IsDeleted && item.IsEnabled)
                    .Where(item => item.FailedAttempts < MaxVerificationAttempts)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(item => item.FailedAttempts, item => item.FailedAttempts + 1)
                            .SetProperty(
                                item => item.Status,
                                item => item.FailedAttempts + 1 >= MaxVerificationAttempts
                                    ? deniedStatus
                                    : item.Status)
                            .SetProperty(
                                item => item.StatusUpdatedOn,
                                item => item.FailedAttempts + 1 >= MaxVerificationAttempts
                                    ? deniedAt
                                    : item.StatusUpdatedOn)
                            .SetProperty(
                                item => item.ConsumedAt,
                                item => item.FailedAttempts + 1 >= MaxVerificationAttempts
                                    ? deniedAt
                                    : item.ConsumedAt)
                            .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()),
                        ct);

                return Result<IdentityVerification>.Failure("Verification code is invalid or expired", 400);
            }

            // Update verification status to Approved
            _dataContext.Update(verification);
            verification.Status = (short?)GenericStatusType.Approved;
            verification.StatusUpdatedOn = DateTime.UtcNow;
            verification.FailedAttempts = 0;
            verification.ConcurrencyStamp = Guid.NewGuid();

            var saveResult = await _dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result<IdentityVerification>.Failure("Verification could not be approved", saveResult.StatusCode);

            _logger.EntityUpdated("IdentityVerification", verification.Id);

            return Result<IdentityVerification>.Success(verification);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
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
            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId, ct);

            var identityCredential = await _dataContext.Query<IdentityCredential>()
                .Where(i => i.Id == request.CredentialId && i.TenantId == tenant.Id)
                .Where(i => !i.IsDeleted && i.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (identityCredential is null)
            {
                return Result<CheckVerificationResponse>.NotFound(
                    $"Identity credential with id {request.CredentialId} does not exist");
            }

            var authorization = await _authorizationService.AuthorizeCredentialOperationAsync(
                request.Metadata,
                tenant.Id,
                identityCredential.Id,
                IdentityAuthorizationConstants.View,
                allowSelf: true,
                ct);
            if (!authorization.IsSuccess)
                return Result<CheckVerificationResponse>.Failure(authorization.Message!, authorization.StatusCode);

            var verificationType = await _dataContext.Query<IdentityVerificationType>()
                .IgnoreQueryFilters()
                .Where(i => i.Id == request.VerificationTypeId)
                .Where(i => !i.IsDeleted && i.IsEnabled)
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
                .Where(i => i.ConsumedAt == null)
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
                    LastVerification = new VerificationStatusResponse
                    {
                        Id = lastVerification.Id,
                        CredentialId = lastVerification.CredentialId,
                        VerificationTypeId = lastVerification.VerificationTypeId,
                        Status = lastVerification.Status,
                        StatusUpdatedOn = lastVerification.StatusUpdatedOn,
                        Expiry = lastVerification.Expiry,
                        CreatedAt = lastVerification.CreatedAt
                    }
                });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("CheckVerification", "IdentityVerification", Guid.Empty, ex.Message, ex);
            return Result<CheckVerificationResponse>.Failure(
                "An error occurred while checking verification", 500);
        }
    }

    #endregion

    #region Credential Avatars

    public async Task<Result<CredentialAvatarResponse>> UploadCredentialAvatarAsync(
        UploadCredentialAvatarRequest request,
        CancellationToken ct = default)
    {
        Guid? completedStorageFileId = null;
        var avatarPersisted = false;

        try
        {
            var validation = ValidateCredentialAvatarRequest(request);
            if (!validation.IsSuccess)
            {
                return Result<CredentialAvatarResponse>.Failure(
                    validation.Message ?? "Avatar upload request is invalid",
                    validation.StatusCode);
            }

            var tenantId = request.Metadata.TenantId!.Value;
            var authorization = await _authorizationService.AuthorizeCredentialOperationAsync(
                request.Metadata,
                tenantId,
                request.CredentialId,
                IdentityAuthorizationConstants.Update,
                allowSelf: false,
                ct);
            if (!authorization.IsSuccess)
                return Result<CredentialAvatarResponse>.Failure(authorization.Message!, authorization.StatusCode);

            var credential = await FindCredentialForTenant(request.CredentialId, tenantId, ct);
            if (credential is null)
            {
                return Result<CredentialAvatarResponse>.NotFound("Credential not found");
            }

            var contentType = CredentialAvatarPolicy.NormalizeContentType(request.ContentType)!;
            var metadata = await _storageServiceWrapper.EnsureStorageUploadMetadata(
                new EnsureStorageUploadMetadataRequest
                {
                    Metadata = request.Metadata,
                    ContentType = contentType,
                    IdentifierGroupName = CredentialAvatarPolicy.StorageIdentifierGroupName,
                    IdentifierName = CredentialAvatarPolicy.StorageFileIdentifierName,
                    IdentifierDescription = "Identity credential avatar image"
                }, ct);
            if (!metadata.IsSuccess || metadata.Response is null)
            {
                return Result<CredentialAvatarResponse>.Failure(
                    metadata.Message ?? "Avatar storage metadata could not be prepared",
                    ToStatusCode(metadata.HttpStatusCode));
            }

            var uploadResult = await UploadCredentialAvatarToStorageAsync(
                request,
                credential,
                metadata.Response,
                contentType,
                ct);

            if (!uploadResult.IsSuccess || uploadResult.Data is null)
            {
                return Result<CredentialAvatarResponse>.Failure(
                    uploadResult.Message ?? "Avatar upload failed",
                    uploadResult.StatusCode);
            }

            completedStorageFileId = uploadResult.Data.Id;
            var now = DateTime.UtcNow;
            _dataContext.Update(credential);
            credential.AvatarStorageFileId = uploadResult.Data.Id;
            credential.AvatarUrl = ResolveAvatarUrl(uploadResult.Data);
            credential.AvatarUpdatedAt = now;
            credential.ConcurrencyStamp = Guid.NewGuid();
            var claimOutbox = await StageStorageClaimAsync(
                request.Metadata,
                uploadResult.Data.Id,
                ct);

            var saveResult = await _dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
            {
                await DeleteUnattachedAvatarFileAsync(
                    request.Metadata,
                    completedStorageFileId.Value);
                return Result<CredentialAvatarResponse>.Failure("Credential avatar could not be saved", saveResult.StatusCode);
            }

            avatarPersisted = true;
            await TryClaimStorageFileAsync(claimOutbox);
            return Result<CredentialAvatarResponse>.Success(
                CreateCredentialAvatarResponse(credential, uploadResult.Data));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (completedStorageFileId is { } storageFileId && !avatarPersisted)
                await DeleteUnattachedAvatarFileAsync(request.Metadata, storageFileId);

            throw;
        }
        catch (Exception ex)
        {
            if (completedStorageFileId is { } storageFileId && !avatarPersisted)
                await DeleteUnattachedAvatarFileAsync(request.Metadata, storageFileId);

            _logger.OperationFailed("UploadCredentialAvatar", "IdentityCredential", request.CredentialId, ex.Message, ex);
            return Result<CredentialAvatarResponse>.Failure(
                "Credential avatar could not be uploaded",
                500);
        }
    }

    public async Task<Result<CredentialAvatarResponse>> SetCredentialAvatarAsync(
        SetCredentialAvatarRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (request.Metadata.TenantId is not { } tenantId || tenantId == Guid.Empty)
            {
                return Result<CredentialAvatarResponse>.Failure("Tenant context is required", 400);
            }

            var authorization = await _authorizationService.AuthorizeCredentialOperationAsync(
                request.Metadata,
                tenantId,
                request.CredentialId,
                IdentityAuthorizationConstants.Update,
                allowSelf: false,
                ct);
            if (!authorization.IsSuccess)
                return Result<CredentialAvatarResponse>.Failure(authorization.Message!, authorization.StatusCode);

            var credential = await FindCredentialForTenant(request.CredentialId, tenantId, ct);
            if (credential is null)
            {
                return Result<CredentialAvatarResponse>.NotFound("Credential not found");
            }

            var storageResult = await _storageServiceWrapper.GetStorageFile(new GetStorageFileRequest
            {
                Metadata = request.Metadata,
                StorageFileId = request.StorageFileId
            }, ct);
            var storageFile = storageResult.Response;

            if (!storageResult.IsSuccess || storageFile is null)
            {
                return Result<CredentialAvatarResponse>.NotFound("Storage file not found");
            }

            if (storageFile.TenantId != tenantId)
                return Result<CredentialAvatarResponse>.NotFound("Storage file not found");

            if (storageFile.Identifier != credential.Id)
            {
                return Result<CredentialAvatarResponse>.Forbidden(
                    "Storage file is not available for this credential");
            }

            if (!CredentialAvatarPolicy.IsAllowedContentType(storageFile.ContentType))
            {
                return Result<CredentialAvatarResponse>.Failure(
                    "Storage file must be a PNG, JPEG, or WebP image",
                    400);
            }

            if (storageFile.Status != StorageFileStatus.Available ||
                storageFile.ObjectDeletedAt is not null)
            {
                return Result<CredentialAvatarResponse>.Failure(
                    "Storage file is not an available image",
                    400);
            }

            if (storageFile.Visibility != StorageFileVisibility.Public ||
                string.IsNullOrWhiteSpace(storageFile.PublicUrl))
            {
                return Result<CredentialAvatarResponse>.Failure(
                    "Credential avatars must use a public storage file",
                    400);
            }

            if (storageFile.ContentLengthBytes is not > 0 or > CredentialAvatarPolicy.MaxFileSizeBytes)
            {
                return Result<CredentialAvatarResponse>.Failure(
                    $"Storage file must be no larger than {CredentialAvatarPolicy.MaxFileSizeBytes} bytes",
                    400);
            }

            var expectedObjectKeyPrefix = $"{tenantId:N}/{storageFile.Id:N}/";
            if (string.IsNullOrWhiteSpace(storageFile.BucketName) ||
                !string.Equals(storageFile.BlobContainer, storageFile.BucketName, StringComparison.Ordinal) ||
                !string.Equals(storageFile.StorageFileIdentifierName, CredentialAvatarPolicy.StorageFileIdentifierName, StringComparison.Ordinal) ||
                !string.Equals(storageFile.StorageFileIdentifierGroupName, CredentialAvatarPolicy.StorageIdentifierGroupName, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(storageFile.ObjectKey) ||
                !storageFile.ObjectKey.StartsWith(expectedObjectKeyPrefix, StringComparison.Ordinal))
            {
                return Result<CredentialAvatarResponse>.Forbidden(
                    "Storage file is not a credential avatar object");
            }

            _dataContext.Update(credential);
            credential.AvatarStorageFileId = storageFile.Id;
            credential.AvatarUrl = ResolveAvatarUrl(storageFile);
            credential.AvatarUpdatedAt = DateTime.UtcNow;
            credential.ConcurrencyStamp = Guid.NewGuid();
            var claimOutbox = await StageStorageClaimAsync(
                request.Metadata,
                storageFile.Id,
                ct);

            var saveResult = await _dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result<CredentialAvatarResponse>.Failure("Credential avatar could not be saved", saveResult.StatusCode);

            await TryClaimStorageFileAsync(claimOutbox);
            return Result<CredentialAvatarResponse>.Success(
                CreateCredentialAvatarResponse(credential, storageFile));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("SetCredentialAvatar", "IdentityCredential", request.CredentialId, ex.Message, ex);
            return Result<CredentialAvatarResponse>.Failure(
                "Credential avatar could not be updated",
                500);
        }
    }

    public async Task<Result<CredentialAvatarResponse>> RemoveCredentialAvatarAsync(
        RemoveCredentialAvatarRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (request.Metadata.TenantId is not { } tenantId || tenantId == Guid.Empty)
            {
                return Result<CredentialAvatarResponse>.Failure("Tenant context is required", 400);
            }

            var authorization = await _authorizationService.AuthorizeCredentialOperationAsync(
                request.Metadata,
                tenantId,
                request.CredentialId,
                IdentityAuthorizationConstants.Update,
                allowSelf: false,
                ct);
            if (!authorization.IsSuccess)
                return Result<CredentialAvatarResponse>.Failure(authorization.Message!, authorization.StatusCode);

            var credential = await FindCredentialForTenant(request.CredentialId, tenantId, ct);
            if (credential is null)
            {
                return Result<CredentialAvatarResponse>.NotFound("Credential not found");
            }

            _dataContext.Update(credential);
            credential.AvatarStorageFileId = null;
            credential.AvatarUrl = null;
            credential.AvatarUpdatedAt = null;
            credential.ConcurrencyStamp = Guid.NewGuid();

            var saveResult = await _dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result<CredentialAvatarResponse>.Failure("Credential avatar could not be removed", saveResult.StatusCode);

            return Result<CredentialAvatarResponse>.Success(
                CreateCredentialAvatarResponse(credential, (StorageFileResponse?)null));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("RemoveCredentialAvatar", "IdentityCredential", request.CredentialId, ex.Message, ex);
            return Result<CredentialAvatarResponse>.Failure(
                "Credential avatar could not be removed",
                500);
        }
    }

    #endregion
    #region Helper Methods

    private async Task<IdentityCredential?> FindCredentialForTenant(
        Guid credentialId,
        Guid tenantId,
        CancellationToken ct)
    {
        return await _dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters()
            .Where(credential => credential.Id == credentialId)
            .Where(credential => credential.TenantId == tenantId)
            .Where(credential => !credential.IsDeleted && credential.IsEnabled)
            .FirstOrDefaultAsync(ct);
    }

    private static Result ValidateCredentialAvatarRequest(UploadCredentialAvatarRequest request)
    {
        if (request.Metadata.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            return Result.Failure("Tenant context is required", 400);
        }

        if (request.CredentialId == Guid.Empty)
        {
            return Result.Failure("Credential is required", 400);
        }

        if (request.FileBytes is null || request.FileBytes.Length == 0)
        {
            return Result.Failure("Avatar image is required", 400);
        }

        if (request.FileBytes.Length > CredentialAvatarPolicy.MaxFileSizeBytes)
        {
            return Result.Failure("Avatar image must be 5 MB or smaller", 400);
        }

        if (!CredentialAvatarPolicy.IsAllowedContentType(request.ContentType))
        {
            return Result.Failure("Avatar image must be PNG, JPEG, or WebP", 400);
        }

        var normalizedContentType = CredentialAvatarPolicy.NormalizeContentType(request.ContentType)!;
        if (!HasMatchingImageSignature(request.FileBytes, normalizedContentType))
        {
            return Result.Failure("Avatar image content does not match its declared type", 400);
        }

        return Result.Success();
    }

    private static bool HasMatchingImageSignature(byte[] bytes, string contentType) =>
        contentType switch
        {
            "image/png" => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/webp" => bytes.Length >= 12
                            && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                            && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };

    private async Task<Result<StorageFileResponse>> UploadCredentialAvatarToStorageAsync(
        UploadCredentialAvatarRequest request,
        IdentityCredential credential,
        StorageUploadMetadataResponse metadata,
        string contentType,
        CancellationToken ct)
    {
        var fileBytes = request.FileBytes!;
        var extension = CredentialAvatarPolicy.GetFileExtension(contentType);
        var fileName = NormalizeAvatarFileName(request.FileName, extension);
        var sha256Hash = ComputeSha256(fileBytes);
        var session = await _storageServiceWrapper.CreateStorageUploadSession(
            new CreateStorageUploadSessionRequest
            {
                Metadata = request.Metadata,
                FileName = fileName,
                ContentType = contentType,
                TypeId = metadata.TypeId,
                Identifier = credential.Id,
                StorageFileIdentifierId = metadata.StorageFileIdentifierId,
                TotalSizeBytes = fileBytes.LongLength,
                ExpectedSha256Hash = sha256Hash,
                ChunkSizeBytes = fileBytes.Length,
                Visibility = StorageFileVisibility.Public,
                RequireClaim = true
            }, ct);

        if (!session.IsSuccess || session.Response is null)
        {
            return Result<StorageFileResponse>.Failure(
                session.Message ?? "Avatar upload session could not be created",
                ToStatusCode(session.HttpStatusCode));
        }

        try
        {
            var uploadPart = await _storageServiceWrapper.UploadStorageFilePart(
                new UploadStorageFilePartRequest
                {
                    Metadata = request.Metadata,
                    UploadSessionId = session.Response.Id,
                    PartNumber = 1,
                    OffsetBytes = 0,
                    PartSha256Hash = sha256Hash,
                    ChunkBytes = fileBytes
                }, ct);

            if (!uploadPart.IsSuccess)
            {
                await AbortAvatarUploadSessionAsync(
                    request.Metadata,
                    session.Response.Id);
                return Result<StorageFileResponse>.Failure(
                    uploadPart.Message ?? "Avatar image could not be uploaded",
                    ToStatusCode(uploadPart.HttpStatusCode));
            }

            var complete = await _storageServiceWrapper.CompleteStorageUploadSession(
                new CompleteStorageUploadSessionRequest
                {
                    Metadata = request.Metadata,
                    UploadSessionId = session.Response.Id,
                    ExpectedSha256Hash = sha256Hash
                }, ct);

            if (!complete.IsSuccess || complete.Response is null)
            {
                await AbortAvatarUploadSessionAsync(
                    request.Metadata,
                    session.Response.Id);
                return Result<StorageFileResponse>.Failure(
                    complete.Message ?? "Avatar upload could not be completed",
                    ToStatusCode(complete.HttpStatusCode));
            }

            return Result<StorageFileResponse>.Success(complete.Response);
        }
        catch
        {
            await AbortAvatarUploadSessionAsync(
                request.Metadata,
                session.Response.Id);
            throw;
        }
    }

    private async Task AbortAvatarUploadSessionAsync(
        RequestMetadata metadata,
        Guid uploadSessionId)
    {
        using var compensationCts = new CancellationTokenSource(AvatarCompensationTimeout);
        try
        {
            var abort = await _storageServiceWrapper.AbortStorageUploadSession(
                new AbortStorageUploadSessionRequest
                {
                    Metadata = metadata,
                    UploadSessionId = uploadSessionId
                },
                compensationCts.Token);

            if (!abort.IsSuccess)
            {
                _logger.LogError(
                    "Avatar upload session compensation failed. UploadSessionId: {UploadSessionId}, StatusCode: {StatusCode}",
                    uploadSessionId,
                    abort.HttpStatusCode);
            }
        }
        catch (OperationCanceledException) when (compensationCts.IsCancellationRequested)
        {
            _logger.LogError(
                "Avatar upload session compensation timed out. UploadSessionId: {UploadSessionId}",
                uploadSessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Avatar upload session compensation threw. UploadSessionId: {UploadSessionId}",
                uploadSessionId);
        }
    }

    private async Task DeleteUnattachedAvatarFileAsync(
        RequestMetadata metadata,
        Guid storageFileId)
    {
        using var compensationCts = new CancellationTokenSource(AvatarCompensationTimeout);
        try
        {
            var delete = await _storageServiceWrapper.DeleteStorageFile(
                new DeleteStorageFileRequest
                {
                    Metadata = metadata,
                    StorageFileId = storageFileId
                },
                compensationCts.Token);

            if (!delete.IsSuccess)
            {
                _logger.LogError(
                    "Unattached avatar file compensation failed. StorageFileId: {StorageFileId}, StatusCode: {StatusCode}",
                    storageFileId,
                    delete.HttpStatusCode);
                await QueueStorageCleanupAsync(metadata, storageFileId);
            }
        }
        catch (OperationCanceledException) when (compensationCts.IsCancellationRequested)
        {
            _logger.LogError(
                "Unattached avatar file compensation timed out. StorageFileId: {StorageFileId}",
                storageFileId);
            await QueueStorageCleanupAsync(metadata, storageFileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unattached avatar file compensation threw. StorageFileId: {StorageFileId}",
                storageFileId);
            await QueueStorageCleanupAsync(metadata, storageFileId);
        }
    }

    private async Task QueueStorageCleanupAsync(RequestMetadata metadata, Guid storageFileId)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            var tenantId = metadata.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
                return;

            var exists = await db.Set<StorageCleanupOutboxMessage>()
                .IgnoreQueryFilters()
                .AnyAsync(message => message.TenantId == tenantId && message.StorageFileId == storageFileId);
            if (exists)
                return;

            db.Set<StorageCleanupOutboxMessage>().Add(new StorageCleanupOutboxMessage
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                StorageFileId = storageFileId,
                RequestId = metadata.RequestId ?? Guid.NewGuid(),
                IsEnabled = true,
                ConcurrencyStamp = Guid.NewGuid()
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                ex,
                "Storage cleanup could not be persisted. StorageFileId: {StorageFileId}",
                storageFileId);
        }
    }

    private async Task<StorageClaimOutboxMessage?> StageStorageClaimAsync(
        RequestMetadata metadata,
        Guid storageFileId,
        CancellationToken ct)
    {
        var tenantId = metadata.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Tenant context is required to claim an avatar file.");

        var requestId = metadata.RequestId ?? Guid.NewGuid();
        var existing = await _dataContext.Query<StorageClaimOutboxMessage>()
            .IgnoreQueryFilters()
            .Where(message => message.TenantId == tenantId)
            .Where(message => message.StorageFileId == storageFileId)
            .Where(message => message.RequestId == requestId)
            .FirstOrDefaultAsync(ct);
        if (existing?.ProcessedAt is not null)
            return null;
        if (existing is not null)
            return existing;

        var outbox = new StorageClaimOutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StorageFileId = storageFileId,
            RequestId = requestId,
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        };
        _dataContext.Add(outbox);
        return outbox;
    }

    private async Task TryClaimStorageFileAsync(StorageClaimOutboxMessage? outbox)
    {
        if (outbox is null)
            return;

        using var claimCts = new CancellationTokenSource(AvatarCompensationTimeout);
        try
        {
            var result = await _storageServiceWrapper.ClaimStorageFile(new ClaimStorageFileRequest
            {
                StorageFileId = outbox.StorageFileId,
                Metadata = new RequestMetadata
                {
                    TenantId = outbox.TenantId,
                    RequestId = outbox.RequestId
                }
            }, claimCts.Token);
            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Immediate avatar storage claim was not accepted. StorageFileId: {StorageFileId}, StatusCode: {StatusCode}",
                    outbox.StorageFileId,
                    result.HttpStatusCode);
                return;
            }

            outbox.ProcessedAt = DateTime.UtcNow;
            outbox.LastError = null;
            outbox.NextAttemptAt = null;
            outbox.ModifiedAt = DateTime.UtcNow;
            outbox.ConcurrencyStamp = Guid.NewGuid();
            _dataContext.Update(outbox);
            var saveResult = await _dataContext.SaveChangesAsync();
            if (!saveResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Immediate avatar storage claim acknowledgement could not be persisted. StorageFileId: {StorageFileId}",
                    outbox.StorageFileId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Immediate avatar storage claim failed; the durable outbox will retry. StorageFileId: {StorageFileId}",
                outbox.StorageFileId);
        }
    }

    private void QueueVerificationDelivery(
        IdentityVerification verification,
        RequestMetadata metadata,
        MessageTransportType transportType,
        string recipient,
        string subject,
        string intent,
        string message)
    {
        if (verification.Id == Guid.Empty)
            verification.Id = Guid.NewGuid();

        _dataContext.Add(new VerificationDeliveryOutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = verification.TenantId,
            VerificationId = verification.Id,
            RequestId = metadata.RequestId ?? Guid.NewGuid(),
            TransportType = (int)transportType,
            Recipient = recipient,
            Subject = subject,
            Intent = intent,
            Message = message,
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        });
    }

    private static string NormalizeAvatarFileName(string? fileName, string extension)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            return $"avatar{extension}";
        }

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(safeName);
        if (string.IsNullOrWhiteSpace(nameWithoutExtension))
        {
            return $"avatar{extension}";
        }

        if (nameWithoutExtension.Length > 120)
        {
            nameWithoutExtension = nameWithoutExtension[..120];
        }

        return $"{nameWithoutExtension}{extension}";
    }

    private static string ComputeSha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string ComputeTokenHash(string? token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty)))
            .ToLowerInvariant();

    private async Task<DistributedSecurityRateLimitDecision> AcquireAuthenticationRateLimitAsync(
        AuthenticateIdentityRequest request,
        CancellationToken ct)
    {
        try
        {
            return await _securityRateLimiter.AcquireAsync(
                StrictSecurityRateLimitPolicyMap.Authentication,
                await CreateTrustedRateLimitKeyAsync(request.Metadata, request.UserName, ct),
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed authentication throttling failed; authentication was denied");
            return DistributedSecurityRateLimitDecision.Rejected(TimeSpan.Zero);
        }
    }

    private async Task<DistributedSecurityRateLimitDecision> AcquireSecurityRateLimitAsync(
        StrictSecurityRateLimitPolicy policy,
        RequestMetadata metadata,
        string identifier,
        string operation,
        CancellationToken ct)
    {
        try
        {
            return await _securityRateLimiter.AcquireAsync(
                policy,
                await CreateTrustedRateLimitKeyAsync(metadata, identifier, ct),
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed throttling failed; {Operation} was denied", operation);
            return DistributedSecurityRateLimitDecision.Rejected(TimeSpan.Zero);
        }
    }

    private async Task<string> CreateTrustedRateLimitKeyAsync(
        RequestMetadata metadata,
        string? identifier,
        CancellationToken ct)
    {
        var remoteIpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrWhiteSpace(remoteIpAddress))
        {
            return StrictSecurityRateLimitPolicyMap.CreateAuthenticationClientKey(
                remoteIpAddress,
                identifier);
        }

        if (!string.IsNullOrWhiteSpace(metadata.ServiceAccessToken))
        {
            var trustedInvocation = await _trustedServiceInvocationResolver.ResolveAsync(
                metadata,
                XFrameworkServiceNames.IdentityServer,
                requireTenant: false,
                ct: ct);
            if (trustedInvocation.IsSuccess)
            {
                return StrictSecurityRateLimitPolicyMap.CreateAuthenticationClientKey(
                    null,
                    $"service:{trustedInvocation.Invocation!.CallerClientId}:{identifier}");
            }

            _logger.LogWarning(
                "Authentication throttling could not resolve trusted Bolt caller; using shared untrusted partition");
        }

        return StrictSecurityRateLimitPolicyMap.CreateAuthenticationClientKey(
            null,
            $"untrusted:{identifier}");
    }

    private async Task<IdentityCredential?> LockCredentialForAuthenticationAsync(
        Guid credentialId,
        CancellationToken ct)
    {
        var credentials = await _dbContext.Set<IdentityCredential>()
            .FromSqlInterpolated(
                $"SELECT * FROM \"Identity\".\"IdentityCredential\" WHERE \"ID\" = {credentialId} FOR UPDATE")
            .IgnoreQueryFilters()
            .AsTracking()
            .ToListAsync(ct);

        return credentials.SingleOrDefault();
    }

    private static string HashVerificationCode(string code) =>
        BCrypt.Net.BCrypt.HashPassword(code, workFactor: 11);

    private static bool VerifyVerificationCode(string? code, string? hash) =>
        !string.IsNullOrWhiteSpace(code)
        && !string.IsNullOrWhiteSpace(hash)
        && BCrypt.Net.BCrypt.Verify(code, hash);

    private static int ToStatusCode(HttpStatusCode statusCode) =>
        statusCode == 0 ? 500 : (int)statusCode;

    private static string? ResolveAvatarUrl(StorageFileResponse storageFile)
    {
        if (!string.IsNullOrWhiteSpace(storageFile.CdnBaseUrl))
        {
            return storageFile.CdnBaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(storageFile.PublicUrl))
        {
            return storageFile.PublicUrl;
        }

        return storageFile.ObjectKey;
    }

    private static CredentialAvatarResponse CreateCredentialAvatarResponse(
        IdentityCredential credential,
        StorageFileResponse? storageFile)
    {
        return new CredentialAvatarResponse
        {
            CredentialId = credential.Id,
            StorageFileId = credential.AvatarStorageFileId,
            AvatarUrl = credential.AvatarUrl,
            ContentType = storageFile?.ContentType,
            FileName = storageFile?.Name,
            AvatarUpdatedAt = credential.AvatarUpdatedAt
        };
    }

    private static CredentialAdministrationResponse CreateCredentialAdministrationResponse(
        IdentityCredential credential) => new()
    {
        Id = credential.Id,
        TenantId = credential.TenantId,
        IdentityInfoId = credential.IdentityInfoId,
        UserName = credential.UserName,
        UserAlias = credential.UserAlias,
        IsEnabled = credential.IsEnabled,
        ConcurrencyStamp = credential.ConcurrencyStamp,
        CreatedAt = credential.CreatedAt,
        ModifiedAt = credential.ModifiedAt,
        AvatarStorageFileId = credential.AvatarStorageFileId,
        AvatarUrl = credential.AvatarUrl,
        AvatarUpdatedAt = credential.AvatarUpdatedAt
    };

    /// <summary>
    /// Validates user authorization with multi-type authentication support.
    /// Supports Username, Email, Phone, and UsernameEmailPhone authentication.
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
                    .Where(i => !i.IsDeleted && i.IsEnabled)
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
                    .Where(i => i.TenantId == tenant.Id && i.UserName == userName)
                    .FirstOrDefaultAsync(ct);

                // Try email if username not found
                if (result is null)
                {
                    var emailContact = await _dataContext.Query<IdentityContact>()
                        .IgnoreQueryFilters()
                        .Include(c => c.Type)
                        .Where(i =>
                            i.TenantId == tenant.Id &&
                            i.Value == userName &&
                            !i.IsDeleted && i.IsEnabled &&
                            i.Type != null &&
                            !i.Type.IsDeleted && i.Type.IsEnabled &&
                            i.Type.Name == nameof(GenericContactType.Email))
                        .FirstOrDefaultAsync(ct);

                    if (emailContact != null)
                    {
                        result = await _dataContext.Query<IdentityCredential>()
                            .IgnoreQueryFilters()
                            .Include(i => i.IdentityInfo)
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
                            i.TenantId == tenant.Id &&
                            i.Value == userName.ValidatePhoneNumber(true) &&
                            !i.IsDeleted && i.IsEnabled &&
                            i.Type != null &&
                            !i.Type.IsDeleted && i.Type.IsEnabled &&
                            i.Type.Name == nameof(GenericContactType.Phone))
                        .FirstOrDefaultAsync(ct);

                    if (phoneContact != null)
                    {
                        result = await _dataContext.Query<IdentityCredential>()
                            .IgnoreQueryFilters()
                            .Include(i => i.IdentityInfo)
                            .Where(i => i.Id == phoneContact.CredentialId)
                            .FirstOrDefaultAsync(ct);
                    }
                }
                break;

            case AuthorizationType.Username:
                result = await _dataContext.Query<IdentityCredential>()
                    .IgnoreQueryFilters()
                    .Include(i => i.IdentityInfo)
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
                        i.TenantId == tenant.Id &&
                        i.Value == userName &&
                        !i.IsDeleted && i.IsEnabled &&
                        i.Type != null &&
                        !i.Type.IsDeleted && i.Type.IsEnabled &&
                        i.Type.Name == nameof(GenericContactType.Email))
                    .FirstOrDefaultAsync(ct);

                result = emailContactForAuth != null
                    ? await _dataContext.Query<IdentityCredential>()
                        .IgnoreQueryFilters()
                        .Include(i => i.IdentityInfo)
                        .Where(i => i.Id == emailContactForAuth.CredentialId)
                        .FirstOrDefaultAsync(ct)
                    : null;
                break;

            case AuthorizationType.Phone:
                var phoneContactForAuth = await _dataContext.Query<IdentityContact>()
                    .IgnoreQueryFilters()
                    .Include(c => c.Type)
                    .Where(i =>
                        i.TenantId == tenant.Id &&
                        i.Value == userName.ValidatePhoneNumber(true) &&
                        !i.IsDeleted && i.IsEnabled &&
                        i.Type != null &&
                        !i.Type.IsDeleted && i.Type.IsEnabled &&
                        i.Type.Name == nameof(GenericContactType.Phone))
                    .FirstOrDefaultAsync(ct);

                result = phoneContactForAuth != null
                    ? await _dataContext.Query<IdentityCredential>()
                        .IgnoreQueryFilters()
                        .Include(i => i.IdentityInfo)
                        .Where(i => i.Id == phoneContactForAuth.CredentialId)
                        .FirstOrDefaultAsync(ct)
                    : null;
                break;

            case AuthorizationType.Token:
                throw new ArgumentOutOfRangeException(
                    nameof(authorizationType),
                    "Service token authentication is not supported by the user authentication endpoint.");

            default:
                throw new ArgumentOutOfRangeException(nameof(authorizationType));
        }

        if (result is null || result.IsDeleted || !result.IsEnabled)
            return null;

        if (result.IdentityInfo is { IsDeleted: true } or { IsEnabled: false })
            return null;

        return result;
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
        var now = DateTime.UtcNow;
        var roleList = await _dataContext.Query<IdentityRole>()
            .IgnoreQueryFilters()
            .Include(i => i.Type)
            .Where(i => i.TenantId == credential.TenantId)
            .Where(i => i.CredentialId == credential.Id)
            .Where(i => !i.IsDeleted && i.IsEnabled)
            .Where(i => i.Type != null && !i.Type.IsDeleted && i.Type.IsEnabled)
            .Where(i => i.Type!.TenantId == credential.TenantId)
            .Where(i => i.RoleExpiration >= now)
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
            SessionId = sessionId,
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        };

        _dataContext.Add(authorizationLog);
    }

    /// <summary>
    /// Creates a session entity for tracking user sessions with expiration.
    /// </summary>
    private Session CreateSession(
        Guid tenantId,
        Guid credentialId,
        Guid sessionTypeId,
        JwtToken token,
        DateTime? expiresAt = null)
    {
        var session = new Session
        {
            Id = token.SessionId,
            TenantId = tenantId,
            SessionTypeId = sessionTypeId,
            CredentialId = credentialId,
            RefreshTokenHash = ComputeTokenHash(token.RefreshToken),
            RefreshTokenExpiresAt = token.RefreshTokenExpiresAt,
            Status = CurrentSessionState.Active,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddHours(DefaultSessionExpirationHours),
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        };

        _dataContext.Add(session);
        return session;
    }

    private async Task RevokeActiveSessionsAsync(
        Guid tenantId,
        Guid credentialId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var concurrencyStamp = Guid.NewGuid();
        await _dbContext.Set<Session>()
            .IgnoreQueryFilters()
            .Where(session => session.TenantId == tenantId)
            .Where(session => session.CredentialId == credentialId)
            .Where(session => session.Status == CurrentSessionState.Active && !session.IsDeleted)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(session => session.Status, CurrentSessionState.Inactive)
                    .SetProperty(session => session.ModifiedAt, now)
                    .SetProperty(session => session.ConcurrencyStamp, concurrencyStamp),
                ct);
    }

    private async Task RevokeSessionAsync(Session session, CancellationToken ct)
    {
        _dataContext.Update(session);
        session.Status = CurrentSessionState.Inactive;
        session.ModifiedAt = DateTime.UtcNow;
        session.ConcurrencyStamp = Guid.NewGuid();

        var saveResult = await _dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            _logger.OperationFailed("RevokeSession", "Session", session.Id, saveResult.Message ?? string.Empty, null);
    }

    /// <summary>
    /// Gets the session type ID based on authorization type.
    /// User session for standard auth, Service session for token-based auth.
    /// </summary>
    private async Task<Guid> GetSessionTypeId(
        Guid tenantId,
        AuthorizationType authorizationType,
        CancellationToken ct)
    {
        Guid? sessionTypeId;

        if (authorizationType is not AuthorizationType.Token)
        {
            // User session type
            var cacheKey = $"identity:tenant:{tenantId}:session-type:user";
            sessionTypeId = _cache.Get<Guid>(cacheKey);
            if (sessionTypeId is null || sessionTypeId == Guid.Empty)
            {
                var userSessionType = await _dataContext.Query<SessionType>()
                    .IgnoreQueryFilters()
                    .Where(i => i.TenantId == tenantId)
                    .Where(i => i.SystemReferenceId == IdentityConstants.SessionType.User)
                    .Where(i => i.Name == "User")
                    .FirstOrDefaultAsync(ct);

                sessionTypeId = userSessionType?.Id;
                if (sessionTypeId is { } resolvedUserSessionTypeId && resolvedUserSessionTypeId != Guid.Empty)
                    await _cache.Set(cacheKey, resolvedUserSessionTypeId);
            }
        }
        else
        {
            // Service/Token session type
            var cacheKey = $"identity:tenant:{tenantId}:session-type:token";
            sessionTypeId = _cache.Get<Guid>(cacheKey);
            if (sessionTypeId is null || sessionTypeId == Guid.Empty)
            {
                var serviceSessionType = await _dataContext.Query<SessionType>()
                    .IgnoreQueryFilters()
                    .Where(i => i.TenantId == tenantId)
                    .Where(i => i.SystemReferenceId == IdentityConstants.SessionType.Service)
                    .Where(i => i.Name == "Service")
                    .FirstOrDefaultAsync(ct);

                sessionTypeId = serviceSessionType?.Id;
                if (sessionTypeId is { } resolvedServiceSessionTypeId && resolvedServiceSessionTypeId != Guid.Empty)
                    await _cache.Set(cacheKey, resolvedServiceSessionTypeId);
            }
        }

        if (sessionTypeId is null || sessionTypeId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Tenant '{tenantId}' does not have the required session type for '{authorizationType}'.");
        }

        return sessionTypeId.Value;
    }

    #endregion

    #region Logout & Refresh Token

    public async Task<Result> LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        try
        {
            if (request.Metadata.TenantId is not { } tenantId || tenantId == Guid.Empty)
                return Result.Forbidden("Tenant context is required");

            var isTrustedServiceCall = !string.IsNullOrWhiteSpace(request.Metadata.ServiceAccessToken);
            if (!isTrustedServiceCall &&
                (request.Metadata.CredentialId is not { } actorCredentialId ||
                 actorCredentialId == Guid.Empty ||
                 actorCredentialId != request.CredentialId))
            {
                return Result.Forbidden("Session does not belong to the authenticated credential");
            }

            await using var logoutTransaction = _dbContext.Database.CurrentTransaction is null
                ? await _dbContext.Database.BeginTransactionAsync(ct)
                : null;
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var updated = await _dbContext.Set<Session>()
                .IgnoreQueryFilters()
                .Where(session => session.Id == request.SessionId)
                .Where(session => session.CredentialId == request.CredentialId)
                .Where(session => session.TenantId == tenantId)
                .Where(session => !session.IsDeleted)
                .Where(session => session.Status != CurrentSessionState.Inactive)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(session => session.Status, CurrentSessionState.Inactive)
                        .SetProperty(session => session.ModifiedAt, now)
                        .SetProperty(session => session.ConcurrencyStamp, Guid.NewGuid()),
                    ct);

            if (updated == 0)
            {
                var existingStatus = await _dbContext.Set<Session>()
                    .IgnoreQueryFilters()
                    .Where(session => session.Id == request.SessionId)
                    .Where(session => session.CredentialId == request.CredentialId)
                    .Where(session => session.TenantId == tenantId)
                    .Where(session => !session.IsDeleted)
                    .Select(session => (CurrentSessionState?)session.Status)
                    .SingleOrDefaultAsync(ct);

                if (existingStatus == CurrentSessionState.Inactive)
                    return Result.Failure("Session is already inactive", 400);

                _logger.EntityNotFound("Session", request.SessionId);
                return Result.NotFound("Session not found");
            }

            await CreateAuthorizationLog(
                tenantId,
                request.CredentialId,
                request.Metadata?.IpAddress ?? string.Empty,
                request.Metadata?.Name ?? string.Empty,
                request.Metadata?.DeviceName ?? string.Empty,
                request.Metadata?.DeviceAgent ?? string.Empty,
                AuthenticationState.NotAuthenticated,
                request.SessionId);

            var saveResult = await _dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result.Failure("Logout could not be persisted", saveResult.StatusCode);

            if (logoutTransaction is not null)
                await logoutTransaction.CommitAsync(ct);

            _logger.UserLoggedOut(request.CredentialId);

            return Result.Success("Logged out successfully");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("Logout", "Session", request.SessionId, ex.Message, ex);
            return Result.Failure("An error occurred during logout", 500);
        }
    }

    public async Task<Result<ValidateIdentitySessionResponse>> ValidateIdentitySessionAsync(
        ValidateIdentitySessionRequest request,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var sessionIsActive = await _dataContext.Query<Session>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(session => session.Id == request.SessionId)
            .Where(session => session.TenantId == request.TenantId)
            .Where(session => session.CredentialId == request.CredentialId)
            .Where(session => session.Status == CurrentSessionState.Active)
            .Where(session => !session.IsDeleted && session.IsEnabled)
            .Where(session => session.ExpiresAt == null || session.ExpiresAt > now)
            .AnyAsync(ct);
        if (!sessionIsActive)
            return Result<ValidateIdentitySessionResponse>.Failure("Identity session is no longer valid", 401);

        var tenantIsActive = await _dataContext.Query<Tenant>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(tenant => tenant.Id == request.TenantId)
            .Where(tenant => !tenant.IsDeleted && tenant.IsEnabled)
            .Where(tenant => tenant.AvailabilityDate == null || tenant.AvailabilityDate <= now)
            .Where(tenant => tenant.Expiration == null || tenant.Expiration > now)
            .AnyAsync(ct);
        if (!tenantIsActive)
            return Result<ValidateIdentitySessionResponse>.Failure("Identity session is no longer valid", 401);

        var credential = await _dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(item => item.Id == request.CredentialId)
            .Where(item => item.TenantId == request.TenantId)
            .Where(item => !item.IsDeleted && item.IsEnabled)
            .FirstOrDefaultAsync(ct);
        if (credential is null)
            return Result<ValidateIdentitySessionResponse>.Failure("Identity session is no longer valid", 401);

        var identityIsActive = await _dataContext.Query<IdentityInformation>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(identity => identity.Id == credential.IdentityInfoId)
            .Where(identity => identity.TenantId == request.TenantId)
            .Where(identity => !identity.IsDeleted && identity.IsEnabled)
            .AnyAsync(ct);
        if (!identityIsActive)
            return Result<ValidateIdentitySessionResponse>.Failure("Identity session is no longer valid", 401);

        if (request.RoleTypeIds.Any(roleTypeId => roleTypeId == Guid.Empty))
            return Result<ValidateIdentitySessionResponse>.Failure("Identity session is no longer valid", 401);

        var claimedRoleTypeIds = request.RoleTypeIds.Distinct().ToList();
        if (claimedRoleTypeIds.Count > 0)
        {
            var activeRoles = await _dataContext.Query<IdentityRole>()
                .IgnoreQueryFilters()
                .NoCache()
                .Where(role => role.TenantId == request.TenantId)
                .Where(role => role.CredentialId == request.CredentialId)
                .Where(role => role.TypeId != null && claimedRoleTypeIds.Contains(role.TypeId.Value))
                .Where(role => !role.IsDeleted && role.IsEnabled)
                .Where(role => role.RoleExpiration >= now)
                .ToListAsync(ct);

            var activeRoleTypeIds = activeRoles
                .Select(role => role.TypeId!.Value)
                .Distinct()
                .ToList();

            var enabledRoleTypes = await _dataContext.Query<IdentityRoleType>()
                .IgnoreQueryFilters()
                .NoCache()
                .Where(roleType => roleType.TenantId == request.TenantId)
                .Where(roleType => activeRoleTypeIds.Contains(roleType.Id))
                .Where(roleType => !roleType.IsDeleted && roleType.IsEnabled)
                .ToListAsync(ct);

            if (claimedRoleTypeIds.Except(enabledRoleTypes.Select(roleType => roleType.Id)).Any())
                return Result<ValidateIdentitySessionResponse>.Failure("Identity session is no longer valid", 401);
        }

        return Result<ValidateIdentitySessionResponse>.Success(new ValidateIdentitySessionResponse
        {
            TenantId = request.TenantId,
            CredentialId = request.CredentialId,
            SessionId = request.SessionId,
            IsValid = true
        });
    }

    public async Task<Result<RefreshTokenResponse>> RefreshTokenAsync(
        RefreshTokenRequest request, CancellationToken ct = default)
    {
        try
        {
            var rateLimitDecision = await AcquireSecurityRateLimitAsync(
                StrictSecurityRateLimitPolicyMap.Refresh,
                request.Metadata,
                $"{request.Metadata.TenantId:D}:{request.SessionId:D}",
                "refresh token",
                ct);
            if (!rateLimitDecision.IsAllowed)
                return Result<RefreshTokenResponse>.Failure("Too many requests.", 429);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var session = await _dataContext.Query<Session>()
                .IgnoreQueryFilters()
                .Where(s => s.Id == request.SessionId)
                .Where(s => s.Status == CurrentSessionState.Active)
                .Where(s => !s.IsDeleted && s.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (session is null)
            {
                _logger.EntityNotFound("Session", request.SessionId);
                return Result<RefreshTokenResponse>.NotFound("Session not found or inactive");
            }

            // Check session expiration - SECURITY CRITICAL
            if (session.ExpiresAt.HasValue && session.ExpiresAt.Value <= now)
            {
                _dataContext.Update(session);
                session.Status = CurrentSessionState.Expired;
                session.ModifiedAt = now;
                session.ConcurrencyStamp = Guid.NewGuid();
                var expirySaveResult = await _dataContext.SaveChangesAsync(ct);
                if (!expirySaveResult.IsSuccess)
                    return Result<RefreshTokenResponse>.Failure("Session expiration could not be persisted", expirySaveResult.StatusCode);

                _logger.TokenValidationFailed(session.CredentialId, "Session has expired");
                return Result<RefreshTokenResponse>.Failure("Session has expired. Please log in again.", 401);
            }

            if (!session.RefreshTokenExpiresAt.HasValue || session.RefreshTokenExpiresAt.Value <= now)
            {
                _dataContext.Update(session);
                session.RefreshTokenHash = null;
                session.ModifiedAt = now;
                session.ConcurrencyStamp = Guid.NewGuid();
                var refreshExpirySave = await _dataContext.SaveChangesAsync(ct);
                if (!refreshExpirySave.IsSuccess)
                    return Result<RefreshTokenResponse>.Failure(
                        "Refresh-token expiration could not be persisted",
                        refreshExpirySave.StatusCode);

                _logger.TokenValidationFailed(session.CredentialId, "Refresh token has expired");
                return Result<RefreshTokenResponse>.Failure("Invalid refresh token", 401);
            }

            var tenantIsActive = await _dataContext.Query<Tenant>()
                .IgnoreQueryFilters()
                .Where(t => t.Id == session.TenantId)
                .Where(t => !t.IsDeleted && t.IsEnabled)
                .Where(t => t.AvailabilityDate == null || t.AvailabilityDate <= now)
                .Where(t => t.Expiration == null || t.Expiration > now)
                .AnyAsync(ct);

            var credential = await _dataContext.Query<IdentityCredential>()
                .IgnoreQueryFilters()
                .Include(c => c.IdentityInfo)
                .Where(c => c.Id == session.CredentialId && c.TenantId == session.TenantId)
                .Where(c => !c.IsDeleted && c.IsEnabled)
                .Where(c => c.IdentityInfo != null && !c.IdentityInfo.IsDeleted && c.IdentityInfo.IsEnabled)
                .FirstOrDefaultAsync(ct);

            var activeRoles = credential is null
                ? []
                : await GetRoleList(credential, ct) ?? [];

            if (!tenantIsActive || credential is null || activeRoles.Count == 0)
            {
                await RevokeSessionAsync(session, ct);
                return Result<RefreshTokenResponse>.Failure("Session is no longer authorized", 401);
            }

            // Validate the one-way refresh-token hash without persisting bearer tokens.
            var suppliedRefreshHash = ComputeTokenHash(request.RefreshToken);
            if (string.IsNullOrWhiteSpace(session.RefreshTokenHash) ||
                !CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(session.RefreshTokenHash),
                    Encoding.ASCII.GetBytes(suppliedRefreshHash)))
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

            var tenantIdClaim = principal.FindFirstValue("tenant_id")
                                ?? principal.FindFirstValue("tenantId");
            if (!Guid.TryParse(tenantIdClaim, out var tokenTenantId) ||
                tokenTenantId != session.TenantId)
            {
                _logger.TokenValidationFailed(session.CredentialId, "Access token does not match session tenant");
                return Result<RefreshTokenResponse>.Failure("Invalid access token", 401);
            }

            var sessionIdClaim = principal.FindFirstValue("session_id");
            if (!Guid.TryParse(sessionIdClaim, out var tokenSessionId) ||
                tokenSessionId != session.Id)
            {
                _logger.TokenValidationFailed(session.CredentialId, "Access token does not match refresh session");
                return Result<RefreshTokenResponse>.Failure("Invalid access token", 401);
            }

            // Generate new token pair from existing claims
            var renewedClaims = new List<Claim>
            {
                new(ClaimTypes.GivenName, credential.UserName ?? credential.UserAlias ?? credential.Id.ToString("D")),
                new(ClaimTypes.Role, JsonSerializer.Serialize(
                    activeRoles.Where(role => role.TypeId.HasValue).Select(role => role.TypeId!.Value).ToList())),
                new(ClaimTypes.Name, credential.Id.ToString("D")),
                new("credential_id", credential.Id.ToString("D")),
                new("tenant_id", credential.TenantId.ToString("D")),
                new("tenantId", credential.TenantId.ToString("D")),
                new("session_id", session.Id.ToString("D")),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
                new(JwtRegisteredClaimNames.AuthTime,
                    principal.FindFirstValue(JwtRegisteredClaimNames.AuthTime) ?? now.ToString("O"))
            };
            var newToken = await _jwtService.GenerateToken(renewedClaims);

            // Rotate the refresh token and concurrency stamp as a single-use transition.
            _dataContext.Update(session);
            session.RefreshTokenHash = ComputeTokenHash(newToken.RefreshToken);
            session.RefreshTokenExpiresAt = newToken.RefreshTokenExpiresAt;
            session.ModifiedAt = now;
            session.ConcurrencyStamp = Guid.NewGuid();
            var saveResult = await _dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result<RefreshTokenResponse>.Failure("Refresh token has already been used", 409);

            // Extract expiration from the newly generated token
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(newToken.AccessToken);
            var expiresIn = (int)(jwt.ValidTo - now).TotalSeconds;

            return Result<RefreshTokenResponse>.Success(new RefreshTokenResponse
            {
                AccessToken = newToken.AccessToken,
                RefreshToken = newToken.RefreshToken,
                SessionId = newToken.SessionId,
                ExpiresIn = expiresIn
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
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
        var resetIdentifier = request.Email ?? request.Phone ?? string.Empty;
        var rateLimitDecision = await AcquireSecurityRateLimitAsync(
            StrictSecurityRateLimitPolicyMap.PasswordReset,
            request.Metadata,
            $"{request.Metadata.TenantId:D}:{resetIdentifier}",
            "password reset request",
            ct);
        if (!rateLimitDecision.IsAllowed)
            return Result.Failure("Too many requests.", 429);

        if (request.Metadata.TenantId is not { } tenantId)
            return Result.Failure("Tenant context is required.", 400);

        var requestId = request.Metadata.RequestId ?? Guid.NewGuid();
        var alreadyAccepted = await _dataContext.Query<PasswordResetOutboxMessage>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(message => message.TenantId == tenantId && message.RequestId == requestId)
            .AnyAsync(ct);
        if (!alreadyAccepted)
        {
            var outboxMessage = new PasswordResetOutboxMessage
            {
                TenantId = tenantId,
                RequestId = requestId,
                Email = request.Email,
                Phone = request.Phone,
                IsEnabled = true,
                ConcurrencyStamp = Guid.NewGuid()
            };
            _dbContext.Add(outboxMessage);
            try
            {
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsPasswordResetRequestConflict(ex))
            {
                _dbContext.Entry(outboxMessage).State = EntityState.Detached;
                var acceptedByConcurrentRequest = await _dbContext.Set<PasswordResetOutboxMessage>()
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .AnyAsync(message =>
                        message.TenantId == tenantId && message.RequestId == requestId, ct);
                if (!acceptedByConcurrentRequest)
                    return Result.Failure("Password reset request could not be accepted.", 503);
            }
            catch (DbUpdateException ex)
            {
                _dbContext.Entry(outboxMessage).State = EntityState.Detached;
                _logger.LogError(ex, "Password reset outbox request {RequestId} could not be persisted.", requestId);
                return Result.Failure("Password reset request could not be accepted.", 503);
            }
        }

        return Result.Success("If an account exists with that contact information, a password reset link has been sent.");
    }

    private static bool IsPasswordResetRequestConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: PasswordResetRequestConstraint
        };

    public async Task<Result> ProcessForgotPasswordAsync(
        ForgotPasswordRequest request, CancellationToken ct = default)
    {
        try
        {
            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId, ct);

            // Determine lookup method based on input
            IdentityCredential? credential = null;
            string? recipientAddress = null;
            MessageTransportType transportType;

            if (!string.IsNullOrEmpty(request.Email))
            {
                // Lookup credential by email contact
                var emailContact = await _dataContext.Query<IdentityContact>()
                    .Include(c => c.Type)
                    .Where(c => c.TenantId == tenant.Id)
                    .Where(c => c.Value == request.Email)
                    .Where(c => !c.IsDeleted && c.IsEnabled)
                    .Where(c => c.Type != null && !c.Type.IsDeleted && c.Type.IsEnabled)
                    .Where(c => c.Type != null && c.Type.Name == nameof(GenericContactType.Email))
                    .FirstOrDefaultAsync(ct);

                if (emailContact != null)
                {
                    credential = await _dataContext.Query<IdentityCredential>()
                        .Where(c => c.Id == emailContact.CredentialId)
                        .Where(c => c.TenantId == tenant.Id)
                        .Where(c => !c.IsDeleted && c.IsEnabled)
                        .Where(c => c.IdentityInfo != null && !c.IdentityInfo.IsDeleted && c.IdentityInfo.IsEnabled)
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
                    .Where(c => c.TenantId == tenant.Id)
                    .Where(c => c.Value == request.Phone)
                    .Where(c => !c.IsDeleted && c.IsEnabled)
                    .Where(c => c.Type != null && !c.Type.IsDeleted && c.Type.IsEnabled)
                    .Where(c => c.Type != null && c.Type.Name == nameof(GenericContactType.Phone))
                    .FirstOrDefaultAsync(ct);

                if (phoneContact != null)
                {
                    credential = await _dataContext.Query<IdentityCredential>()
                        .Where(c => c.Id == phoneContact.CredentialId)
                        .Where(c => c.TenantId == tenant.Id)
                        .Where(c => !c.IsDeleted && c.IsEnabled)
                        .Where(c => c.IdentityInfo != null && !c.IdentityInfo.IsDeleted && c.IdentityInfo.IsEnabled)
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
                TenantId = tenant.Id,
                Status = (short?)GenericStatusType.Pending,
                StatusUpdatedOn = DateTime.UtcNow,
                TokenHash = ComputeTokenHash(resetToken),
                Purpose = IdentityConstants.VerificationPurpose.PasswordReset,
                Expiry = DateTime.UtcNow.AddMinutes(PasswordResetTokenExpirationMinutes),
                CredentialId = credential.Id,
                VerificationTypeId = verificationTypeId,
                IsEnabled = true,
                ConcurrencyStamp = Guid.NewGuid()
            };

            _dataContext.Add(verification);
            var messageTemplate = await _dataContext.Query<RegistryConfiguration>()
                .Where(i => i.TenantId == tenant.Id)
                .Where(i => i.Group != null && i.Group.Name == "CommunicationsService_PasswordReset")
                .FirstOrDefaultAsync(ct);

            var message = messageTemplate?.Value?.Replace("|Token|", resetToken)
                ?? $"Your password reset token is: {resetToken}. This token expires in {PasswordResetTokenExpirationMinutes} minutes.";

            QueueVerificationDelivery(
                verification,
                request.Metadata,
                transportType,
                recipientAddress,
                "Password Reset Request",
                "PasswordReset",
                message);
            var saveResult = await _dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result.Failure("Password reset verification could not be queued.", 503);

            _logger.LogInformation(
                "Password reset token generated and delivery queued. CredentialId: {CredentialId}, Transport: {Transport}",
                credential.Id, transportType);

            return Result.Success("If an account exists with that contact information, a password reset link has been sent.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("ForgotPassword", "IdentityCredential", Guid.Empty, ex.Message, ex);
            return Result.Failure("Password reset dispatch failed.", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword))
                return Result.Failure("New password is required", 400);

            if (!IdentityPasswordPolicy.IsWithinBcryptByteLimit(request.NewPassword))
                return Result.Failure("Password must not exceed 72 UTF-8 bytes", 400);

            var tokenHash = ComputeTokenHash(request.Token);
            var rateLimitDecision = await AcquireSecurityRateLimitAsync(
                StrictSecurityRateLimitPolicyMap.PasswordReset,
                request.Metadata,
                tokenHash,
                "password reset",
                ct);
            if (!rateLimitDecision.IsAllowed)
                return Result.Failure("Too many requests.", 429);

            // Look up verification by token, must be pending and not expired
            var verification = await _dataContext.Query<IdentityVerification>()
                .IgnoreQueryFilters()
                .Where(i => i.TokenHash == tokenHash)
                .Where(i => i.Purpose == IdentityConstants.VerificationPurpose.PasswordReset)
                .Where(i => i.Status == (short?)GenericStatusType.Pending)
                .Where(i => i.Expiry > DateTime.UtcNow)
                .Where(i => i.ConsumedAt == null)
                .Where(i => !i.IsDeleted && i.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (verification is null)
            {
                return Result.Failure("Invalid or expired reset token", 400);
            }

            var tenantIsActive = await _dataContext.Query<Tenant>()
                .IgnoreQueryFilters()
                .Where(t => t.Id == verification.TenantId)
                .Where(t => !t.IsDeleted && t.IsEnabled)
                .Where(t => t.AvailabilityDate == null || t.AvailabilityDate <= DateTime.UtcNow)
                .Where(t => t.Expiration == null || t.Expiration > DateTime.UtcNow)
                .AnyAsync(ct);
            if (!tenantIsActive)
                return Result.Failure("Invalid or expired reset token", 400);

            // Look up the credential
            var credential = await _dataContext.Query<IdentityCredential>()
                .Where(c => c.Id == verification.CredentialId)
                .Where(c => c.TenantId == verification.TenantId)
                .Where(c => !c.IsDeleted && c.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (credential is null)
            {
                return Result.NotFound("Associated account not found");
            }

            await using var transaction = _dbContext.Database.CurrentTransaction is null
                ? await _dbContext.Database.BeginTransactionAsync(ct)
                : null;

            // Hash new password with BCrypt (workFactor 11) - SECURITY CRITICAL
            var hashPasswordByte = Encoding.ASCII.GetBytes(
                BCrypt.Net.BCrypt.HashPassword(inputKey: request.NewPassword, workFactor: 11));
            _dataContext.Update(credential);
            _dataContext.Update(verification);
            credential.PasswordByte = hashPasswordByte;
            credential.FailedLoginAttempts = 0;
            credential.LockoutEnd = null;
            credential.ConcurrencyStamp = Guid.NewGuid();

            // Invalidate the token (mark verification as used)
            verification.Status = (short?)GenericStatusType.Approved;
            verification.StatusUpdatedOn = DateTime.UtcNow;
            verification.ConsumedAt = DateTimeOffset.UtcNow;
            verification.ConcurrencyStamp = Guid.NewGuid();

            var canceledAt = DateTimeOffset.UtcNow;
            await _dbContext.Set<IdentityVerification>()
                .IgnoreQueryFilters()
                .Where(i => i.TenantId == credential.TenantId)
                .Where(i => i.CredentialId == credential.Id)
                .Where(i => i.Purpose == IdentityConstants.VerificationPurpose.PasswordReset)
                .Where(i => i.Id != verification.Id)
                .Where(i => i.Status == (short?)GenericStatusType.Pending && i.ConsumedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(i => i.Status, (short?)GenericStatusType.Canceled)
                        .SetProperty(i => i.ConsumedAt, canceledAt)
                        .SetProperty(i => i.StatusUpdatedOn, canceledAt)
                        .SetProperty(i => i.ConcurrencyStamp, Guid.NewGuid()),
                    ct);

            await RevokeActiveSessionsAsync(credential.TenantId, credential.Id, ct);
            var saveResult = await _dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result.Failure("Password reset could not be completed", saveResult.StatusCode);

            if (transaction is not null)
                await transaction.CommitAsync(ct);

            _logger.PasswordChanged(credential.Id);

            return Result.Success("Password has been reset successfully");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("ResetPassword", "IdentityCredential", Guid.Empty, ex.Message, ex);
            return Result.Failure("An error occurred while resetting the password", 500);
        }
    }

    #endregion
}
