using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Core.Domains.Abstractions;

public interface ITransactionNotificationFactory
{
	IAppNotification Build(
		Guid accountId,
		Guid userId,
		Guid categoryId,
		decimal amount,
		string currency,
		DirectionType direction,
		string? description,
		DateTime occurredAt
	);
}