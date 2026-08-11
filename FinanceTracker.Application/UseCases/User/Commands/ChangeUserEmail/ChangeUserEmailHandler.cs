using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Auth;
using FinanceTracker.Core.Exceptions.DomainExceptions.Validation;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Password;
using FinanceTracker.Core.ValueObjects;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserEmail;

public sealed class ChangeUserEmailHandler(
	IUserAuthRepository userAuthRepository,
	IUserWriteRepository userWriteRepository,
	IUserSessionWriteRepository userSessionWriteRepository,
	IPasswordHasher passwordHasher,
	IUnitOfWork unitOfWork,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeUserEmailCommand, Core.Domains.User.User, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeUserEmailCommand command,
		Core.Domains.User.User user,
		CancellationToken ct = default)
	{
		bool currentPasswordMatches = await passwordHasher.Verify(
			password: command.CurrentPassword,
			storedHash: user.PasswordHash
		);

		if (!currentPasswordMatches)
			return Result<Guid, AppException>.Failure(error: new InvalidCredentialsException());

		Core.Domains.User.User? existing = await userAuthRepository.GetByEmailAsync(email: command.NewEmail, ct: ct);
		if (existing is not null)
			return Result<Guid, AppException>.Failure(error: new EmailException(message: "The user with this email address already exists.", email: command.NewEmail));

		Result<Email, DomainException> newEmailResult = Email.Create(value: command.NewEmail);
		if (newEmailResult.IsFailure)
			return Result<Guid, AppException>.Failure(error: newEmailResult.Error!);

		Email oldEmail = user.Email;

		Result<Unit, DomainException> result = user.ChangeEmail(newEmail: newEmailResult.Value);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		try
		{
			await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			{
				await userWriteRepository.ChangeEmailAsync(
					userId: command.UserId,
					expectedVersion: user.RowVersion,
					newEmail: newEmailResult.Value,
					ct: ct
				);

				await userSessionWriteRepository.RevokeAllExceptAsync(
					userId: command.UserId,
					exceptSessionId: command.CurrentSessionId,
					revokedAt: dateProvider.UtcNow,
					ct: ct
				);
			}, ct: ct);
		}
		catch (EmailException exception)
		{
			return Result<Guid, AppException>.Failure(error: exception);
		}

		postCommitNotifications.Stage(notification: new UserEmailChangedNotification(
			UserId: user.Id,
			OldEmail: oldEmail,
			NewEmail: newEmailResult.Value,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
