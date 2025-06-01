namespace Payments.Domain.Shared.Abstractions;

using Payments.Domain.Shared.Contracts.Requests.Create;
using System.Threading;
using System.Threading.Tasks;

public interface IPaymentGatewayProvider
{
    string Name { get; }
    PaymentGateway Provider { get; }
    bool IsAvailable { get; }
    bool SupportsCashInCallback { get; }
    
    /// <summary>
    /// Process a cash in request. For providers using callbacks, this may return a pending response
    /// with information to initiate the payment process.
    /// </summary>
    Task<Contracts.PaymentResponse> ProcessCashInAsync(CreateCashInRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process a cash out request.
    /// </summary>
    Task<Contracts.PaymentResponse> ProcessCashOutAsync(CreateCashoutRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the balance for the specified payment gateway.
    /// </summary>
    Task<decimal> GetBalanceAsync(PaymentGateway paymentGateway, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate merchant credentials.
    /// </summary>
    Task<bool> ValidateCredentialsAsync(Contracts.MerchantCredentials credentials, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate a cash in callback URL for the provider.
    /// </summary>
    string GenerateCashInCallbackUrl(string baseUrl, string merchantId, string referenceNumber);
    
    /// <summary>
    /// Process a cash in callback/webhook notification from the payment provider.
    /// </summary>
    Task<Contracts.PaymentResponse> ProcessCashInCallbackAsync(Contracts.PaymentCallbackPayload payload, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verify if a callback/webhook is authentic and came from the payment provider.
    /// </summary>
    Task<bool> VerifyCallbackAuthenticityAsync(Contracts.PaymentCallbackPayload payload, CancellationToken cancellationToken = default);
}