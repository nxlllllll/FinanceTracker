using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Application.UseCases.Category.Commands.RenameCategory;

public sealed class RenameCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<RenameCategoryCommand, Core.Domains.Category.Category, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		RenameCategoryCommand command,
		Core.Domains.Category.Category category,
		CancellationToken ct = default)
	{
		string oldName = category.Name;

		Result<bool, DomainException> result = category.Rename(newName: command.NewName);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (!result.Value)
			return Result<Guid, AppException>.Success(value: category.Id);

		await categoryWriteRepository.RenameAsync(
			categoryId: command.CategoryId,
			newName: command.NewName,
			expectedVersion: category.RowVersion,
			ct: ct
		);

		postCommitNotifications.Stage(notification: new CategoryRenamedNotification(
			CategoryId: category.Id,
			UserId: category.UserId,
			OldName: oldName,
			NewName: command.NewName,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: category.Id);
	}
}
