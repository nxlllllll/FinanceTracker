using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.User;
using MediatR;

namespace FinanceTracker.Application.Users.Commands.RegisterUser;

public sealed class RegisterUserHandler(
	IUserReadRepository userReadRepository,
	IUserWriteRepository userWriteRepository
) : IRequestHandler<RegisterUserCommand, Guid>
{
	public async Task<Guid> Handle(
		RegisterUserCommand command,
		CancellationToken ct = default)
	{
		User? existing = await userReadRepository.GetByEmailAsync(email: command.Email, ct: ct);
		if (existing is not null)
			throw new EmailException(message: "The user with this email address already exists.", email: command.Email);

		User user = User.Register(
			email: command.Email,
			passwordHash: command.PasswordHash,
			baseCurrencyCode: command.BaseCurrencyCode
		);

		await userWriteRepository.CreateAsync(user: user, ct: ct);
		return user.Id;
	}
}