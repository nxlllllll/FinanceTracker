using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.AccountTypes.Queries.GetAccountTypes;

public sealed record GetAccountTypesQuery : IRequest<IReadOnlyList<AccountTypeDto>>;