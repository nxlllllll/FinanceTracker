using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.IncludeTransaction;

public sealed record IncludeTransactionCommand(Guid TransactionId) : IRequest;