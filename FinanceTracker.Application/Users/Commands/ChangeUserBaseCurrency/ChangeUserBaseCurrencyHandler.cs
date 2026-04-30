using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Repositories.User;

namespace FinanceTracker.Application.Users.Commands.ChangeUserBaseCurrency;

public sealed class ChangeUserBaseCurrencyHandler(
	IUserWriteRepository userWriteRepository
) : IAuthorizedHandler<ChangeUserBaseCurrencyCommand, User>
{
	public async Task HandleAsync(
		ChangeUserBaseCurrencyCommand command,
		User user,
		CancellationToken ct = default)
	{
		user.ChangeBaseCurrency(newBaseCurrencyCode: command.NewBaseCurrency);

		await userWriteRepository.ChangeBaseCurrencyAsync(
			userId: command.UserId,
			newBaseCurrencyCode: command.NewBaseCurrency,
			ct: ct
		);
	}
}