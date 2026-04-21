using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.Currencies.Queries.GetCurrencies;

public sealed record GetCurrenciesQuery : IRequest<IReadOnlyList<CurrencyDto>>;