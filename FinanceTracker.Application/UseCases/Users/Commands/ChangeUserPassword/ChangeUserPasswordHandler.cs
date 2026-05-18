using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Password;

namespace FinanceTracker.Application.UseCases.Users.Commands.ChangeUserPassword;

public sealed class ChangeUserPasswordHandler(
	IUserWriteRepository userWriteRepository,
	IPasswordHasher passwordHasher
) : IAuthorizedHandler<ChangeUserPasswordCommand, User, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeUserPasswordCommand command,
		User user,
		CancellationToken ct = default)
	{
		string newPasswordHash = await passwordHasher.Hash(password: command.NewPassword);

		Result<Unit, DomainException> result = user.ChangePassword(newPasswordHash: newPasswordHash);
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await userWriteRepository.ChangePasswordAsync(
			userId: command.UserId,
			newPasswordHash: newPasswordHash,
			ct: ct
		);
		
		return Result<Guid, DomainException>.Success(value: user.Id);
	}
}