using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Currency;
using MediatR;

namespace FinanceTracker.Application.UseCases.Currency.Queries.GetCurrencies;

public sealed class GetCurrenciesHandler(
	ICurrencyReadRepository currencyReadRepository
) : IRequestHandler<GetCurrenciesQuery, IReadOnlyList<CurrencyInfo>>
{
	public async Task<IReadOnlyList<CurrencyInfo>> Handle(
		GetCurrenciesQuery query,
		CancellationToken ct = default
	) => await currencyReadRepository.GetAllAsync(ct: ct);
}
