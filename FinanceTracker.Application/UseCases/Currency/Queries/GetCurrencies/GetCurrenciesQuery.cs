using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Currency;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Currency.Queries.GetCurrencies;

public sealed record GetCurrenciesQuery : IRequest<Result<IReadOnlyList<CurrencyInfo>, AppException>>;
