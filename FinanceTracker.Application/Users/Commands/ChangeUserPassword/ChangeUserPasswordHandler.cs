using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Repositories.User;

namespace FinanceTracker.Application.Users.Commands.ChangeUserPassword;

public sealed class ChangeUserPasswordHandler(
	IUserWriteRepository userWriteRepository
) : IAuthorizedHandler<ChangeUserPasswordCommand, User>
{
	public async Task HandleAsync(
		ChangeUserPasswordCommand command,
		User user,
		CancellationToken ct = default)
	{
		user.ChangePassword(newPasswordHash: command.NewPasswordHash);

		await userWriteRepository.ChangePasswordAsync(
			userId: command.UserId,
			newPasswordHash: command.NewPasswordHash,
			ct: ct
		);
	}
}