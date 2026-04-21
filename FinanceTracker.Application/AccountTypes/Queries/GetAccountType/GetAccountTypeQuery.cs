using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.AccountTypes.Queries.GetAccountType;

public sealed record GetAccountTypeQuery(string AccountType) : IRequest<AccountTypeDto?>;