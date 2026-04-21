using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using MediatR;

namespace FinanceTracker.Application.Users.Commands.ChangeUserBaseCurrency;

public sealed class ChangeUserBaseCurrencyHandler(
	IUserRepository userRepository
) : IRequestHandler<ChangeUserBaseCurrencyCommand>
{
	public async Task Handle(
		ChangeUserBaseCurrencyCommand command,
		CancellationToken ct = default)
	{
		User user = await userRepository.GetByIdAsync(userId: command.UserId, ct: ct)
			?? throw new NotFoundException(message: "User not found.", id: command.UserId);

		user.ChangeBaseCurrency(newBaseCurrencyCode: command.NewBaseCurrency);

		await userRepository.ChangeBaseCurrencyAsync(
			userId: command.UserId,
			newBaseCurrencyCode: command.NewBaseCurrency,
			ct: ct
		);
	}
}