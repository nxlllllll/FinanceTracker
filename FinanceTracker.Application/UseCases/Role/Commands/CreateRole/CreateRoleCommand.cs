using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Role.Commands.CreateRole;

public sealed record CreateRoleCommand(
	Name DisplayName,
	IReadOnlySet<Permission> Permissions
) : IIdempotentCommand, IRequest<Result<Guid, AppException>>
{
	public Guid IdempotencyKey { get; init; }

	object IIdempotentCommand.IdempotencyFingerprint => new
	{
		DisplayName,
		Permissions = Permissions.Select(selector: permission => permission.ToString()).Order(comparer: StringComparer.Ordinal).ToArray()
	};
}
