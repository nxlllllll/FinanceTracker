using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Password;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Users.Commands.RegisterUser;

public sealed class RegisterUserHandler(
	IUserReadRepository userReadRepository,
	IUserWriteRepository userWriteRepository,
	IPasswordHasher passwordHasher,
	IDateProvider dateProvider,
	ILogger<RegisterUserHandler> logger
) : IRequestHandler<RegisterUserCommand, Result<Guid, DomainException>>
{
	public async Task<Result<Guid, DomainException>> Handle(
		RegisterUserCommand command,
		CancellationToken ct = default)
	{
		User? existing = await userReadRepository.GetByEmailAsync(email: command.Email, ct: ct);
		if (existing is not null)
			return Result<Guid, DomainException>.Failure(error: new EmailException(message: "The user with this email address already exists.", email: command.Email));
 
		Result<Email, DomainException> emailResult = Email.Create(value: command.Email);
		if (emailResult.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: emailResult.Error!);
 
		Result<Currency, DomainException> currencyResult = Currency.Create(value: command.BaseCurrencyCode);
		if (currencyResult.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: currencyResult.Error!);
 
		string passwordHash = await passwordHasher.Hash(password: command.Password);

		Result<User, DomainException> userResult = User.Register(
			createdAt: dateProvider.UtcNow,
			email: emailResult.Value!,
			passwordHash: passwordHash,
			baseCurrency: currencyResult.Value!
		);
		if (userResult.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: userResult.Error!);
 
		User user = userResult.Value!;

		await userWriteRepository.CreateAsync(user: user, ct: ct);
		logger.ZLogInformation(message: $"User {user.Id} registered successfully.");

		return Result<Guid, DomainException>.Success(value: user.Id);
	}
}