using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Notifications;

public sealed record BudgetAmountChangedNotification(
	Guid BudgetId,
	Guid UserId,
	decimal NewAmount,
	DateTimeOffset OccurredAt
) : INotification;