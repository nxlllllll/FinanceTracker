using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Commands.CreateCategory;

public sealed class CreateCategoryHandler(
	ICategoryWriteRepository categoryWriteRepository,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	IDateProvider dateProvider
) : IRequestHandler<CreateCategoryCommand, Result<Guid, DomainException>>
{
	public async Task<Result<Guid, DomainException>> Handle(
		CreateCategoryCommand command,
		CancellationToken ct = default)
	{
		Result<Name, DomainException> nameResult = Name.Create(value: command.Name);
		if (nameResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: nameResult.Error!);
		
		Core.Domains.Category.Category category = Core.Domains.Category.Category.Create(
			createdAt: dateProvider.UtcNow,
			userId: command.UserId,
			name: nameResult.Value,
			parentId: command.ParentId,
			type: command.Type
		);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () => await categoryWriteRepository.CreateAsync(category: category, ct: ct), ct: ct);
		
		await publisher.Publish(notification: new CategoryCreatedNotification(
			CategoryId: category.Id,
			UserId: category.UserId,
			Name: category.Name,
			Type: category.Type,
			ParentId: category.ParentId,
			OccurredAt: dateProvider.UtcNow
		), cancellationToken: ct);
		
		return Result<Guid, DomainException>.Success(value: category.Id);
	}
}
