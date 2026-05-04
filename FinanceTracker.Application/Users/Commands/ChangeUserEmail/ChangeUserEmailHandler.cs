using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;

namespace FinanceTracker.Application.Users.Commands.ChangeUserEmail;

public sealed class ChangeUserEmailHandler(
	IUserReadRepository userReadRepository,
	IUserWriteRepository userWriteRepository
) : IAuthorizedHandler<ChangeUserEmailCommand, User, Guid>
{
	public async Task<Guid> HandleAsync(
		ChangeUserEmailCommand command,
		User user,
		CancellationToken ct = default)
	{
		User? existing = await userReadRepository.GetByEmailAsync(email: command.NewEmail, ct: ct);
		if (existing is not null)
			throw new EmailException(message: "The user with this email address already exists.", email: command.NewEmail);

		user.ChangeEmail(newEmail: command.NewEmail);

		await userWriteRepository.ChangeEmailAsync(
			userId: command.UserId,
			newEmail: command.NewEmail,
			ct: ct
		);
		
		return user.Id;
	}
}