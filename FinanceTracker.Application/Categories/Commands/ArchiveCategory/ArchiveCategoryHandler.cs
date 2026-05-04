using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.RecurringTransaction;

namespace FinanceTracker.Application.Categories.Commands.ArchiveCategory;

public sealed class ArchiveCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IUnitOfWork unitOfWork
) : IAuthorizedHandler<ArchiveCategoryCommand, Category, Guid>
{
	public async Task<Guid> HandleAsync(
		ArchiveCategoryCommand command,
		Category category,
		CancellationToken ct = default)
	{
		category.Archive();

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await categoryWriteRepository.ArchiveAsync(categoryId: command.CategoryId, ct: ct);
			await recurringTransactionWriteRepository.DeactivateByCategoryIdAsync(categoryId: command.CategoryId, ct: ct);
		}, ct: ct);
		
		return category.Id;
	}
}