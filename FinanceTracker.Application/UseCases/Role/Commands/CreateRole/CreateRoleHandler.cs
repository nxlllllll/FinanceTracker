using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Role.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;

namespace FinanceTracker.Application.UseCases.Role.Commands.CreateRole;

public sealed class CreateRoleHandler(
	IRoleRepository roleRepository,
	IDateProvider dateProvider,
	IPostCommitNotifications postCommitNotifications
) : IRequestHandler<CreateRoleCommand, Result<Guid, AppException>>
{
	public async Task<Result<Guid, AppException>> Handle(CreateRoleCommand command, CancellationToken ct = default)
	{
		DateTimeOffset now = dateProvider.UtcNow;

		Guid roleId = await roleRepository.CreateAsync(
			displayName: command.DisplayName,
			permissions: command.Permissions,
			createdAt: now,
			ct: ct
		);

		postCommitNotifications.Stage(notification: new RoleCreatedNotification(
			RoleId: roleId,
			DisplayName: command.DisplayName.Value,
			Permissions: command.Permissions.Select(selector: p => p.ToString()).ToHashSet(),
			OccurredAt: now
		));

		return Result<Guid, AppException>.Success(value: roleId);
	}
}
