using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Services.Categories;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Category.Commands.CreateCategory;

public sealed class CreateCategoryHandler(
	ICategoryReadRepository categoryReadRepository,
	ICategoryWriteRepository categoryWriteRepository,
	ICategoryTreePolicy categoryTreePolicy,
	IUnitOfWork unitOfWork,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IRequestHandler<CreateCategoryCommand, Result<Guid, AppException>>
{
	public async Task<Result<Guid, AppException>> Handle(
		CreateCategoryCommand command,
		CancellationToken ct = default)
	{
		Core.Domains.Category.CategoryType? parentType = null;

		if (command.ParentId is not null)
		{
			CategoryReadModel? parent = await categoryReadRepository.GetByIdAsync(
				categoryId: command.ParentId.Value,
				userId: command.UserId,
				ct: ct
			);

			if (parent is null)
				return Result<Guid, AppException>.Failure(error: new NotFoundException(message: "Parent category not found.", id: command.ParentId.Value));

			parentType = parent.Type;
		}

		Result<Unit, DomainException> placement = await categoryTreePolicy.EnsurePlaceableAsync(
			userId: command.UserId,
			parentId: command.ParentId,
			ct: ct
		);

		if (placement.IsFailure)
			return Result<Guid, AppException>.Failure(error: placement.Error!);

		Result<Core.Domains.Category.Category, DomainException> categoryResult = Core.Domains.Category.Category.Create(
			createdAt: dateProvider.UtcNow,
			userId: command.UserId,
			name: command.Name,
			type: command.Type,
			parentId: command.ParentId,
			parentType: parentType
		);

		if (categoryResult.IsFailure)
			return Result<Guid, AppException>.Failure(error: categoryResult.Error!);

		Core.Domains.Category.Category category = categoryResult.Value!;

		await unitOfWork.ExecuteInTransactionAsync(
			operation: async () => await categoryWriteRepository.CreateAsync(category: category, ct: ct),
			ct: ct
		);

		postCommitNotifications.Stage(notification: new CategoryCreatedNotification(
			CategoryId: category.Id,
			UserId: category.UserId,
			Name: category.Name,
			Type: category.Type,
			ParentId: category.ParentId,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: category.Id);
	}
}
