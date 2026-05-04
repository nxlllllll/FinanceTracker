using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Repositories.User;

namespace FinanceTracker.Application.Users.Commands.ChangeUserBaseCurrency;

public sealed class ChangeUserBaseCurrencyHandler(
	IUserWriteRepository userWriteRepository
) : IAuthorizedHandler<ChangeUserBaseCurrencyCommand, User, Guid>
{
	public async Task<Guid> HandleAsync(
		ChangeUserBaseCurrencyCommand command,
		User user,
		CancellationToken ct = default)
	{
		user.ChangeBaseCurrency(newBaseCurrency: command.NewBaseCurrency);
		await userWriteRepository.ChangeBaseCurrencyAsync(
			userId: command.UserId,
			newBaseCurrencyCode: command.NewBaseCurrency,
			ct: ct
		);
		
		return user.Id;
	}
}