using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories;
using MediatR;

namespace FinanceTracker.Application.Currencies.Queries.GetCurrencies;

public sealed class GetCurrenciesHandler(
	ICurrencyRepository currencyRepository
) : IRequestHandler<GetCurrenciesQuery, IReadOnlyList<CurrencyDto>>
{
	public async Task<IReadOnlyList<CurrencyDto>> Handle(
		GetCurrenciesQuery query,
		CancellationToken ct = default
	) => await currencyRepository.GetAllAsync(ct: ct);
}