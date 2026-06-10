using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Notifications;

public sealed record BudgetCreatedNotification(
	Guid BudgetId,
	Guid UserId,
	Guid CategoryId,
	Money Amount,
	DateOnly From,
	DateOnly To,
	DateTimeOffset OccurredAt
) : INotification;