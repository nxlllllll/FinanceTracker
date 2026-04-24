using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class TransactionFactory
{
	public static TransactionDto Create(Guid? accountId = null)
	{
		return new TransactionDto(
			Id: Guid.NewGuid(),
			AccountId: accountId ?? Guid.NewGuid(),
			UserId: Guid.NewGuid(),
			CategoryId: Guid.NewGuid(),
			Amount: 1000m,
			Direction: DirectionType.Debit,
			ExchangeRate: 1m,
			IsExcluded: false,
			IsRatePending: false,
			Description: null,
			OccurredAt: DateTime.UtcNow
		);
	}
}