using FinanceTracker.Core.Domains.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Queries.GetTransaction;

public sealed record GetTransactionQuery(Guid TransactionId) : IRequest<Transaction?>;