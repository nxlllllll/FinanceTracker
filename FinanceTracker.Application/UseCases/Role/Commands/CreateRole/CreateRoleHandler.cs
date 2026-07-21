using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;

namespace FinanceTracker.Application.UseCases.Role.Commands.CreateRole;

public sealed class CreateRoleHandler(
	IRoleRepository roleRepository,
	IDateProvider dateProvider
) : IRequestHandler<CreateRoleCommand, Result<Guid, AppException>>
{
	public async Task<Result<Guid, AppException>> Handle(
		CreateRoleCommand command,
		CancellationToken ct = default)
	{
		Guid roleId = await roleRepository.CreateAsync(
			displayName: command.DisplayName,
			permissions: command.Permissions,
			createdAt: dateProvider.UtcNow,
			ct: ct
		);

		return Result<Guid, AppException>.Success(value: roleId);
	}
}
