using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Repositories.User;

public interface IUserReadRepository
{
	Task<Domains.User.User?> GetByIdAsync(
		Guid userId,
		CancellationToken ct = default
	);

	Task<Domains.User.User?> GetByEmailAsync(
		string email,
		CancellationToken ct = default
	);

	Task<decimal> GetTotalBalanceAsync(
		Guid userId,
		ValueObjects.Currency baseCurrency,
		DateOnly date,
		CancellationToken ct = default
	);

	Task<(decimal Income, decimal Expense)> GetIncomeExpenseSummaryAsync(
		Guid userId,
		DateOnly period,
		CancellationToken ct = default
	);

	Task<PagedResult<OperationRecord>> GetHistoryAsync(
		Guid userId,
		OperationFilterType? type = null,
		DateTimeOffset? dateFrom = null,
		DateTimeOffset? dateTo = null,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default
	);
}
