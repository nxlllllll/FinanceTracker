using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Currency;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Currency.Queries.GetCurrencies;

public sealed class GetCurrenciesHandler(
	ICurrencyReadRepository currencyReadRepository
) : IRequestHandler<GetCurrenciesQuery, Result<IReadOnlyList<CurrencyInfo>, AppException>>
{
	public async Task<Result<IReadOnlyList<CurrencyInfo>, AppException>> Handle(
		GetCurrenciesQuery query,
		CancellationToken ct = default
	) => Result<IReadOnlyList<CurrencyInfo>, AppException>.Success(value: await currencyReadRepository.GetAllAsync(ct: ct));
}
