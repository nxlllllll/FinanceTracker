using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.UseCases.Accounts.Queries.GetAccount;

public sealed record GetAccountQuery(Guid AccountId) : IRequest<AccountDto?>;