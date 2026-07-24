using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Account.Commands.RenameAccount;

public sealed record RenameAccountCommand(
	Guid UserId,
	Guid AccountId,
	Name NewName
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest, IHasExpectedVersion
{
	public int? ExpectedVersion { get; init; }
}
