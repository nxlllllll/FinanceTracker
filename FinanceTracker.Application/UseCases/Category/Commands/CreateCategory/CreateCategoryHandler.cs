using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Commands.CreateCategory;

public sealed class CreateCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IUnitOfWork unitOfWork,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IRequestHandler<CreateCategoryCommand, Result<Guid, AppException>>
{
	public async Task<Result<Guid, AppException>> Handle(
		CreateCategoryCommand command,
		CancellationToken ct = default)
	{
		Core.Domains.Category.Category category = Core.Domains.Category.Category.Create(
			createdAt: dateProvider.UtcNow,
			userId: command.UserId,
			name: command.Name,
			parentId: command.ParentId,
			type: command.Type
		);

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
