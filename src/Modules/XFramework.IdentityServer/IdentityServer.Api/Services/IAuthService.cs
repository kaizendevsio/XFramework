using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;

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
    /// <returns>Result containing the created credential or error details</returns>
    Task<Result<IdentityCredential>> CreateCredentialAsync(
        Create<IdentityCredential> request,
        CancellationToken ct = default);

    /// <summary>
    /// Authenticates an identity with multi-type support (Username, Email, Phone, Token).
    /// Generates JWT tokens, creates session, and logs authorization attempts.
    /// </summary>
    /// <param name="request">Authentication request with credentials and authentication type</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing authentication response with tokens and session info</returns>
    Task<Result<AuthenticateIdentityResponse>> AuthenticateAsync(
        AuthenticateIdentityRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Changes a user's password with optional verification requirement.
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
    /// <param name="request">Patch request for credential update</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the updated credential</returns>
    Task<Result<IdentityCredential>> UpdateCredentialAsync(
        Patch<IdentityCredential> request,
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

    /// <summary>
    /// Uploads a file to Azure Blob Storage.
    /// </summary>
    /// <param name="request">File creation request with file bytes and metadata</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the created storage file entity</returns>
    Task<Result<StorageFile>> CreateFileAsync(
        Create<StorageFile> request,
        CancellationToken ct = default);
}