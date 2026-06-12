using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Category.Commands.ArchiveCategory;

public sealed class ArchiveCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IBudgetWriteRepository budgetWriteRepository,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<ArchiveCategoryHandler> logger
) : IAuthorizedHandler<ArchiveCategoryCommand, Core.Domains.Category.Category, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ArchiveCategoryCommand command,
		Core.Domains.Category.Category category,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = category.Archive();
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await categoryWriteRepository.ArchiveAsync(categoryId: command.CategoryId, expectedVersion: category.RowVersion, ct: ct);
			await recurringTransactionWriteRepository.DeactivateByCategoryIdAsync(categoryId: command.CategoryId, ct: ct);
			await budgetWriteRepository.DeactivateByCategoryIdAsync(categoryId: command.CategoryId, ct: ct);
		}, 
		onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to archive category {category.Id}."),
		ct: ct);
		
		await publisher.Publish(notification: new CategoryArchivedNotification(
			CategoryId: category.Id,
			UserId: category.UserId,
			OccurredAt: dateProvider.UtcNow
		), cancellationToken: ct);
		
		return Result<Guid, DomainException>.Success(value: category.Id);
	}
}