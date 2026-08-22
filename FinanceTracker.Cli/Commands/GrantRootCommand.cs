using FinanceTracker.Application.Services.Roles;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Cli.Commands;

/// <summary>
/// Grants the root role to an existing user, identified by e-mail.
/// </summary>
public sealed class GrantRootCommand(
	IUserAuthRepository userAuthRepository,
	IRoleRepository roleRepository,
	IUserRoleService userRoleService,
	ILogger<GrantRootCommand> logger)
{
	public async Task<int> ExecuteAsync(
		string email,
		CancellationToken ct = default)
	{
		User? user = await userAuthRepository.GetByEmailAsync(email: email, ct: ct);

		if (user is null)
		{
			logger.ZLogError(message: $"No user with e-mail '{email}'. Register the account first, then grant it root.");
			return 1;
		}

		RoleDto? rootRole = await roleRepository.GetBySystemKeyAsync(systemKey: SystemRole.Root, ct: ct);

		if (rootRole is null)
		{
			logger.ZLogError(message: $"The 'root' system role is missing. Check that the role seed migration has been applied.");
			return 1;
		}

		Result<Unit, AppException> result = await userRoleService.AssignAsync(
			userId: user.Id,
			roleId: rootRole.Id,
			assignedBy: SystemActor.Id,
			ct: ct
		);

		if (result.IsFailure)
		{
			logger.ZLogError(message: $"Could not grant root to {email}: {result.Error!.Message}");
			return 1;
		}

		logger.ZLogInformation(message: $"Root granted to {email} ({user.Id}). The projection catches up through the outbox, so it takes a moment to take effect.");
		return 0;
	}
}
