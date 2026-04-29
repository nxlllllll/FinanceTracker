using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class TransactionFactory
{
	public static TransactionDto Create(
		Guid? accountId = null,
		Guid? userId = null,
		Guid? categoryId = null,
		bool isExcluded = false,
		DirectionType direction = DirectionType.Debit)
	{
		return new TransactionDto(
			Id: Guid.NewGuid(),
			AccountId: accountId ?? Guid.NewGuid(),
			UserId: userId ?? Guid.NewGuid(),
			CategoryId: categoryId ?? Guid.NewGuid(),
			Amount: 1000m,
			Currency: "RUB",
			Direction: direction,
			ExchangeRate: 1m,
			IsExcluded: isExcluded,
			IsRatePending: false,
			Description: null,
			OccurredAt: DateTime.UtcNow
		);
	}
}