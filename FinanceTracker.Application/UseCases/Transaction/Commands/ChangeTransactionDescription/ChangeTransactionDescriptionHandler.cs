using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transaction.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionDescription;

public sealed class ChangeTransactionDescriptionHandler(
	ITransactionWriteRepository transactionWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeTransactionDescriptionCommand, Core.Domains.Transaction.Transaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeTransactionDescriptionCommand command,
		Core.Domains.Transaction.Transaction accounts,
		CancellationToken ct = default)
	{
		if (accounts.Description == command.Description)
			return Result<Guid, DomainException>.Success(value: accounts.Id);
		
		string? oldDescription = accounts.Description;
		
		Result<Unit, DomainException> result = accounts.ChangeDescription(description: command.Description);
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);
		
		await transactionWriteRepository.ChangeDescriptionAsync(
			transactionId: command.TransactionId,
			expectedVersion: accounts.RowVersion,
			description: command.Description,
			ct: ct
		);
		
		await publisher.Publish(notification: new TransactionDescriptionChangedNotification(
			TransactionId: accounts.Id,
			UserId: accounts.UserId,
			OldDescription: oldDescription,
			NewDescription: command.Description,
			OccurredAt: dateProvider.UtcNow
		), cancellationToken: ct);
		
		return Result<Guid, DomainException>.Success(value: accounts.Id);
	}
}
