using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories;
using MediatR;

namespace FinanceTracker.Application.Currencies.Queries.GetCurrency;

public sealed class GetCurrencyHandler(
	ICurrencyRepository currencyRepository
) : IRequestHandler<GetCurrencyQuery, CurrencyDto?>
{
	public async Task<CurrencyDto?> Handle(
		GetCurrencyQuery query, 
		CancellationToken ct = default
	) => await currencyRepository.GetByCodeAsync(code: query.Code, ct: ct);
}