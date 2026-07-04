using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserEmail;

public sealed class ChangeUserEmailHandler(
	IUserAuthRepository userAuthRepository,
	IUserWriteRepository userWriteRepository,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<ChangeUserEmailHandler> logger
) : IAuthorizedHandler<ChangeUserEmailCommand, Core.Domains.User.User, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeUserEmailCommand command,
		Core.Domains.User.User accounts,
		CancellationToken ct = default)
	{
		Core.Domains.User.User? existing = await userAuthRepository.GetByEmailAsync(email: command.NewEmail, ct: ct);
		if (existing is not null)
			return Result<Guid, AppException>.Failure(error: new EmailException(message: "The user with this email address already exists.", email: command.NewEmail));

		Result<Email, DomainException> newEmailResult = Email.Create(value: command.NewEmail);
		if (newEmailResult.IsFailure)
			return Result<Guid, AppException>.Failure(error: newEmailResult.Error!);

		Email oldEmail = accounts.Email;

		Result<Unit, DomainException> result = accounts.ChangeEmail(newEmail: newEmailResult.Value);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		try
		{
			await unitOfWork.ExecuteInTransactionAsync(operation: async () => await userWriteRepository.ChangeEmailAsync(
				userId: command.UserId,
				expectedVersion: accounts.RowVersion,
				newEmail: newEmailResult.Value,
				ct: ct
			), ct: ct);
		}
		catch (EmailException exception)
		{
			return Result<Guid, AppException>.Failure(error: exception);
		}

		try
		{
			await publisher.Publish(notification: new UserEmailChangedNotification(
				UserId: accounts.Id,
				OldEmail: oldEmail,
				NewEmail: newEmailResult.Value,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish UserEmailChangedNotification for user {accounts.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: accounts.Id);
	}
}
