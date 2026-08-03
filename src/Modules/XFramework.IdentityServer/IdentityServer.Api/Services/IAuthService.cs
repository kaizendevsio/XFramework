using IdentityServer.Domain.Shared.Contracts.Responses;

namespace IdentityServer.Api.Services;

/// <summary>
/// Unified authentication service for IdentityServer operations.
/// Consolidates credential management, authentication, verification, and session management.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Creates a new identity credential with BCrypt password hashing (workFactor 11).
    /// </summary>
    /// <param name="request">The credential creation request containing username, password, and identity info</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing safe credential administration metadata or error details</returns>
    Task<Result<CredentialAdministrationResponse>> CreateCredentialAsync(
        CreateCredentialRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Authenticates an identity with username, email, or phone lookup.
    /// Generates JWT tokens, creates session, and logs authorization attempts.
    /// </summary>
    /// <param name="request">Authentication request with credentials and authentication type</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing authentication response with tokens and session info</returns>
    Task<Result<AuthenticateIdentityResponse>> AuthenticateAsync(
        AuthenticateIdentityRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Changes a user's password after an approved verification challenge.
    /// Uses BCrypt hashing with workFactor 11.
    /// </summary>
    /// <param name="request">Password change request with credential ID and new password</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an identity credential.
    /// </summary>
    /// <param name="request">Dedicated credential administration update request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing safe credential administration metadata</returns>
    Task<Result<CredentialAdministrationResponse>> UpdateCredentialAsync(
        UpdateCredentialRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Verifies a password against stored credential using BCrypt.
    /// </summary>
    /// <param name="request">Password verification request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating whether password is valid</returns>
    Task<Result<bool>> VerifyPasswordAsync(
        VerifyPasswordRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a verification code (SMS OTP) for multi-factor authentication.
    /// Sends SMS message with the generated code.
    /// </summary>
    /// <param name="request">Verification creation request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the created verification entity</returns>
    Task<Result<IdentityVerification>> CreateVerificationAsync(
        Create<IdentityVerification> request,
        CancellationToken ct = default);

    /// <summary>
    /// Updates a verification status from Pending to Approved when valid token is provided.
    /// </summary>
    /// <param name="request">Verification update request with token</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the updated verification</returns>
    Task<Result<IdentityVerification>> UpdateVerificationAsync(
        Patch<IdentityVerification> request,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a valid (non-expired) verification exists for a credential.
    /// Verifications expire after 10 minutes.
    /// </summary>
    /// <param name="request">Verification check request with credential and verification type</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing verification status and last verification if pending</returns>
    Task<Result<CheckVerificationResponse>> CheckVerificationAsync(
        CheckVerificationRequest request,
        CancellationToken ct = default);

    Task<Result<CredentialAvatarResponse>> UploadCredentialAvatarAsync(
        UploadCredentialAvatarRequest request,
        CancellationToken ct = default);

    Task<Result<CredentialAvatarResponse>> SetCredentialAvatarAsync(
        SetCredentialAvatarRequest request,
        CancellationToken ct = default);

    Task<Result<CredentialAvatarResponse>> RemoveCredentialAvatarAsync(
        RemoveCredentialAvatarRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Logs out a user by marking their session as Inactive.
    /// Creates an authorization log entry for audit trail.
    /// </summary>
    Task<Result> LogoutAsync(
        LogoutRequest request,
        CancellationToken ct = default);

    Task<Result<ValidateIdentitySessionResponse>> ValidateIdentitySessionAsync(
        ValidateIdentitySessionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Refreshes an expired access token using a valid refresh token.
    /// Validates the refresh token against stored session data,
    /// generates a new token pair, and updates the session.
    /// </summary>
    Task<Result<RefreshTokenResponse>> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Initiates a password reset flow by generating a reset token and sending it via email or SMS.
    /// Does not reveal whether the account exists (security best practice).
    /// </summary>
    /// <param name="request">Request containing email or phone to identify the account</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating the request was processed (always success for security)</returns>
    Task<Result> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Resets a user's password using a valid reset token.
    /// Validates the token, hashes the new password with BCrypt, and invalidates the token.
    /// </summary>
    /// <param name="request">Request containing the reset token and new password</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating whether the password was reset successfully</returns>
    Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken ct = default);
}
