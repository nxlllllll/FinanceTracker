using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Categories.Commands.ArchiveCategory;

public sealed class ArchiveCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IUnitOfWork unitOfWork,
	ILogger<ArchiveCategoryHandler> logger
) : IAuthorizedHandler<ArchiveCategoryCommand, Category, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ArchiveCategoryCommand command,
		Category category,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = category.Archive();
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await categoryWriteRepository.ArchiveAsync(categoryId: command.CategoryId, ct: ct);
			await recurringTransactionWriteRepository.DeactivateByCategoryIdAsync(categoryId: command.CategoryId, ct: ct);
		}, 
		onError: async exception => logger.ZLogError(message: $"Failed to archiving for category {category.Id}: {exception.Message}."),
		ct: ct);
		
		return Result<Guid, DomainException>.Success(value: category.Id);
	}
}