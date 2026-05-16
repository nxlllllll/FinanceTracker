using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Core.Repositories.Currency;

public interface ICurrencyReadRepository
{
	Task<IReadOnlyList<CurrencyDto>> GetAllAsync(CancellationToken ct = default);

	Task<IReadOnlyList<CurrencyDto>> GetAllActiveAsync(CancellationToken ct = default);

	Task<CurrencyDto?> GetByCodeAsync(
		string code,
		CancellationToken ct = default
	);
}