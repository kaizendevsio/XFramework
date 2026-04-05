using System.Text;
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
        throw new NotImplementedException();
    }
    
    public async Task<BitcoinfeeBO> GetBitcoinFee()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_btcBlockchainConfiguration.FeeUrl}fees/recommended");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<BitcoinfeeBO>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            return result ?? new BitcoinfeeBO { FastestFee = 50, HalfHourFee = 50, HourFee = 50 };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Bitcoin fee, using defaults");
            return new BitcoinfeeBO { FastestFee = 50, HalfHourFee = 50, HourFee = 50 };
        }
    }
    
    public async Task<HttpResponseMessage> SendToMany(List<BtcTransactionBO> transactionList)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        foreach (var transaction in transactionList)
        {
            sb.Append('"')
                .Append($"{transaction.BtcAddress}")
                .Append('"')
                .Append($":{transaction.BtcAmount * Satoshi},");
        }
        sb.Append('}');
        
        var fee = await GetBitcoinFee();
        
        var request = new
        {
            password = _btcBlockchainConfiguration.WalletConfiguration?.WalletPassword,
            api_code = _btcBlockchainConfiguration.ApiCode,
            from = 0,
            fee_per_byte = fee.HalfHourFee,
            recipients = Uri.EscapeDataString(sb.ToString().Replace(",}", "}")),
        };

        var queryParams = JsonSerializer.Serialize(request).JsonToQuery();
        var url = $"{_btcBlockchainConfiguration.ServiceUrl}merchant/{_btcBlockchainConfiguration.WalletConfiguration?.WalletIdentifier}/sendmany{queryParams}";
        
        _logger.LogInformation("Sending bulk transaction to {Count} recipients", transactionList.Count);
        
        var response = await _httpClient.GetAsync(url);
        
        return response;
    }
    
    public async Task<bool> EnableHd()
    {
        await Task.CompletedTask;
        throw new NotImplementedException();
    }
}

/// <summary>
/// Extension methods for JSON operations
/// </summary>
internal static class JsonExtensions
{
    public static string JsonToQuery(this string json)
    {
        if (string.IsNullOrEmpty(json) || json == "{}")
            return string.Empty;
            
        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        if (dict == null || dict.Count == 0)
            return string.Empty;
            
        var sb = new StringBuilder("?");
        foreach (var kvp in dict)
        {
            if (kvp.Value != null)
            {
                sb.Append($"{kvp.Key}={kvp.Value}&");
            }
        }
        
        return sb.ToString().TrimEnd('&');
    }
}