using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transaction.Services;
using FinanceTracker.Application.UseCases.Transaction.Utilities;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Category;
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
		DomainException? validationResult = CategoryDirectionValidator.Validate(category: category, direction: command.Direction, categoryId: command.CategoryId);
		if (validationResult is not null)
			return Result<Guid, DomainException>.Failure(error: validationResult);
		
		return await transactionCreationService.CreateAsync(command: command, account: account, ct: ct);
	}
}
