using System;
using MediatR;
using PaymentGateways.Domain.Generic.Contracts.Responses;

namespace PaymentGateways.Core.DataAccess.Commands.Entity.Transactions
{
    public class NewTransactionCmd : CommandBaseEntity, IRequest<CmdResponseBO<NewTransactionCmd>>
    {
        public Guid? Cuid { get; set; }
        public string Remarks { get; set; }
        public decimal? Amount { get; set; }
        public Guid? TransactionGuid { get; set; }
        public long? TargetWalletTypeId { get; set; }
        public GatewayContract Gateway { get; set; }
    }
}