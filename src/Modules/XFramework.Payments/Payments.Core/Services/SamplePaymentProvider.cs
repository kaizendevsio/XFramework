using Payments.Domain.Shared.Contracts;
using Payments.Domain.Shared.Contracts.Requests.Create;

namespace Payments.Core.Services;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

public class SamplePaymentProvider : IPaymentGatewayProvider
{
    public string Name => "Sample Payment Provider";
    public PaymentGateway? Provider { get; set; }
    public bool IsAvailable => true;
    public bool SupportsCashInCallback => true;
    
    // Store pending transactions that are awaiting callbacks
    private readonly ConcurrentDictionary<string, CreateCashInRequest> _pendingCashIns = new();
    
    // Store merchant balances
    private readonly ConcurrentDictionary<string, decimal> _merchantBalances = new();
    
    // Secret key for verifying callback authenticity (in a real scenario, this would be securely stored)
    private readonly string _callbackSecretKey = "sample-provider-secret-key-12345";
    
    public SamplePaymentProvider()
    {
        // Initialize with some sample balances
        _merchantBalances["merchant-123"] = 10000;
        _merchantBalances["merchant-456"] = 5000;
    }

    private async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // In a real implementation, you would initialize the provider here
        // For example, you could use the credentials to authenticate with the provider
        // and fetch the merchant balances
        //
        // For this sample, we'll just simulate the initialization by delaying the task
        await Task.Delay(1000, cancellationToken);
        
        
    }
    
    public async Task<PaymentResponse> ProcessCashInAsync(CreateCashInRequest request, CancellationToken cancellationToken = default)
    {
        // Validate request
        if (request.Amount <= 0)
        {
            return new PaymentResponse 
            { 
                Success = false, 
                Message = "Amount must be greater than zero",
                Amount = request.Amount
            };
        }
        
        // Simulate API call delay
        await Task.Delay(500, cancellationToken);
        
        // Generate reference ID if not provided
        var referenceId = !string.IsNullOrEmpty(request.ReferenceNumber) 
            ? request.ReferenceNumber 
            : $"SMP-CI-{Guid.NewGuid():N}";
            
        // For callback-based flow, we store the request and return a pending response
        // In a real implementation, you would initiate the payment flow with the provider here
        // and they would later call your callback URL
        _pendingCashIns[referenceId] = request;
        
        // Return pending response with payment instructions
        return new PaymentResponse
        {
            Success = true,
            ReferenceId = referenceId,
            Message = "Cash in initiated. Waiting for payment confirmation via callback.",
            Amount = request.Amount,
            Fee = CalculateFee(request.Amount, true),
            ProviderResponseCode = "01", // Pending code
            ProviderResponseMessage = "Pending",
            ProviderResponse = JsonSerializer.Serialize(
                new
                {
                    TransactionId = referenceId,
                    Status = "PENDING",
                    PaymentInstructions = "Please complete the payment using the provider's payment portal."
                }
            )
        };
    }
    
    public async Task<PaymentResponse> ProcessCashInCallbackAsync(PaymentCallbackPayload payload, CancellationToken cancellationToken = default)
    {
        // Validate the callback payload
        if (string.IsNullOrEmpty(payload.ReferenceNumber) || payload.Amount <= 0)
        {
            return new PaymentResponse 
            { 
                Success = false, 
                Message = "Invalid callback payload"
            };
        }
        
        // Check if this is a pending transaction
        if (!_pendingCashIns.TryGetValue(payload.ReferenceNumber, out var originalRequest))
        {
            return new PaymentResponse 
            { 
                Success = false, 
                Message = $"Unknown transaction reference: {payload.ReferenceNumber}"
            };
        }
        
        // Verify amount matches
        if (payload.Amount != originalRequest.Amount)
        {
            return new PaymentResponse 
            { 
                Success = false, 
                Message = $"Amount mismatch. Expected: {originalRequest.Amount}, Received: {payload.Amount}",
                Amount = payload.Amount.Value
            };
        }
        
        // Process the successful payment
        var merchantId = originalRequest.MerchantId;
        if (!string.IsNullOrEmpty(merchantId))
        {
            _merchantBalances.AddOrUpdate(
                merchantId,
                originalRequest.Amount,
                (_, currentBalance) => currentBalance + originalRequest.Amount);
        }
        
        // Remove from pending transactions
        _pendingCashIns.TryRemove(payload.ReferenceNumber, out _);
        
        // Return successful response
        return new PaymentResponse
        {
            Success = true,
            ReferenceId = payload.ReferenceNumber,
            Message = "Cash in successful via callback",
            Amount = payload.Amount.Value,
            Fee = CalculateFee(payload.Amount.Value, true),
            TransactionDate = payload.TransactionDate ?? DateTimeOffset.UtcNow,
            ProviderResponseCode = "00",
            ProviderResponseMessage = "Approved",
            ProviderResponse = JsonSerializer.Serialize(
                new
                {
                    TransactionId = payload.ProviderTransactionId,
                    Status = "COMPLETED",
                    PayerName = payload.PayerName,
                    PayerAccount = payload.PayerAccount
                }
            )
        };
    }
    
    public async Task<bool> VerifyCallbackAuthenticityAsync(PaymentCallbackPayload payload, CancellationToken cancellationToken = default)
    {
        // Basic verification example - in real scenarios, this would involve
        // checking signatures, hashes, or other security measures provided by the payment gateway
        
        if (string.IsNullOrEmpty(payload.SignatureOrHash))
        {
            return false;
        }
        
        // Calculate expected hash using the callback secret key
        // For example: hash of reference number + amount + transaction date + secret key
        var dataToHash = $"{payload.ReferenceNumber}|{payload.Amount}|{payload.TransactionDate}|{_callbackSecretKey}";
        var expectedHash = ComputeSha256Hash(dataToHash);
        
        // Compare with the provided signature/hash
        return string.Equals(expectedHash, payload.SignatureOrHash, StringComparison.OrdinalIgnoreCase);
    }
    
    public string GenerateCashInCallbackUrl(string baseUrl, string merchantId, string referenceNumber)
    {
        // Create a URL that the payment provider will call when the payment is complete
        return $"{baseUrl.TrimEnd('/')}/api/payments/callback/sample-provider?ref={referenceNumber}&merchant={merchantId}";
    }
    
    public async Task<PaymentResponse> ProcessCashOutAsync(CreateCashoutRequest request, CancellationToken cancellationToken = default)
    {
        // Validate request
        if (request.Amount <= 0)
        {
            return new PaymentResponse 
            { 
                Success = false, 
                Message = "Amount must be greater than zero",
                Amount = request.Amount
            };
        }
        
        // Check merchant balance
        string merchantId = request.MerchantId;
        if (!string.IsNullOrEmpty(merchantId) && 
            _merchantBalances.TryGetValue(merchantId, out var balance) && 
            balance < request.Amount)
        {
            return new PaymentResponse 
            { 
                Success = false, 
                Message = $"Insufficient merchant balance. Available: {balance}, Requested: {request.Amount}",
                Amount = request.Amount
            };
        }
        
        // Simulate API call delay
        await Task.Delay(500, cancellationToken);
        
        // Generate reference ID
        var referenceId = !string.IsNullOrEmpty(request.ReferenceNumber) 
            ? request.ReferenceNumber 
            : $"SMP-CO-{Guid.NewGuid():N}";
        
        // Update merchant balance if provided
        if (!string.IsNullOrEmpty(merchantId))
        {
            _merchantBalances.AddOrUpdate(
                merchantId,
                0, // This shouldn't happen but prevents exceptions
                (_, currentBalance) => currentBalance - request.Amount);
        }
        
        // Return successful response
        return new PaymentResponse
        {
            Success = true,
            ReferenceId = referenceId,
            Message = "Cash out successful",
            Amount = request.Amount,
            Fee = CalculateFee(request.Amount, false),
            ProviderResponseCode = "00",
            ProviderResponseMessage = "Approved",
            ProviderResponse = JsonSerializer.Serialize(
                new { TransactionId = referenceId, Status = "COMPLETED" }
            )
        };
    }
    
    public async Task<decimal> GetBalanceAsync(PaymentGateway paymentGateway, CancellationToken cancellationToken = default)
    {
        // In a real implementation, you would fetch the balance from the provider
        // Here we'll just return some mock data
        
        // Simulate API call delay
        await Task.Delay(300, cancellationToken);
        
        // Return total balance across all merchants
        return _merchantBalances.Values.Sum();
    }
    
    public async Task<bool> ValidateCredentialsAsync(MerchantCredentials credentials, CancellationToken cancellationToken = default)
    {
        // Simulate API call delay
        await Task.Delay(200, cancellationToken);
        
        // Simple validation logic (for demo purposes only)
        var isValid = !string.IsNullOrEmpty(credentials.MerchantId) && 
                      !string.IsNullOrEmpty(credentials.ApiKey) &&
                      !string.IsNullOrEmpty(credentials.ApiSecret);
                      
        return isValid;
    }
    
    private decimal CalculateFee(decimal amount, bool isCashIn)
    {
        // Sample fee calculation logic
        if (isCashIn)
        {
            return amount * 0.01m; // 1% for cash in
        }
        else
        {
            return amount * 0.015m; // 1.5% for cash out
        }
    }
    
    private string ComputeSha256Hash(string data)
    {
        using (var sha256 = SHA256.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            var hash = sha256.ComputeHash(bytes);
            
            var builder = new StringBuilder();
            for (var i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2"));
            }
            
            return builder.ToString();
        }
    }
}
