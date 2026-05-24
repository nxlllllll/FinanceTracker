using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.DomainEvents;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Application.UseCases.Users.Commands.ChangeUserEmail;

public sealed class ChangeUserEmailHandler(
	IUserReadRepository userReadRepository,
	IUserWriteRepository userWriteRepository,
	IDomainEventOutboxWriter domainEventOutboxWriter,
	IUnitOfWork unitOfWork,
	ICorrelationContext correlationContext,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeUserEmailCommand, User, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeUserEmailCommand command,
		User user,
		CancellationToken ct = default)
	{
		User? existing = await userReadRepository.GetByEmailAsync(email: command.NewEmail, ct: ct);
		if (existing is not null)
			return Result<Guid, DomainException>.Failure(error: new EmailException(message: "The user with this email address already exists.", email: command.NewEmail));

		Result<Email, DomainException> newEmailResult = Email.Create(value: command.NewEmail);
		if (newEmailResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: newEmailResult.Error!);

		Result<Unit, DomainException> result = user.ChangeEmail(newEmail: newEmailResult.Value, occurredAt: dateProvider.UtcNow);
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		try
		{
			await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			{
				await userWriteRepository.ChangeEmailAsync(userId: command.UserId, newEmail: newEmailResult.Value, ct: ct);
				await domainEventOutboxWriter.WriteAsync(entity: user, correlationId: correlationContext.CorrelationId, ct: ct);
			}, ct: ct);
		}
		catch (EmailException exception)
		{
			return Result<Guid, DomainException>.Failure(error: exception);
		}

		return Result<Guid, DomainException>.Success(value: user.Id);
	}
}