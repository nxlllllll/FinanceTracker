namespace FinanceTracker.Core.Repositories.Currency;

public interface ICurrencyReadRepository
{
	Task<IReadOnlyList<CurrencyInfo>> GetAllAsync(CancellationToken ct = default);

	Task<IReadOnlyList<CurrencyInfo>> GetAllActiveAsync(CancellationToken ct = default);

	Task<CurrencyInfo?> GetByCodeAsync(
		string code,
		CancellationToken ct = default
	);
	
	Task<bool> ExistsAsync(
		string code,
		CancellationToken ct = default
	);
}
