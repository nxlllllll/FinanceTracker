using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.UseCases.Currency.Queries.GetCurrency;

public sealed record GetCurrencyQuery(string Code) : IRequest<CurrencyDto?>;
