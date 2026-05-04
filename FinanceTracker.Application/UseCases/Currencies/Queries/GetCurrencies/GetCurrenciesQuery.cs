using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.UseCases.Currencies.Queries.GetCurrencies;

public sealed record GetCurrenciesQuery : IRequest<IReadOnlyList<CurrencyDto>>;