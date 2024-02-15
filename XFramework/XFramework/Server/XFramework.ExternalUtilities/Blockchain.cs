using Info.Blockchain.API.Client;
using Info.Blockchain.API.Wallet;
using Info.Blockchain.API.Models;
using Newtonsoft.Json;
using XFramework.External.Models;

namespace XFramework.External;

public class Blockchain
{
    private BlockchainApiSettings GetSettings()
    {
        BlockchainApiSettings blockchainApiSettings = new BlockchainApiSettings();
        blockchainApiSettings.ApiKey = "9004fe40-5fd0-411a-ac42-4e820562c673";
        blockchainApiSettings.XpubKey =
            "xpub6Chiu7mVhgUjHGHasTxukV9scfYmm6UvPNa2MVSRjZCST2A9cTd3a6GGtE5NkCzpGHsN9wDJdFufSAGQMB3wGeVMEguhuLUHhS6HUgKgk3r";
        blockchainApiSettings.CallbackURL = "https%3A%2F%2Fwww.urlencoder.org%2F";
        blockchainApiSettings.ApiUri = new Uri("https://api.blockchain.info/");
        blockchainApiSettings.BlockCypherApiUri = new Uri("https://api.blockcypher.com/");
        blockchainApiSettings.ServiceUrl = "http://127.0.0.1:3000/";
        blockchainApiSettings.WalletID = "cda0ac06-2bf7-4df5-a76d-e891c2e225b8";
        blockchainApiSettings.WalletPassword = "-WG{%rj:[9b&vj2%";

        return blockchainApiSettings;
    }

    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

    public CreateWalletResponse CreateWallet(string passwordString, string walletLabel)
    {
        BlockchainApiSettings blockchainApiSettings = GetSettings();

        using (BlockchainApiHelper apiHelper = new BlockchainApiHelper(apiCode: blockchainApiSettings.ApiKey,
                   serviceUrl: blockchainApiSettings.ServiceUrl))
        {
            try
            {
                WalletCreator walletCreator = apiHelper.CreateWalletCreator();
                CreateWalletResponse newWallet =
                    walletCreator.CreateAsync(passwordString, label: walletLabel).Result;

                return newWallet;
            }
            catch (ClientApiException e)
            {
                throw new ArgumentException("Blockchain exception: " + e.Message);
            }
        }
    }

    public WalletAddress CreateNewAddress(string addressLabel)
    {
        BlockchainApiSettings blockchainApiSettings = GetSettings();

        using (BlockchainApiHelper apiHelper = new BlockchainApiHelper(apiCode: blockchainApiSettings.ApiKey,
                   serviceUrl: blockchainApiSettings.ServiceUrl))
        {
            try
            {
                // create an instance of an existing wallet
                Wallet wallet = apiHelper.InitializeWallet(blockchainApiSettings.WalletID,
                    blockchainApiSettings.WalletPassword);
                WalletAddress newAddr = wallet.NewAddressAsync(addressLabel).Result;

                return newAddr;
            }
            catch (ClientApiException e)
            {
                throw new ArgumentException("Blockchain exception: " + e.Message);
            }
        }
    }

    public async Task<BlockchainResponse> NewPaymentAddress(string callBackUrl)
    {
        HttpUtilities httpUtilities = new HttpUtilities();
        BlockchainApiSettings blockchainApiSettings = GetSettings();
        HttpResponseBO res = await httpUtilities.GetAsync(blockchainApiSettings.ApiUri, "v2/receive",
            new
            {
                xpub = blockchainApiSettings.XpubKey, callback = blockchainApiSettings.CallbackURL,
                key = blockchainApiSettings.ApiKey
            });
        ReceivePaymentResponse receivePayment =
            JsonConvert.DeserializeObject<ReceivePaymentResponse>(res.ResponseResult);

        BlockchainResponse blockchainResponse = new BlockchainResponse();
        blockchainResponse.Address = receivePayment.Address;
        blockchainResponse.XpubKey = blockchainApiSettings.XpubKey;

        return blockchainResponse;
        //return "123";
    }

