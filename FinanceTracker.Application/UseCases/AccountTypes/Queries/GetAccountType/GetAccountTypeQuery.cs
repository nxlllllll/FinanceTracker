using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.UseCases.AccountTypes.Queries.GetAccountType;

public sealed record GetAccountTypeQuery(string AccountType) : IRequest<AccountTypeDto?>;