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

/// <summary>Grants a system role to an existing user, identified by e-mail.</summary>
public sealed class GrantRoleCommand(
	IUserAuthRepository userAuthRepository,
	IRoleRepository roleRepository,
	IUserRoleService userRoleService,
	ILogger<GrantRoleCommand> logger)
{
	public async Task<int> ExecuteAsync(
		string email,
		string role,
		CancellationToken ct = default)
	{
		if (!Enum.TryParse(value: role, ignoreCase: true, result: out SystemRole systemRole))
		{
			logger.ZLogError(message: $"'{role}' is not a system role. Expected one of: {String.Join(separator: ", ", values: Enum.GetNames<SystemRole>().Select(selector: name => name.ToLowerInvariant()))}.");
			return 1;
		}

		User? user = await userAuthRepository.GetByEmailAsync(email: email, ct: ct);

		if (user is null)
		{
			logger.ZLogError(message: $"No user with e-mail '{email}'. Register the account first, then grant it a role.");
			return 1;
		}

		RoleDto? targetRole = await roleRepository.GetBySystemKeyAsync(systemKey: systemRole, ct: ct);

		if (targetRole is null)
		{
			logger.ZLogError(message: $"The '{systemRole}' system role is missing. Check that the role seed migration has been applied.");
			return 1;
		}

		Result<Unit, AppException> result = await userRoleService.AssignAsync(
			userId: user.Id,
			roleId: targetRole.Id,
			assignedBy: SystemActor.Id,
			ct: ct
		);

		if (result.IsFailure)
		{
			logger.ZLogError(message: $"Could not grant {systemRole} to {email}: {result.Error!.Message}");
			return 1;
		}

		logger.ZLogInformation(message: $"{systemRole} granted to {email} ({user.Id}). The projection catches up through the outbox, so it takes a moment to take effect.");
		return 0;
	}
}
