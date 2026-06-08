using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Account.Queries.GetAccount;

public sealed record GetAccountQuery(
	Guid AccountId,
	Guid UserId
) : IRequest<Result<AccountReadModel, DomainException>>, IUserScopedRequest;