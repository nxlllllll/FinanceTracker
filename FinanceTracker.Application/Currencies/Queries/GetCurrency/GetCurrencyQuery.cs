using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.Currencies.Queries.GetCurrency;

public sealed record GetCurrencyQuery(string Code) : IRequest<CurrencyDto?>;