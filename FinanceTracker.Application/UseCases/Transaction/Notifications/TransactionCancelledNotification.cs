using FinanceTracker.Core.Domains.Account;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Notifications;

public sealed record TransactionCancelledNotification(
	Guid TransactionId,
	Guid UserId,
	Guid AccountId,
	Guid ReversalId,
	decimal Amount,
	DirectionType Direction,
	bool WasExcluded,
	DateTimeOffset OccurredAt
) : INotification;
