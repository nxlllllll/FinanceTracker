using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.DomainEvents;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserBaseCurrency;

public sealed class ChangeUserBaseCurrencyHandler(
	IUserWriteRepository userWriteRepository,
	IDomainOutboxWriter domainOutboxWriter,
	IUnitOfWork unitOfWork,
	ICorrelationContext correlationContext,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeUserBaseCurrencyCommand, Core.Domains.User.User, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeUserBaseCurrencyCommand command,
		Core.Domains.User.User user,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = user.ChangeBaseCurrency(newBaseCurrency: command.NewBaseCurrency, occurredAt: dateProvider.UtcNow);
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		if (user.DomainEvents.Count == 0)
			return Result<Guid, DomainException>.Success(value: user.Id);
		
		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await userWriteRepository.ChangeBaseCurrencyAsync(
				userId: command.UserId,
				newBaseCurrencyCode: command.NewBaseCurrency,
				ct: ct
			);
			await domainOutboxWriter.WriteAsync(
				entity: user,
				correlationId: correlationContext.CorrelationId,
				ct: ct
			);
		}, ct: ct);

		return Result<Guid, DomainException>.Success(value: user.Id);
	}
}
