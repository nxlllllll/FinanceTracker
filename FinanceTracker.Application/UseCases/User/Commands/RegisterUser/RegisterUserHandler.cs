using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Services.Roles;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
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
			email: command.Email.Value, ct: ct);

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

		try
		{
			await unitOfWork.ExecuteInTransactionAsync(
				operation: async () => await userWriteRepository.CreateAsync(user: user, ct: ct),
				ct: ct
			);
		}
		catch (UniqueConstraintException ex)
		{
			logger.ZLogWarning(message: $"Duplicate email race condition detected for user registration: {command.Email.Masked}. Constraint: {ex.ConstraintName}.");
			return Result<Guid, AppException>.Failure(error: new EmailException(
				message: "The user with this email address already exists.",
				email: command.Email.Value
			));
		}

		postCommitNotifications.Stage(notification: new UserRegisteredNotification(
			UserId: user.Id,
			Email: user.Email,
			BaseCurrency: user.BaseCurrency,
			OccurredAt: dateProvider.UtcNow
		));

		logger.ZLogInformation(message: $"User {user.Id} registered successfully.");

		RoleDto? defaultRole = await roleRepository.GetBySystemKeyAsync(systemKey: SystemRole.User, ct: ct);

		if (defaultRole is null)
			logger.ZLogWarning(message: $"System role 'user' not found — user {user.Id} was registered without a default role.");
		else
		{
			Result<Unit, AppException> roleAssignResult = await userRoleService.AssignAsync(
				userId: user.Id,
				roleId: defaultRole.Id,
				assignedBy: user.Id,
				ct: ct
			);

			if (roleAssignResult.IsFailure)
				logger.ZLogWarning(message: $"Failed to assign default 'user' role to {user.Id}: {roleAssignResult.Error!.Message}.");
		}

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
