using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Account.Commands.UnarchiveAccount;

public sealed record UnarchiveAccountCommand(
	Guid UserId,
	Guid AccountId
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest, IHasExpectedVersion
{
	public int? ExpectedVersion { get; init; }
}
