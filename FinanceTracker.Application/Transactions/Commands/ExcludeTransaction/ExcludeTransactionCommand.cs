using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.ExcludeTransaction;

public sealed record ExcludeTransactionCommand(Guid TransactionId) : IRequest;