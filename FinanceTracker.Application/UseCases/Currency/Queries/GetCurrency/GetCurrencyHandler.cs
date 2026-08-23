using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Currency;
using FinanceTracker.Core.ReadModels.Currency;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Currency.Queries.GetCurrency;

public sealed class GetCurrencyHandler(
	ICurrencyReadRepository currencyReadRepository
) : IRequestHandler<GetCurrencyQuery, Result<CurrencyInfo, AppException>>
{
	public async Task<Result<CurrencyInfo, AppException>> Handle(
		GetCurrencyQuery query,
		CancellationToken ct = default)
	{
		CurrencyInfo? model = await currencyReadRepository.GetByCodeAsync(
			code: query.Code,
			ct: ct
		);

		if (model is not null)
			return Result<CurrencyInfo, AppException>.Success(value: model);

		return Result<CurrencyInfo, AppException>.Failure(
			error: new CurrencyNotFoundException(message: $"Currency '{query.Code}' not found.",
			code: query.Code
		));
	}
}
