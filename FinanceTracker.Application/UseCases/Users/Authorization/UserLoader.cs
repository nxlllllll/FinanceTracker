using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Users.Commands.ChangeUserBaseCurrency;
using FinanceTracker.Application.UseCases.Users.Commands.ChangeUserEmail;
using FinanceTracker.Application.UseCases.Users.Commands.ChangeUserPassword;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;

namespace FinanceTracker.Application.UseCases.Users.Authorization;

public sealed class UserLoader(
	IUserReadRepository userReadRepository
) : IEntityLoader<ChangeUserBaseCurrencyCommand, User>,
	IEntityLoader<ChangeUserEmailCommand, User>,
	IEntityLoader<ChangeUserPasswordCommand, User>
{
	public Task<User> LoadAsync(
		ChangeUserBaseCurrencyCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(userId: request.UserId, ct: ct);

	public Task<User> LoadAsync(
		ChangeUserEmailCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(userId: request.UserId, ct: ct);

	public Task<User> LoadAsync(
		ChangeUserPasswordCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(userId: request.UserId, ct: ct);

	private async Task<User> LoadAndAuthorize(Guid userId, CancellationToken ct)
	{
		User user = await userReadRepository.GetByIdAsync(userId: userId, ct: ct)
			?? throw new NotFoundException(message: "User not found.", id: userId);

		return user;
	}
}