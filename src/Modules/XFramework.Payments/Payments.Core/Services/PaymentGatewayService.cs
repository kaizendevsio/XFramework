namespace Payments.Core.Services;

using Domain.Shared.Contracts.Requests.Create;
using Domain.Shared.Contracts;

public class PaymentGatewayService
{
    private readonly Dictionary<PaymentGateway, IPaymentGatewayProvider> _providers;
    private readonly ILogger<PaymentGatewayService> _logger;
    private readonly string _baseCallbackUrl;

    public PaymentGatewayService(
        IEnumerable<IPaymentGatewayProvider> providers,
        ILogger<PaymentGatewayService> logger,
        string baseCallbackUrl = "https://api.yourdomain.com")
    {
        _providers = providers.ToDictionary(p => p.Provider, p => p);
        _logger = logger;
        _baseCallbackUrl = baseCallbackUrl;
    }

    public async Task<PaymentResponse> ProcessCashInAsync(CreateCashInRequest request, CancellationToken cancellationToken = default)
    {
        if (!_providers.TryGetValue(request.PaymentGateway, out var provider))
        {
            _logger.LogError("Payment gateway provider {Provider} not found", request.PaymentGateway);
            return new PaymentResponse 
            { 
                Success = false, 
                Message = $"Payment gateway provider {request.PaymentGateway} not found",
                Amount = request.Amount
            };
        }

        if (!provider.IsAvailable)
        {   
            _logger.LogWarning("Payment gateway provider {Provider} is not available", provider.Name);
            return new PaymentResponse 
            { 
                Success = false, 
                Message = $"Payment gateway provider {provider.Name} is not available",
                Amount = request.Amount
            };
        }

        try
        {
            // Make sure we have a reference number
            request.ReferenceNumber = string.IsNullOrEmpty(request.ReferenceNumber) 
                ? $"REF-{Guid.NewGuid():N}" 
                : request.ReferenceNumber;
            
            // Process the cash in request
            var response = await provider.ProcessCashInAsync(request, cancellationToken);
            
            // If the provider supports callbacks, include callback information in the response
            if (provider.SupportsCashInCallback)
            {
                // Use the reference ID from the response or fall back to the request reference number
                var referenceId = response.ReferenceId ?? request.ReferenceNumber;
                
                // Generate the callback URL that the payment provider will call when payment is complete
                var callbackUrl = provider.GenerateCashInCallbackUrl(
                    _baseCallbackUrl, 
                    request.MerchantId, 
                    referenceId);
                
                // Add the callback URL to the response
                var originalResponse = response.ProviderResponse;
                response.ProviderResponse = JsonSerializer.Serialize(new { 
                    OriginalResponse = originalResponse,
                    CallbackUrl = callbackUrl
                });
                
                _logger.LogInformation(
                    "Generated callback URL for provider {Provider}, reference {Reference}: {CallbackUrl}",
                    provider.Name,
                    referenceId,
                    callbackUrl);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing cash in with provider {Provider}", provider.Name);
            return new PaymentResponse 
            { 
                Success = false, 
                Message = $"Error processing payment: {ex.Message}",
                Amount = request.Amount
            };
        }
    }

    public async Task<PaymentResponse> ProcessCashInCallbackAsync(
        PaymentGateway paymentGateway,
        PaymentCallbackPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!_providers.TryGetValue(paymentGateway, out var provider))
        {
            _logger.LogError("Payment gateway provider {Provider} not found for callback processing", paymentGateway);
            return new PaymentResponse 
            { 
                Success = false, 
                Message = $"Payment gateway provider {paymentGateway} not found"
            };
        }

        if (!provider.SupportsCashInCallback)
        {
            _logger.LogError("Provider {Provider} does not support cash in callbacks", provider.Name);
            return new PaymentResponse 
            { 
                Success = false, 
                Message = $"Provider {provider.Name} does not support cash in callbacks"
            };
        }

        try
        {
            // Log the callback receipt
            _logger.LogInformation(
                "Received callback for provider {Provider}, reference {Reference}",
                provider.Name,
                payload.ReferenceNumber);
                
            // Verify the callback is authentic
            var isAuthentic = await provider.VerifyCallbackAuthenticityAsync(payload, cancellationToken);
            if (!isAuthentic)
            {
                _logger.LogWarning("Received unauthentic callback for provider {Provider}", provider.Name);
                return new PaymentResponse 
                { 
                    Success = false, 
                    Message = "Callback verification failed"
                };
            }

            // Process the callback
            var response = await provider.ProcessCashInCallbackAsync(payload, cancellationToken);
            
            // Log the result
            if (response.Success)
            {
                _logger.LogInformation(
                    "Successfully processed callback for provider {Provider}, reference {Reference}",
                    provider.Name,
                    payload.ReferenceNumber);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to process callback for provider {Provider}, reference {Reference}: {Message}",
                    provider.Name,
                    payload.ReferenceNumber,
                    response.Message);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing cash in callback with provider {Provider}", provider.Name);
            return new PaymentResponse 
            { 
                Success = false, 
                Message = $"Error processing callback: {ex.Message}"
            };
        }
    }

