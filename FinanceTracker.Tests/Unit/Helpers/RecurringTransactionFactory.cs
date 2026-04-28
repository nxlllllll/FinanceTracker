using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class RecurringTransactionFactory
{
	public static RecurringTransactionDto Create(
		Guid? id = null,
		Guid? userId = null,
		Guid? accountId = null,
		Guid? categoryId = null,
		decimal amount = 5000m,
		string currency = "RUB",
		DirectionType direction = DirectionType.Debit,
		int dayOfMonth = 15,
		string? description = "Monthly rent",
		bool isActive = true,
		DateTime? lastExecutedAt = null)
	{
		return new RecurringTransactionDto(
			Id: id ?? Guid.NewGuid(),
			UserId: userId ?? Guid.NewGuid(),
			AccountId: accountId ?? Guid.NewGuid(),
			CategoryId: categoryId ?? Guid.NewGuid(),
			Amount: amount,
			Currency: currency,
			Direction: direction,
			DayOfMonth: dayOfMonth,
			Description: description,
			IsActive: isActive,
			LastExecutedAt: lastExecutedAt,
			CreatedAt: DateTime.UtcNow
		);
	}
}