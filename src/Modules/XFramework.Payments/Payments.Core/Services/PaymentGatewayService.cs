namespace Payments.Core.Services;

using Domain.Shared.Contracts;
using Domain.Shared.Contracts.Requests.Create;

public class PaymentGatewayService
{
    private readonly IReadOnlyList<IPaymentGatewayProvider> _providers;
    private readonly ILogger<PaymentGatewayService> _logger;
    private readonly string _baseCallbackUrl;

    public PaymentGatewayService(
        IEnumerable<IPaymentGatewayProvider> providers,
        ILogger<PaymentGatewayService> logger,
        string baseCallbackUrl = "https://api.yourdomain.com")
    {
        _providers = providers.ToList();
        _logger = logger;
        _baseCallbackUrl = baseCallbackUrl;
    }

    public async Task<PaymentResponse> ProcessCashInAsync(
        CreateCashInRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(request.PaymentGateway);
        if (provider is null)
        {
            return ProviderNotFound(request.PaymentGateway, request.Amount);
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
            request.ReferenceNumber = string.IsNullOrEmpty(request.ReferenceNumber)
                ? $"REF-{Guid.NewGuid():N}"
                : request.ReferenceNumber;

            var response = await provider.ProcessCashInAsync(request, cancellationToken);

            if (provider.SupportsCashInCallback)
            {
                var referenceId = response.ReferenceId ?? request.ReferenceNumber;
                var callbackUrl = provider.GenerateCashInCallbackUrl(
                    _baseCallbackUrl,
                    request.MerchantId ?? string.Empty,
                    referenceId);

                var originalResponse = response.ProviderResponse;
                response.ProviderResponse = JsonSerializer.Serialize(new
                {
                    OriginalResponse = originalResponse,
                    CallbackUrl = callbackUrl
                });

                _logger.LogInformation(
                    "Generated callback URL for provider {Provider}, reference {Reference}",
                    provider.Name,
                    referenceId);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing cash in with provider {Provider}", provider.Name);
            return new PaymentResponse
            {
                Success = false,
                Message = "Error processing payment",
                Amount = request.Amount
            };
        }
    }

    public async Task<PaymentResponse> ProcessCashInCallbackAsync(
        PaymentGateway paymentGateway,
        PaymentCallbackPayload payload,
        CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(paymentGateway);
        if (provider is null)
        {
            return ProviderNotFound(paymentGateway);
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
            _logger.LogInformation(
                "Received callback for provider {Provider}, reference {Reference}",
                provider.Name,
                payload.ReferenceNumber);

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

            var response = await provider.ProcessCashInCallbackAsync(payload, cancellationToken);

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
                Message = "Error processing callback"
            };
        }
    }

    public async Task<PaymentResponse> ProcessCashOutAsync(
        CreateCashoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(request.PaymentGateway);
        if (provider is null)
        {
            return ProviderNotFound(request.PaymentGateway, request.Amount);
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
            var balance = await provider.GetBalanceAsync(request.PaymentGateway!, cancellationToken);
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

            return await provider.ProcessCashOutAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing cash out with provider {Provider}", provider.Name);
            return new PaymentResponse
            {
                Success = false,
                Message = "Error processing cash out",
                Amount = request.Amount
            };
        }
    }

    public async Task<decimal> GetBalanceAsync(PaymentGateway paymentGateway, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(paymentGateway);
        if (provider is null)
        {
            _logger.LogWarning("Payment gateway provider {Provider} not found", GetGatewayLabel(paymentGateway));
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
        return _providers.Where(p => p.IsAvailable).ToList();
    }

    public IPaymentGatewayProvider? GetProvider(PaymentGateway paymentGateway)
    {
        var provider = FindProvider(paymentGateway);
        if (provider is null)
        {
            _logger.LogWarning("Payment gateway provider {Provider} not found", GetGatewayLabel(paymentGateway));
        }

        return provider;
    }

    private IPaymentGatewayProvider? FindProvider(PaymentGateway? paymentGateway)
    {
        if (paymentGateway is null)
        {
            return null;
        }

        if (paymentGateway.Id != Guid.Empty)
        {
            var providerById = _providers.FirstOrDefault(p => p.Provider.Id == paymentGateway.Id);
            if (providerById is not null)
            {
                return providerById;
            }
        }

        if (!string.IsNullOrWhiteSpace(paymentGateway.Name))
        {
            return _providers.FirstOrDefault(p =>
                string.Equals(p.Provider.Name, paymentGateway.Name, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private PaymentResponse ProviderNotFound(PaymentGateway? paymentGateway, decimal amount = 0)
    {
        var label = GetGatewayLabel(paymentGateway);
        _logger.LogWarning("Payment gateway provider {Provider} not found", label);

        return new PaymentResponse
        {
            Success = false,
            Message = $"Payment gateway provider {label} not found",
            Amount = amount
        };
    }

    private static string GetGatewayLabel(PaymentGateway? paymentGateway)
    {
        if (paymentGateway is null)
        {
            return "not specified";
        }

        if (!string.IsNullOrWhiteSpace(paymentGateway.Name))
        {
            return paymentGateway.Name;
        }

        return paymentGateway.Id == Guid.Empty
            ? "not specified"
            : paymentGateway.Id.ToString();
    }
}
