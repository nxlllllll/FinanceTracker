using FinanceTracker.Core.Repositories.Currency;
using MediatR;

namespace FinanceTracker.Application.UseCases.Currency.Queries.GetCurrency;

public sealed class GetCurrencyHandler(
	ICurrencyReadRepository currencyReadRepository
) : IRequestHandler<GetCurrencyQuery, CurrencyInfo?>
{
	public async Task<CurrencyInfo?> Handle(
		GetCurrencyQuery query, 
		CancellationToken ct = default
	) => await currencyReadRepository.GetByCodeAsync(code: query.Code, ct: ct);
}
