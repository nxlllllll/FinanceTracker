using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.User.Commands.ChangeUserBaseCurrency;
using FinanceTracker.Application.UseCases.User.Commands.ChangeUserEmail;
using FinanceTracker.Application.UseCases.User.Commands.ChangeUserPassword;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.User.Authorization;

public sealed class UserLoader(
	IUserAuthRepository userAuthRepository
) : IEntityLoader<ChangeUserBaseCurrencyCommand, Core.Domains.User.User, AppException>,
	IEntityLoader<ChangeUserEmailCommand, Core.Domains.User.User, AppException>,
	IEntityLoader<ChangeUserPasswordCommand, Core.Domains.User.User, AppException>
{
	public Task<Result<Core.Domains.User.User, AppException>> LoadAsync(
		ChangeUserBaseCurrencyCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.User.User, AppException>> LoadAsync(
		ChangeUserEmailCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.User.User, AppException>> LoadAsync(
		ChangeUserPasswordCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(userId: request.UserId, ct: ct);

	private async Task<Result<Core.Domains.User.User, AppException>> LoadAndAuthorize(Guid userId, CancellationToken ct)
	{
		Core.Domains.User.User? user = await userAuthRepository.GetByIdAsync(userId: userId, ct: ct);

		if (user is null)
			return Result<Core.Domains.User.User, AppException>.Failure(error: new NotFoundException(message: "User not found.", id: userId));

		return Result<Core.Domains.User.User, AppException>.Success(value: user);
	}
}
