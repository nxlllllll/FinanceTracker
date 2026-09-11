using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Services.Categories;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Category.Commands.ChangeCategoryParent;

public sealed class ChangeCategoryParentHandler(
	ICategoryReadRepository categoryReadRepository,
	ICategoryWriteRepository categoryWriteRepository,
	ICategoryTreePolicy categoryTreePolicy,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeCategoryParentCommand, Core.Domains.Category.Category, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeCategoryParentCommand command,
		Core.Domains.Category.Category category,
		CancellationToken ct = default)
	{
		Guid? oldParentId = category.ParentId;
		Core.Domains.Category.CategoryType? newParentType = null;

		if (command.NewParentId is not null)
		{
			CategoryReadModel? parent = await categoryReadRepository.GetByIdAsync(
				categoryId: command.NewParentId.Value,
				userId: command.UserId,
				ct: ct
			);

			if (parent is null)
				return Result<Guid, AppException>.Failure(error: new NotFoundException(message: "Parent category not found.", id: command.NewParentId.Value));

			newParentType = parent.Type;
		}

		Result<Unit, DomainException> placement = await categoryTreePolicy.EnsurePlaceableAsync(
			userId: command.UserId,
			parentId: command.NewParentId,
			movingCategoryId: category.Id,
			ct: ct
		);

		if (placement.IsFailure)
			return Result<Guid, AppException>.Failure(error: placement.Error!);

		Result<bool, DomainException> result = category.ChangeParent(
			newParentId: command.NewParentId,
			newParentType: newParentType
		);

		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (!result.Value)
			return Result<Guid, AppException>.Success(value: category.Id);

		await categoryWriteRepository.ChangeParentAsync(
			categoryId: category.Id,
			newParentId: command.NewParentId,
			expectedVersion: category.RowVersion,
			ct: ct
		);

		postCommitNotifications.Stage(notification: new CategoryParentChangedNotification(
			CategoryId: category.Id,
			UserId: category.UserId,
			OldParentId: oldParentId,
			NewParentId: command.NewParentId,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: category.Id);
	}
}
