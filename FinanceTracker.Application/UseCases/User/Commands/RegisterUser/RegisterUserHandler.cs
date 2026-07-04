using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Password;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.User.Commands.RegisterUser;

public sealed class RegisterUserHandler(
	IUserAuthRepository userAuthRepository,
	IUserWriteRepository userWriteRepository,
	IPasswordHasher passwordHasher,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<RegisterUserHandler> logger
) : IRequestHandler<RegisterUserCommand, Result<Guid, AppException>>
{
	public async Task<Result<Guid, AppException>> Handle(
		RegisterUserCommand command,
		CancellationToken ct = default)
	{
		Core.Domains.User.User? existing = await userAuthRepository.GetByEmailAsync(
			email: command.Email.Value, ct: ct);

		if (existing is not null)
			return Result<Guid, AppException>.Failure(error: new EmailException(
				message: "The user with this email address already exists.",
				email: command.Email));

		string passwordHash = await passwordHasher.Hash(password: command.Password);

		Result<Core.Domains.User.User, DomainException> userResult = Core.Domains.User.User.Register(
			createdAt: dateProvider.UtcNow,
			email: command.Email,
			passwordHash: passwordHash,
			baseCurrency: command.BaseCurrencyCode
		);
		if (userResult.IsFailure)
			return Result<Guid, AppException>.Failure(error: userResult.Error!);

		Core.Domains.User.User user = userResult.Value!;

		try
		{
			await unitOfWork.ExecuteInTransactionAsync(operation: async () => await userWriteRepository.CreateAsync(user: user, ct: ct), ct: ct);
		}
		catch (UniqueConstraintException ex)
		{
			logger.ZLogWarning(message: $"Duplicate email race condition detected for user registration: {command.Email.Value}. Constraint: {ex.ConstraintName}.");
			return Result<Guid, AppException>.Failure(error: new EmailException(
				message: "The user with this email address already exists.",
				email: command.Email.Value
			));
		}

		try
		{
			await publisher.Publish(notification: new UserRegisteredNotification(
				UserId: user.Id,
				Email: user.Email,
				BaseCurrency: user.BaseCurrency,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish UserRegisteredNotification for user {user.Id} after successful commit.");
		}

		logger.ZLogInformation(message: $"User {user.Id} registered successfully.");

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
