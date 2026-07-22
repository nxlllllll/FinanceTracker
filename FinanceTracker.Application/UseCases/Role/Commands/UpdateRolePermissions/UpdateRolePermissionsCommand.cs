using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.UpdateRolePermissions;

public sealed record UpdateRolePermissionsCommand(
	Guid RoleId,
	IReadOnlySet<Permission> NewPermissions,
	Guid UpdatedBy
) : IRequest<Result<Unit, AppException>>;
