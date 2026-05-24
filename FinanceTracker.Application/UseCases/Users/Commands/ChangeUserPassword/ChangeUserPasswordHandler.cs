using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.DomainEvents;
using FinanceTracker.Core.Services.Password;

namespace FinanceTracker.Application.UseCases.Users.Commands.ChangeUserPassword;

public sealed class ChangeUserPasswordHandler(
	IUserWriteRepository userWriteRepository,
	IPasswordHasher passwordHasher,
	IDomainEventOutboxWriter domainEventOutboxWriter,
	IUnitOfWork unitOfWork,
	ICorrelationContext correlationContext,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeUserPasswordCommand, User, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeUserPasswordCommand command,
		User user,
		CancellationToken ct = default)
	{
		string newPasswordHash = await passwordHasher.Hash(password: command.NewPassword);

		Result<Unit, DomainException> result = user.ChangePassword(newPasswordHash: newPasswordHash, occurredAt: dateProvider.UtcNow);
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await userWriteRepository.ChangePasswordAsync(
				userId: command.UserId,
				newPasswordHash: newPasswordHash,
				ct: ct
			);
			await domainEventOutboxWriter.WriteAsync(
				entity: user,
				correlationId: correlationContext.CorrelationId,
				ct: ct
			);
		}, ct: ct);
		
		return Result<Guid, DomainException>.Success(value: user.Id);
	}
}