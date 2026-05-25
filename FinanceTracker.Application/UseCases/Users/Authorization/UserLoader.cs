using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Users.Commands.ChangeUserBaseCurrency;
using FinanceTracker.Application.UseCases.Users.Commands.ChangeUserEmail;
using FinanceTracker.Application.UseCases.Users.Commands.ChangeUserPassword;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Users.Authorization;

public sealed class UserLoader(
	IUserReadRepository userReadRepository
) : IEntityLoader<ChangeUserBaseCurrencyCommand, User, NotFoundException>,
	IEntityLoader<ChangeUserEmailCommand, User, NotFoundException>,
	IEntityLoader<ChangeUserPasswordCommand, User, NotFoundException>
{
	public Task<Result<User, NotFoundException>> LoadAsync(
		ChangeUserBaseCurrencyCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(userId: request.UserId, ct: ct);

	public Task<Result<User, NotFoundException>> LoadAsync(
		ChangeUserEmailCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(userId: request.UserId, ct: ct);

	public Task<Result<User, NotFoundException>> LoadAsync(
		ChangeUserPasswordCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(userId: request.UserId, ct: ct);

	private async Task<Result<User, NotFoundException>> LoadAndAuthorize(Guid userId, CancellationToken ct)
	{
		User? user = await userReadRepository.GetByIdAsync(userId: userId, ct: ct);
		if (user is null) 
			return Result<User, NotFoundException>.Failure(error: new NotFoundException(message: "User not found.", id: userId));

		return Result<User, NotFoundException>.Success(value: user);
	}
}
