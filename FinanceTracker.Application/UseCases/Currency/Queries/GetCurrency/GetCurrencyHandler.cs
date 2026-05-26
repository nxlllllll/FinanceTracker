using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Currency;
using MediatR;

namespace FinanceTracker.Application.UseCases.Currency.Queries.GetCurrency;

public sealed class GetCurrencyHandler(
	ICurrencyReadRepository currencyReadRepository
) : IRequestHandler<GetCurrencyQuery, CurrencyDto?>
{
	public async Task<CurrencyDto?> Handle(
		GetCurrencyQuery query, 
		CancellationToken ct = default
	) => await currencyReadRepository.GetByCodeAsync(code: query.Code, ct: ct);
}
