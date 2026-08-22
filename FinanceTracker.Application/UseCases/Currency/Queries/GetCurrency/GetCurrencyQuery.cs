using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Currency;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Currency.Queries.GetCurrency;

public sealed record GetCurrencyQuery(Core.ValueObjects.Currency Code) : IRequest<Result<CurrencyInfo, AppException>>;
