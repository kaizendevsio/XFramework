using System.Collections.Generic;
using System.Threading.Tasks;
using Coins.Domain.BusinessObjects;
using Coins.Domain.Configurations;
using Info.Blockchain.API.Models;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Generic.Interfaces;

namespace Coins.Core.Interfaces.Wrappers
{
    public interface IBtcBlockchainWrapper : IXFrameworkService
    {
        public decimal Satoshi { get; }
        public BtcBlockchainConfiguration Configuration { get; set; }
        
        public int GetGapLimit();
        public PaymentResponse PayGapLimit(string toAddress);
        public BitcoinfeeBO GetBitcoinFee();

        public PaymentResponse Send(string toAddress, decimal amountBtc, decimal? feeBtc = null);
        public Task<bool> SendToMany(List<BtcTransactionBO> transactionList);
        public Task<bool> EnableHd();
    }
}