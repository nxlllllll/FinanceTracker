using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Category.Commands.ArchiveCategory;

public sealed class ArchiveCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IBudgetWriteRepository budgetWriteRepository,
	IUnitOfWork unitOfWork,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider,
	ILogger<ArchiveCategoryHandler> logger
) : IAuthorizedHandler<ArchiveCategoryCommand, Core.Domains.Category.Category, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ArchiveCategoryCommand command,
		Core.Domains.Category.Category category,
		CancellationToken ct = default)
	{
		Result<bool, DomainException> result = category.Archive();
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (!result.Value)
			return Result<Guid, AppException>.Success(value: category.Id);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await categoryWriteRepository.ArchiveAsync(categoryId: command.CategoryId, expectedVersion: category.RowVersion, ct: ct);
			await recurringTransactionWriteRepository.DeactivateByCategoryIdAsync(categoryId: command.CategoryId, ct: ct);
			await budgetWriteRepository.DeactivateByCategoryIdAsync(categoryId: command.CategoryId, ct: ct);
		},
		onError: exception =>
		{
			logger.ZLogError(exception: exception, message: $"Failed to archive category {category.Id}.");
			return Task.CompletedTask;
		},
		ct: ct);

		postCommitNotifications.Stage(notification: new CategoryArchivedNotification(
			CategoryId: category.Id,
			UserId: category.UserId,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: category.Id);
	}
}
