using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Role.Commands.CreateRole;

public sealed record CreateRoleCommand(
	Name DisplayName,
	IReadOnlySet<Permission> Permissions
) : IRequest<Result<Guid, AppException>>;
