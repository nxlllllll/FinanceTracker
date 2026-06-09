using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transaction.Services;
using FinanceTracker.Application.UseCases.Transaction.Utilities;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;

public sealed class CreateTransactionHandler(
	ITransactionCreationService transactionCreationService,
	ICategoryReadRepository categoryReadRepository
) : IAuthorizedHandler<CreateTransactionCommand, Core.Domains.Account.Account, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		CreateTransactionCommand command,
		Core.Domains.Account.Account account,
		CancellationToken ct = default)
	{
		CategoryReadModel? category = await categoryReadRepository.GetByIdAsync(categoryId: command.CategoryId, userId: command.UserId, ct: ct);
		if (category is null)
			return Result<Guid, DomainException>.Failure(error: new NotFoundException(message: "Category not found.", id: command.CategoryId));

		DomainException? validationResult = CategoryDirectionValidator.Validate(category: category, direction: command.Direction);
		if (validationResult is not null)
			return Result<Guid, DomainException>.Failure(error: validationResult);
		
		if (category.IsArchived)
			return Result<Guid, DomainException>.Failure(error: new ArchivedOperationException(message: "Cannot create a transaction for an archived category."));
		
		return await transactionCreationService.CreateAsync(command: command, account: account, ct: ct);
	}
}
