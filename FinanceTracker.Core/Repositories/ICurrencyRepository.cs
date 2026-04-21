using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Core.Repositories;

public interface ICurrencyRepository
{
	Task<IReadOnlyList<CurrencyDto>> GetAllAsync(CancellationToken ct = default);

	Task<CurrencyDto?> GetByCodeAsync(
		string code,
		CancellationToken ct = default
	);
}