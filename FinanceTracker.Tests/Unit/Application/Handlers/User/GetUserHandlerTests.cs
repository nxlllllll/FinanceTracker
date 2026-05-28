using FinanceTracker.Application.UseCases.User.Queries.GetUser;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.ValueObjects;
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
	public async Task Handle_WhenUserExists_ShouldReturnUserReadModel()
	{
		UserReadModel readModel = new UserReadModel(
			Id: Guid.CreateVersion7(),
			Email: Email.Create(value: "test@test.com").Value!,
			BaseCurrency: Currency.Create(value: "RUB").Value,
			CreatedAt: FakeDateProvider.Default.UtcNow
		);

		_userQueryRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: readModel);

		UserReadModel? result = await _handler.Handle(
			query: new GetUserQuery(UserId: readModel.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: readModel.Id);
	}

	[Test]
	public async Task Handle_WhenUserNotFound_ShouldReturnNull()
	{
		_userQueryRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<UserReadModel?>(result: null));

		UserReadModel? result = await _handler.Handle(
			query: new GetUserQuery(UserId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNull();
	}
}