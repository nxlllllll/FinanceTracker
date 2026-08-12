using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Currency;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Currency.Queries.GetCurrency;

public sealed record GetCurrencyQuery(string Code) : IRequest<Result<CurrencyInfo, AppException>>;
