using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.Transactions.Queries.GetTransaction;

public sealed record GetTransactionQuery(Guid TransactionId) : IRequest<TransactionDto?>;