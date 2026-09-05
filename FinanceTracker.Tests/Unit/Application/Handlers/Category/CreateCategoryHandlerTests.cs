using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Services.Categories;
using FinanceTracker.Application.UseCases.Category.Commands.CreateCategory;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class CreateCategoryHandlerTests
{
	private ICategoryReadRepository _categoryReadRepository = null!;
	private ICategoryWriteRepository _categoryWriteRepository = null!;
	private ICategoryTreePolicy _categoryTreePolicy = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private CreateCategoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryWriteRepository = Substitute.For<ICategoryWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()?.Invoke());

		_categoryReadRepository = Substitute.For<ICategoryReadRepository>();
		_categoryTreePolicy = Substitute.For<ICategoryTreePolicy>();

		_categoryTreePolicy.EnsurePlaceableAsync(
			userId: Arg.Any<Guid>(),
			parentId: Arg.Any<Guid?>(),
			movingCategoryId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, DomainException>.Success(value: FinanceTracker.Core.Results.Unit.Default));

		_handler = new CreateCategoryHandler(
			categoryReadRepository: _categoryReadRepository,
			categoryWriteRepository: _categoryWriteRepository,
			categoryTreePolicy: _categoryTreePolicy,
			unitOfWork: _unitOfWork,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldCreateCategory()
	{
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Еда").Value,
			Type: CategoryType.Expense,
			ParentId: null
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _categoryWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			category: Arg.Is<FinanceTracker.Core.Domains.Category.Category>(c =>
				c!.Name == "Еда" &&
				c.Type == CategoryType.Expense &&
				c.IsArchived == false
			),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WithParentId_ShouldCreateCategoryWithParentId()
	{
		Guid parentId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();

		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: userId,
			Name: Name.Create(value: "Рестораны").Value,
			Type: CategoryType.Expense,
			ParentId: parentId
		);

		_categoryReadRepository.GetByIdAsync(
			categoryId: parentId,
			userId: userId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new CategoryReadModel(
			Id: parentId,
			UserId: userId,
			ParentId: null,
			Name: Name.Create(value: "Еда").Value,
			Type: CategoryType.Expense,
			IsArchived: false,
			CreatedAt: FakeDateProvider.Default.UtcNow
		));

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _categoryWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			category: Arg.Is<FinanceTracker.Core.Domains.Category.Category>(c => c!.ParentId == parentId),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldPublishNotification()
	{
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Еда").Value,
			Type: CategoryType.Expense,
			ParentId: null
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<CategoryCreatedNotification>(n =>
			n!.UserId == command.UserId &&
			n.Name == "Еда" &&
			n.Type == CategoryType.Expense
		));
	}
}
