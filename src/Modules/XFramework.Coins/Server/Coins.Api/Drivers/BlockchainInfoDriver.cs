using System.Globalization;
using System.Text.Json;
using Coins.Api.BusinessObjects;
using Coins.Api.Configurations;
using Coins.Api.Interfaces.Wrappers;

namespace Coins.Api.Drivers;

/// <summary>
/// Driver for Blockchain.info API operations
/// </summary>
public class BlockchainInfoDriver : IBtcBlockchainWrapper
{
    public decimal Satoshi { get; set; } = 100000000;

    private readonly HttpClient _httpClient;
    private readonly BtcBlockchainConfiguration _btcBlockchainConfiguration;
    private readonly ILogger<BlockchainInfoDriver> _logger;

    public BlockchainInfoDriver(
        IConfiguration configuration,
        HttpClient httpClient,
        ILogger<BlockchainInfoDriver> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _btcBlockchainConfiguration = new BtcBlockchainConfiguration();
        configuration.Bind(nameof(BtcBlockchainConfiguration), _btcBlockchainConfiguration);
    }

    public BlockchainInfoDriver(
        BtcBlockchainConfiguration btcBlockchainConfiguration,
        HttpClient httpClient,
        ILogger<BlockchainInfoDriver> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _btcBlockchainConfiguration = btcBlockchainConfiguration;
    }

    public int GetGapLimit()
    {
        return Math.Max(0, _btcBlockchainConfiguration.MaxGapLimit);
    }

    public async Task<BitcoinfeeBO> GetBitcoinFee()
    {
        try
        {
            var feeUrl = _btcBlockchainConfiguration.FeeUrl?.ToString();
            if (string.IsNullOrWhiteSpace(feeUrl))
            {
                _logger.LogWarning("Bitcoin fee URL is not configured, using defaults");
                return CreateDefaultFee();
            }

            var response = await _httpClient.GetAsync($"{feeUrl.TrimEnd('/')}/fees/recommended");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<BitcoinfeeBO>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? CreateDefaultFee();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Bitcoin fee, using defaults");
            return CreateDefaultFee();
        }
    }

    public async Task<HttpResponseMessage> SendToMany(List<BtcTransactionBO> transactionList)
    {
        if (transactionList is null || transactionList.Count == 0)
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                ReasonPhrase = "Transaction list cannot be empty"
            };
        }

        var serviceUrl = _btcBlockchainConfiguration.ServiceUrl?.ToString();
        var walletIdentifier = _btcBlockchainConfiguration.WalletConfiguration?.WalletIdentifier;
        if (string.IsNullOrWhiteSpace(serviceUrl) || string.IsNullOrWhiteSpace(walletIdentifier))
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                ReasonPhrase = "Bitcoin service URL and wallet identifier must be configured"
            };
        }

        var recipients = transactionList
            .GroupBy(static transaction => transaction.BtcAddress)
            .ToDictionary(
                static group => group.Key,
                group => group.Sum(transaction => transaction.BtcAmount * Satoshi));

        var fee = await GetBitcoinFee();
        var queryParams = new Dictionary<string, string?>
        {
            ["password"] = _btcBlockchainConfiguration.WalletConfiguration?.WalletPassword,
            ["api_code"] = _btcBlockchainConfiguration.ApiCode,
            ["from"] = "0",
            ["fee_per_byte"] = fee.HalfHourFee.ToString(CultureInfo.InvariantCulture),
            ["recipients"] = JsonSerializer.Serialize(recipients)
        }.ToQueryString();

        var url =
            $"{serviceUrl.TrimEnd('/')}/merchant/{Uri.EscapeDataString(walletIdentifier)}/sendmany{queryParams}";

        _logger.LogInformation("Sending bulk transaction to {Count} recipients", transactionList.Count);

        return await _httpClient.GetAsync(url);
    }

    public Task<bool> EnableHd()
    {
        _logger.LogWarning("HD wallet enablement is not supported by the Blockchain.info driver");
        return Task.FromResult(false);
    }

    private static BitcoinfeeBO CreateDefaultFee() =>
        new() { FastestFee = 50, HalfHourFee = 50, HourFee = 50 };
}

internal static class QueryStringExtensions
{
    public static string ToQueryString(this IReadOnlyDictionary<string, string?> values)
    {
        var encodedValues = values
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(static pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}")
            .ToArray();

        return encodedValues.Length == 0
            ? string.Empty
            : $"?{string.Join("&", encodedValues)}";
    }
}
