using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using MediatR;

namespace FinanceTracker.Application.Users.Commands.ChangeUserEmail;

public sealed class ChangeUserEmailHandler(
	IUserRepository userRepository
) : IRequestHandler<ChangeUserEmailCommand>
{
	public async Task Handle(
		ChangeUserEmailCommand command,
		CancellationToken ct = default)
	{
		User user = await userRepository.GetByIdAsync(userId: command.UserId, ct: ct)
			?? throw new NotFoundException(message: "User not found.", id: command.UserId);

		User? existing = await userRepository.GetByEmailAsync(email: command.NewEmail, ct: ct);
		if (existing is not null)
			throw new EmailException(message: "The user with this email address already exists.", email: command.NewEmail);

		user.ChangeEmail(newEmail: command.NewEmail);

		await userRepository.ChangeEmailAsync(
			userId: command.UserId,
			newEmail: command.NewEmail,
			ct: ct
		);
	}
}