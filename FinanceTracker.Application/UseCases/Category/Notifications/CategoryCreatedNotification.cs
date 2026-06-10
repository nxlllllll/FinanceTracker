using FinanceTracker.Core.Domains.Category;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Notifications;

public sealed record CategoryCreatedNotification(
	Guid CategoryId,
	Guid UserId,
	string Name,
	CategoryType Type,
	Guid? ParentId,
	DateTimeOffset OccurredAt
) : INotification;