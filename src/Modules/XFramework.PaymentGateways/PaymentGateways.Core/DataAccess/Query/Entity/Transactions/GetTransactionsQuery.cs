using System;
using System.Collections.Generic;
using MediatR;
using PaymentGateways.Domain.Generic.Contracts.Responses;

namespace PaymentGateways.Core.DataAccess.Query.Entity.Transactions;

public class GetTransactionsQuery : QueryBaseEntity, IRequest<QueryResponse<List<UserDepositContract>>>
{
    public Guid? Cuid { get; set; }
    public string PhoneNumber { get; set; }
}