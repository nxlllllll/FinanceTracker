using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionDescription;

public sealed class ChangeTransactionDescriptionHandler(
	ITransactionWriteRepository transactionWriteRepository
) : IAuthorizedHandler<ChangeTransactionDescriptionCommand, Core.Domains.Transaction.Transaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeTransactionDescriptionCommand command,
		Core.Domains.Transaction.Transaction transaction,
		CancellationToken ct = default)
	{
		if (transaction.Description == command.Description)
			return Result<Guid, DomainException>.Success(value: transaction.Id);
		
		Result<Unit, DomainException> result = transaction.ChangeDescription(description: command.Description);
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);
		
		await transactionWriteRepository.ChangeDescriptionAsync(
			transactionId: command.TransactionId,
			description: command.Description,
			ct: ct
		);
		
		return Result<Guid, DomainException>.Success(value: transaction.Id);
	}
}
