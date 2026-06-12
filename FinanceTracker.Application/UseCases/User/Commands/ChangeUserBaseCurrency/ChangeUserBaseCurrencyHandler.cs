using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserBaseCurrency;

public sealed class ChangeUserBaseCurrencyHandler(
	IUserWriteRepository userWriteRepository,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeUserBaseCurrencyCommand, Core.Domains.User.User, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeUserBaseCurrencyCommand command,
		Core.Domains.User.User user,
		CancellationToken ct = default)
	{
		Core.ValueObjects.Currency oldBaseCurrency = user.BaseCurrency;

		Result<Unit, DomainException> result = user.ChangeBaseCurrency(newBaseCurrency: command.NewBaseCurrency);
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		if (user.BaseCurrency == oldBaseCurrency)
			return Result<Guid, DomainException>.Success(value: user.Id);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () => await userWriteRepository.ChangeBaseCurrencyAsync(
			userId: command.UserId, 
			expectedVersion: user.RowVersion,
			newBaseCurrencyCode: command.NewBaseCurrency,
			ct: ct
		), ct: ct);

		await publisher.Publish(notification: new UserBaseCurrencyChangedNotification(
			UserId: user.Id,
			OldBaseCurrency: oldBaseCurrency,
			NewBaseCurrency: command.NewBaseCurrency,
			OccurredAt: dateProvider.UtcNow
		), cancellationToken: ct);

		return Result<Guid, DomainException>.Success(value: user.Id);
	}
}