using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Currency;
using MediatR;

namespace FinanceTracker.Application.UseCases.Currencies.Queries.GetCurrencies;

public sealed class GetCurrenciesHandler(
	ICurrencyReadRepository currencyReadRepository
) : IRequestHandler<GetCurrenciesQuery, IReadOnlyList<CurrencyDto>>
{
	public async Task<IReadOnlyList<CurrencyDto>> Handle(
		GetCurrenciesQuery query,
		CancellationToken ct = default
	) => await currencyReadRepository.GetAllAsync(ct: ct);
}