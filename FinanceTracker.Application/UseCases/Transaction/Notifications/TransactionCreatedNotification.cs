using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Notifications;

public sealed record TransactionCreatedNotification(
	Guid TransactionId,
	Guid AccountId,
	Guid UserId,
	Guid CategoryId,
	Money Amount,
	DirectionType Direction,
	decimal ExchangeRate,
	bool IsRatePending,
	string? Description,
	DateTimeOffset OccurredAt
) : INotification;