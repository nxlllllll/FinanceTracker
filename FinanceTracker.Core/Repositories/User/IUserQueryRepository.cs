using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Operation;
using FinanceTracker.Core.ReadModels.User;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Repositories.User;

public interface IUserQueryRepository : IReadRepository<UserReadModel>
{
	Task<UserReadModel?> GetByIdAsync(
		Guid userId,
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

	Task<PagedResult<ReadModels.Operation.Operation>> GetHistoryAsync(
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
