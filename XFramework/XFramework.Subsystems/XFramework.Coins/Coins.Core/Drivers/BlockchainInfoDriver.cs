using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Coins.Core.Interfaces.Wrappers;
using Coins.Domain.BusinessObjects;
using Coins.Domain.Configurations;
using Info.Blockchain.API.Models;
using Info.Blockchain.API.Wallet;
using Microsoft.Extensions.Configuration;

namespace Coins.Core.Drivers
{
    public class BlockchainInfoDriver : IBtcBlockchainWrapper
    {
        public decimal Satoshi { get; set; } = 100000000;
        public Wallet Wallet { get; set; }
        public BtcBlockchainConfiguration Configuration { get; set; } = new();
        public BlockchainInfoDriver(IConfiguration configuration)
        {
            configuration.Bind(nameof(Configuration),Configuration);
        }
        
        
        public int GetGapLimit()
        {
            throw new System.NotImplementedException();
        }
        public PaymentResponse PayGapLimit(string toAddress)
        {
            throw new System.NotImplementedException();
        }
        public BitcoinfeeBO GetBitcoinFee()
        {
            throw new System.NotImplementedException();
        }
        public PaymentResponse Send(string toAddress, decimal amountBtc, decimal? feeBtc = null)
        {
            throw new System.NotImplementedException();
        }
        public async Task<bool> SendToMany(List<BtcTransactionBO> transactionList)
        {
            var sb = new StringBuilder();
            foreach (var transaction in transactionList)
            {
                sb.Append('{');
                sb.AppendFormat("\"{0}\":{1},", transaction.BtcAddress, transaction.BtcAmount * Satoshi);
                sb.Append('}');
            }
            
            var request = new
            {
                recipients = sb.ToString().Replace(",}", "}"),
                password = Configuration.WalletConfiguration.WalletPassword,
                api_code = Configuration.WalletConfiguration.WalletIdentifier,
                from = 0,
                fee_per_byte = GetBitcoinFee().HalfHourFee,
            };
            
            var sendManyResponse = Post<SendManyResponse>(AppSettings.BlockchainServiceUrl, string.Format("merchant/{0}/sendmany", AppSettings.BlockchainSendWalletIdentifier), request);
            
        }
        public async Task<bool> EnableHd()
        {
            throw new System.NotImplementedException();
        }
    }
}