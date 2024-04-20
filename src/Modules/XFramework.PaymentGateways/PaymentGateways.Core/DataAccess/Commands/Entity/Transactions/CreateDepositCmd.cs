using System;
using MediatR;
using PaymentGateways.Domain.Generic.Contracts.Requests.Create;
using PaymentGateways.Domain.Generic.Contracts.Responses;

namespace PaymentGateways.Core.DataAccess.Commands.Entity.Transactions;

public class CreateDepositCmd : CreateDepositRequest, IRequest<CmdResponse<DepositResponse>>
{
      
}