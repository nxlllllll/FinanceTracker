using FinanceTracker.Core.Dtos;

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

	Task<IReadOnlyList<OperationDto>> GetHistoryAsync(
		Guid userId,
		OperationFilterType? type = null,
		DateTime? dateFrom = null,
		DateTime? dateTo = null,
		DateTime? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default
	);
}