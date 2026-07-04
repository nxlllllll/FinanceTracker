using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetAccounts;

public sealed record GetAccountsQuery(
	Guid UserId,
	bool? IsArchived = null
) : IRequest<Result<IReadOnlyList<AccountReadModel>, AppException>>, IUserScopedRequest;
