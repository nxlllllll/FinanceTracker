using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Abstractions;

public interface ITransactionNotificationFactory
{
	IAppNotification Build(
		Guid accountId,
		Guid userId,
		Guid categoryId,
		decimal amount,
		Currency currency,
		DirectionType direction,
		string? description,
		DateTime occurredAt
	);
}