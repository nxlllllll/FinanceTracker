using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using MediatR;

namespace FinanceTracker.Application.Users.Commands.ChangeUserPassword;

public sealed class ChangeUserPasswordHandler(
	IUserRepository userRepository
) : IRequestHandler<ChangeUserPasswordCommand>
{
	public async Task Handle(
		ChangeUserPasswordCommand command,
		CancellationToken ct = default)
	{
		User user = await userRepository.GetByIdAsync(userId: command.UserId, ct: ct)
			?? throw new NotFoundException(message: "User not found.", id: command.UserId);

		user.ChangePassword(newPasswordHash: command.NewPasswordHash);

		await userRepository.ChangePasswordAsync(
			userId: command.UserId,
			newPasswordHash: command.NewPasswordHash,
			ct: ct
		);
	}
}