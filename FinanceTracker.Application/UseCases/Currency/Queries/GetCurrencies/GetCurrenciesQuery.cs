using FinanceTracker.Core.Repositories.Currency;
using MediatR;

namespace FinanceTracker.Application.UseCases.Currency.Queries.GetCurrencies;

public sealed record GetCurrenciesQuery : IRequest<IReadOnlyList<CurrencyInfo>>;
