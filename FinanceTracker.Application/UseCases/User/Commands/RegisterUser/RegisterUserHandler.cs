using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.DomainEvents;
using FinanceTracker.Core.Services.Password;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.User.Commands.RegisterUser;

public sealed class RegisterUserHandler(
	IUserReadRepository userReadRepository,
	IUserWriteRepository userWriteRepository,
	IDomainOutboxWriter domainOutboxWriter,
	IPasswordHasher passwordHasher,
	IUnitOfWork unitOfWork,
	ICorrelationContext correlationContext,
	IDateProvider dateProvider,
	ILogger<RegisterUserHandler> logger
) : IRequestHandler<RegisterUserCommand, Result<Guid, DomainException>>
{
	public async Task<Result<Guid, DomainException>> Handle(
		RegisterUserCommand command,
		CancellationToken ct = default)
	{
		Core.Domains.User.User? existing = await userReadRepository.GetByEmailAsync(email: command.Email, ct: ct);
		if (existing is not null)
			return Result<Guid, DomainException>.Failure(error: new EmailException(message: "The user with this email address already exists.", email: command.Email));

		string passwordHash = await passwordHasher.Hash(password: command.Password);

		Result<Core.Domains.User.User, DomainException> userResult = Core.Domains.User.User.Register(
			createdAt: dateProvider.UtcNow,
			email: command.Email,
			passwordHash: passwordHash,
			baseCurrency: command.BaseCurrencyCode
		);
		if (userResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: userResult.Error!);

		Core.Domains.User.User user = userResult.Value!;

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await userWriteRepository.CreateAsync(user: user, ct: ct);
			await domainOutboxWriter.WriteAsync(entity: user, correlationId: correlationContext.CorrelationId, ct: ct);
		}, ct: ct);

		logger.ZLogInformation(message: $"User {user.Id} registered successfully.");

		return Result<Guid, DomainException>.Success(value: user.Id);
	}
}
