using FinanceTracker.Application.UseCases.User.Queries.GetUser;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.User;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class GetUserHandlerTests
{
	private IUserQueryRepository _userQueryRepository = null!;
	private GetUserHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userQueryRepository = Substitute.For<IUserQueryRepository>();
		_handler = new GetUserHandler(userQueryRepository: _userQueryRepository);
	}

	[Test]
	public async Task Handle_WhenUserExists_ShouldReturnSuccess()
	{
		UserReadModel model = UserFactory.CreateReadModel();
		GetUserQuery query = new GetUserQuery(UserId: model.Id);

		_userQueryRepository.GetByIdAsync(
			userId: model.Id,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: model);

		Result<UserReadModel, AppException> result = await _handler.Handle(
			query: query,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: model);
	}

	[Test]
	public async Task Handle_WhenUserNotFound_ShouldReturnNotFound()
	{
		Guid userId = Guid.CreateVersion7();
		GetUserQuery query = new GetUserQuery(UserId: userId);

		_userQueryRepository.GetByIdAsync(
			userId: userId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (UserReadModel?)null);

		Result<UserReadModel, AppException> result = await _handler.Handle(
			query: query,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}
}
