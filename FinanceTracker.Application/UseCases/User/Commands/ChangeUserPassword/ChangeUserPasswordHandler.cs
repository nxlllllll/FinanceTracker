using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Password;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserPassword;

public sealed class ChangeUserPasswordHandler(
	IUserWriteRepository userWriteRepository,
	IUserSessionWriteRepository userSessionWriteRepository,
	IPasswordHasher passwordHasher,
	IUnitOfWork unitOfWork,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeUserPasswordCommand, Core.Domains.User.User, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeUserPasswordCommand command,
		Core.Domains.User.User user,
		CancellationToken ct = default)
	{
		bool currentPasswordMatches = await passwordHasher.Verify(
			password: command.CurrentPassword,
			storedHash: user.PasswordHash
		);

		if (!currentPasswordMatches)
			return Result<Guid, AppException>.Failure(error: new InvalidCredentialsException());

		string newPasswordHash = await passwordHasher.Hash(password: command.NewPassword);

		Result<Unit, DomainException> result = user.ChangePassword(newPasswordHash: newPasswordHash);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await userWriteRepository.ChangePasswordAsync(
				userId: command.UserId,
				expectedVersion: user.RowVersion,
				newPasswordHash: newPasswordHash,
				ct: ct
			);

			await userSessionWriteRepository.RevokeAllExceptAsync(
				userId: command.UserId,
				exceptSessionId: command.CurrentSessionId,
				revokedAt: dateProvider.UtcNow,
				ct: ct
			);
		}, ct: ct);

		postCommitNotifications.Stage(notification: new UserPasswordChangedNotification(
			UserId: user.Id,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
