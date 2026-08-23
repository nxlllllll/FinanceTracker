using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Account;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Account.Queries.GetAccounts;

public sealed record GetAccountsQuery(
	Guid UserId,
	bool? IsArchived = null
) : IRequest<Result<IReadOnlyList<AccountReadModel>, AppException>>, IUserScopedRequest;
