using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Services.Roles;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Data;
using FinanceTracker.Core.Exceptions.DomainExceptions.Validation;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Password;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.User.Commands.RegisterUser;

public sealed class RegisterUserHandler(
	IUserAuthRepository userAuthRepository,
	IUserWriteRepository userWriteRepository,
	IPasswordHasher passwordHasher,
	IUnitOfWork unitOfWork,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider,
	IRoleRepository roleRepository,
	IUserRoleService userRoleService,
	ILogger<RegisterUserHandler> logger
) : IRequestHandler<RegisterUserCommand, Result<Guid, AppException>>
{
	public async Task<Result<Guid, AppException>> Handle(
		RegisterUserCommand command,
		CancellationToken ct = default)
	{
		Core.Domains.User.User? existing = await userAuthRepository.GetByEmailAsync(
			email: command.Email.Value,
			ct: ct
		);

		if (existing is not null)
			return Result<Guid, AppException>.Failure(error: new EmailException(message: "The user with this email address already exists.", email: command.Email));

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

		RoleDto? defaultRole = await roleRepository.GetBySystemKeyAsync(systemKey: SystemRole.User, ct: ct);

		if (defaultRole is null)
		{
			logger.ZLogCritical(message: $"""
				System role 'user' is missing, so a new account cannot be granted anything.
				Refusing to register {command.Email.Masked} rather than create an account that gets 403 on every request.
				Check that the role seed migration has been applied.
			""");

			throw new ConfigurationException(message: "The 'user' system role is not present in the database.");
		}

		try
		{
			await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			{
				await userWriteRepository.CreateAsync(user: user, ct: ct);

				Result<Unit, AppException> roleAssignResult = await userRoleService.AssignAsync(
					userId: user.Id,
					roleId: defaultRole.Id,
					assignedBy: user.Id,
					ct: ct
				);

				if (roleAssignResult.IsFailure)
					throw roleAssignResult.Error!;
			}, ct: ct);
		}
		catch (UniqueConstraintException ex)
		{
			logger.ZLogWarning(message: $"Duplicate email race condition detected for user registration: {command.Email.Masked}. Constraint: {ex.ConstraintName}.");
			return Result<Guid, AppException>.Failure(error: new EmailException(
				message: "The user with this email address already exists.",
				email: command.Email.Value
			));
		}
		catch (AppException ex) when (ex is not ConcurrencyConflictException)
		{
			logger.ZLogError(message: $"Registration of {command.Email.Masked} was rolled back: {ex.Message}");
			return Result<Guid, AppException>.Failure(error: ex);
		}

		postCommitNotifications.Stage(notification: new UserRegisteredNotification(
			UserId: user.Id,
			Email: user.Email,
			BaseCurrency: user.BaseCurrency,
			OccurredAt: dateProvider.UtcNow
		));

		logger.ZLogInformation(message: $"User {user.Id} registered successfully with the default 'user' role.");

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
