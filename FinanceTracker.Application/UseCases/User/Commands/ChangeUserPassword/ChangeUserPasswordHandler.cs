using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Password;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserPassword;

public sealed class ChangeUserPasswordHandler(
	IUserWriteRepository userWriteRepository,
	IUserSessionWriteRepository userSessionWriteRepository,
	IPasswordHasher passwordHasher,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeUserPasswordCommand, Core.Domains.User.User, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeUserPasswordCommand command,
		Core.Domains.User.User user,
		CancellationToken ct = default)
	{
		string newPasswordHash = await passwordHasher.Hash(password: command.NewPassword);

		Result<Unit, DomainException> result = user.ChangePassword(newPasswordHash: newPasswordHash);
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);

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

		await publisher.Publish(notification: new UserPasswordChangedNotification(
			UserId: user.Id,
			OccurredAt: dateProvider.UtcNow
		), cancellationToken: ct);

		return Result<Guid, DomainException>.Success(value: user.Id);
	}
}