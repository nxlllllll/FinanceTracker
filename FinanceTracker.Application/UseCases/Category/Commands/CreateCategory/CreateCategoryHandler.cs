using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Category.Commands.CreateCategory;

public sealed class CreateCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<CreateCategoryHandler> logger
) : IRequestHandler<CreateCategoryCommand, Result<Guid, AppException>>
{
	public async Task<Result<Guid, AppException>> Handle(
		CreateCategoryCommand command,
		CancellationToken ct = default)
	{
		Result<Name, DomainException> nameResult = Name.Create(value: command.Name);
		if (nameResult.IsFailure)
			return Result<Guid, AppException>.Failure(error: nameResult.Error!);

		Core.Domains.Category.Category category = Core.Domains.Category.Category.Create(
			createdAt: dateProvider.UtcNow,
			userId: command.UserId,
			name: nameResult.Value,
			parentId: command.ParentId,
			type: command.Type
		);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () => await categoryWriteRepository.CreateAsync(category: category, ct: ct), ct: ct);

		try
		{
			await publisher.Publish(notification: new CategoryCreatedNotification(
				CategoryId: category.Id,
				UserId: category.UserId,
				Name: category.Name,
				Type: category.Type,
				ParentId: category.ParentId,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish CategoryCreatedNotification for category {category.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: category.Id);
	}
}
