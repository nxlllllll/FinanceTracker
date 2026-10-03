using System.Globalization;
using FinanceTracker.Contracts.Events.Budget;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Services.Notification;

namespace FinanceTracker.Worker.Notification.Mapping;

public sealed class BudgetThresholdReachedMapper(
	ICategoryReadRepository categoryReadRepository
) : UserNotificationMapper<BudgetThresholdReachedEvent>
{
	public const string ThresholdReachedTemplate = "BudgetThresholdReached";
	public const string LimitReachedTemplate = "BudgetLimitReached";
	public const string UnknownCategoryName = "category";

	protected override async Task<NotificationRequest> MapAsync(
		BudgetThresholdReachedEvent notification,
		CancellationToken ct)
	{
		CategoryReadModel? category = await categoryReadRepository.GetByIdAsync(
			categoryId: notification.CategoryId,
			userId: notification.UserId,
			ct: ct
		);

		return new NotificationRequest(
			EventId: notification.EventId,
			UserId: notification.UserId,
			Template: notification.Threshold >= 100 ? LimitReachedTemplate : ThresholdReachedTemplate,
			Values: new Dictionary<string, string>
			{
				["Category"] = category?.Name.Value ?? UnknownCategoryName,
				["Threshold"] = notification.Threshold.ToString(provider: CultureInfo.InvariantCulture),
				["Spent"] = notification.Spent.ToString(format: "N2", provider: CultureInfo.InvariantCulture),
				["Limit"] = notification.Limit.ToString(format: "N2", provider: CultureInfo.InvariantCulture),
				["Currency"] = notification.Currency,
				["Percent"] = Math.Round(d: notification.Spent / notification.Limit * 100, mode: MidpointRounding.AwayFromZero)
					.ToString(format: "0", provider: CultureInfo.InvariantCulture)
			}
		);
	}
}
