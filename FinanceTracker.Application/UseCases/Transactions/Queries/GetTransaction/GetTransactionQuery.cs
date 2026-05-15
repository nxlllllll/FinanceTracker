using FinanceTracker.Core.Domains.Transaction;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transactions.Queries.GetTransaction;

public sealed record GetTransactionQuery(Guid TransactionId, Guid UserId) : IRequest<Transaction?>;