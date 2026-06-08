using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Currency.Queries.GetCurrency;

public sealed class GetCurrencyHandler(
	ICurrencyReadRepository currencyReadRepository
) : IRequestHandler<GetCurrencyQuery, Result<CurrencyInfo, DomainException>>
{
	public async Task<Result<CurrencyInfo, DomainException>> Handle(
		GetCurrencyQuery query,
		CancellationToken ct = default)
	{
		CurrencyInfo? model = await currencyReadRepository.GetByCodeAsync(
			code: query.Code,
			ct: ct
		);

		if (model is null)
			return Result<CurrencyInfo, DomainException>.Failure(error: new CurrencyException(message: $"Currency '{query.Code}' not found."));

		return Result<CurrencyInfo, DomainException>.Success(value: model);
	}
}