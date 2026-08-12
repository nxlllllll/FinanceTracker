using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Account;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Account.Queries.GetAccount;

public sealed record GetAccountQuery(
	Guid AccountId,
	Guid UserId
) : IRequest<Result<AccountReadModel, AppException>>, IUserScopedRequest;
