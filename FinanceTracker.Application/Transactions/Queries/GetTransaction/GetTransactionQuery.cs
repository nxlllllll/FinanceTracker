using FinanceTracker.Core.Domains.Transactions;
using MediatR;

namespace FinanceTracker.Application.Transactions.Queries.GetTransaction;

public sealed record GetTransactionQuery(Guid TransactionId) : IRequest<Transaction?>;