    public WalletAddress GetAddress(string walletAddress)
    {
        BlockchainApiSettings blockchainApiSettings = GetSettings();

        using (BlockchainApiHelper apiHelper = new BlockchainApiHelper(apiCode: blockchainApiSettings.ApiKey,
                   serviceUrl: blockchainApiSettings.ServiceUrl))
        {
            try
            {
                Wallet wallet = apiHelper.InitializeWallet(blockchainApiSettings.WalletID,
                    blockchainApiSettings.WalletPassword);
                WalletAddress addr = wallet.GetAddressAsync(walletAddress).Result;

                return addr;
            }
            catch (ClientApiException e)
            {
                throw new ArgumentException("Blockchain exception: " + e.Message);
            }
        }
    }

    public List<WalletAddress> GetAddressList()
    {
        BlockchainApiSettings blockchainApiSettings = GetSettings();

        using (BlockchainApiHelper apiHelper = new BlockchainApiHelper(apiCode: blockchainApiSettings.ApiKey,
                   serviceUrl: blockchainApiSettings.ServiceUrl))
        {
            try
            {
                Wallet wallet = apiHelper.InitializeWallet(blockchainApiSettings.WalletID,
                    blockchainApiSettings.WalletPassword);
                List<WalletAddress> addresses = wallet.ListAddressesAsync().Result;

                return addresses;
            }
            catch (ClientApiException e)
            {
                throw new ArgumentException("Blockchain exception: " + e.Message);
            }
        }
    }

    public async Task<BlockchainTx> GetAddressTransactions(string walletAddress)
    {
        HttpUtilities httpUtilities = new HttpUtilities();
        BlockchainApiSettings blockchainApiSettings = GetSettings();
        HttpResponseBO _res = await httpUtilities.GetAsync(blockchainApiSettings.BlockCypherApiUri,
            "v1/btc/main/addrs/" + walletAddress, new object { });
        BlockchainTx blockchainTx = JsonConvert.DeserializeObject<BlockchainTx>(_res.ResponseResult);

        CoinCap coinCap = new CoinCap();
        CoinProperty coinProperty = coinCap.GetCoinProperty("bitcoin");

        foreach (var item in blockchainTx.Txrefs)
        {
            item.ValueFiat = (long) decimal.Parse(coinProperty.Data.PriceUsd) * item.Value;
        }

        return blockchainTx;
    }

    public PaymentResponse Send(string recipientWallet, decimal amount, decimal fee)
    {
        BlockchainApiSettings blockchainApiSettings = GetSettings();
        var httpClient = new BlockchainHttpClient(blockchainApiSettings.ApiKey, blockchainApiSettings.ServiceUrl);

        using (BlockchainApiHelper apiHelper = new BlockchainApiHelper(apiCode: blockchainApiSettings.ApiKey,
                   serviceUrl: blockchainApiSettings.ServiceUrl, serviceHttpClient: httpClient))
        {
            try
            {
                BitcoinValue _fee = BitcoinValue.FromBtc(fee);
                BitcoinValue _amount = BitcoinValue.FromBtc(amount);

                Wallet wallet = apiHelper.InitializeWallet(blockchainApiSettings.WalletID,
                    blockchainApiSettings.WalletPassword);
                PaymentResponse payment = wallet.SendAsync(recipientWallet, _amount, fee: _fee).Result;

                return payment;
            }
            catch (ClientApiException e)
            {
                throw new ArgumentException("Blockchain exception: " + e.Message);
            }
        }
    }

    public PaymentResponse SendMany(Dictionary<string, BitcoinValue> recipients)
    {
        BlockchainApiSettings blockchainApiSettings = GetSettings();

        using (BlockchainApiHelper apiHelper = new BlockchainApiHelper(apiCode: blockchainApiSettings.ApiKey,
                   serviceUrl: blockchainApiSettings.ServiceUrl))
        {
            try
            {
                Wallet wallet = apiHelper.InitializeWallet(blockchainApiSettings.WalletID,
                    blockchainApiSettings.WalletPassword);
                PaymentResponse payment = wallet.SendManyAsync(recipients).Result;

                return payment;
            }
            catch (ClientApiException e)
            {
                throw new ArgumentException("Blockchain exception: " + e.Message);
            }
        }
    }
}