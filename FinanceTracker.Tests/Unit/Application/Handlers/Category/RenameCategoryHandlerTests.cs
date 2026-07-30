using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Category.Commands.RenameCategory;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class RenameCategoryHandlerTests
{
	private ICategoryWriteRepository _categoryWriteRepository = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private RenameCategoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryWriteRepository = Substitute.For<ICategoryWriteRepository>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();
		_handler = new RenameCategoryHandler(
			categoryWriteRepository: _categoryWriteRepository,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_WithValidName_ShouldCallRenameAsync()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new RenameCategoryCommand(UserId: category.UserId, CategoryId: category.Id, NewName: Name.Reconstitute(value: "Транспорт")),
			category: category,
			ct: CancellationToken.None
		);

		await _categoryWriteRepository.Received(requiredNumberOfCalls: 1).RenameAsync(
			categoryId: category.Id,
			newName: Arg.Any<Name>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidName_ShouldPublishNotification()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create().Value!;
		string oldName = category.Name;

		await _handler.HandleAsync(
			command: new RenameCategoryCommand(UserId: category.UserId, CategoryId: category.Id, NewName: Name.Reconstitute(value: "Транспорт")),
			category: category,
			ct: CancellationToken.None
		);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<CategoryRenamedNotification>(n =>
			n!.CategoryId == category.Id &&
			n.UserId == category.UserId &&
			n.OldName == oldName &&
			n.NewName == "Транспорт"
		));
	}

	[Test]
	public async Task HandleAsync_WithInvalidName_ShouldReturnFailure()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create().Value!;

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new RenameCategoryCommand(UserId: category.UserId, CategoryId: category.Id, NewName: Name.Reconstitute(value: String.Empty)),
			category: category,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WithInvalidName_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new RenameCategoryCommand(UserId: category.UserId, CategoryId: category.Id, NewName: Name.Reconstitute(value: String.Empty)),
			category: category,
			ct: CancellationToken.None
		);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<CategoryRenamedNotification>());
	}
}