    public async Task<PaymentResponse> ProcessCashOutAsync(CreateCashoutRequest request, CancellationToken cancellationToken = default)
    {
        if (!_providers.TryGetValue(request.PaymentGateway, out var provider))
        {
            _logger.LogError("Payment gateway provider {Provider} not found", request.PaymentGateway);
            return new PaymentResponse 
            { 
                Success = false, 
                Message = $"Payment gateway provider {request.PaymentGateway} not found",
                Amount = request.Amount
            };
        }

        if (!provider.IsAvailable)
        {
            _logger.LogWarning("Payment gateway provider {Provider} is not available", provider.Name);
            return new PaymentResponse 
            { 
                Success = false, 
                Message = $"Payment gateway provider {provider.Name} is not available",
                Amount = request.Amount
            };
        }

        try
        {
            // Check the balance before processing cash out
            var balance = await provider.GetBalanceAsync(request.PaymentGateway, cancellationToken);
            if (balance < request.Amount)
            {
                _logger.LogWarning(
                    "Insufficient balance for cash out with provider {Provider}. Available: {Balance}, Requested: {Amount}", 
                    provider.Name,
                    balance,
                    request.Amount);
                    
                return new PaymentResponse 
                { 
                    Success = false, 
                    Message = $"Insufficient balance. Available: {balance}, Requested: {request.Amount}",
                    Amount = request.Amount
                };
            }

            // Process the cash out
            return await provider.ProcessCashOutAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing cash out with provider {Provider}", provider.Name);
            return new PaymentResponse 
            { 
                Success = false, 
                Message = $"Error processing cash out: {ex.Message}",
                Amount = request.Amount
            };
        }
    }

    public async Task<decimal> GetBalanceAsync(PaymentGateway paymentGateway, CancellationToken cancellationToken = default)
    {
        if (!_providers.TryGetValue(paymentGateway, out var provider))
        {
            _logger.LogError("Payment gateway provider {Provider} not found", paymentGateway);
            return 0;
        }

        try
        {
            return await provider.GetBalanceAsync(paymentGateway, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving balance from provider {Provider}", provider.Name);
            return 0;
        }
    }

    public IReadOnlyList<IPaymentGatewayProvider> GetAvailableProviders()
    {
        return _providers.Values.Where(p => p.IsAvailable).ToList();
    }
    
    public IPaymentGatewayProvider GetProvider(PaymentGateway paymentGateway)
    {
        if (_providers.TryGetValue(paymentGateway, out var provider))
        {
            return provider;
        }
        
        throw new KeyNotFoundException($"Payment gateway provider {paymentGateway} not found");
    }
}