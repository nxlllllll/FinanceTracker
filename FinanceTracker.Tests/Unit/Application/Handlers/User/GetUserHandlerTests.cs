using FinanceTracker.Application.UseCases.Users.Queries.GetUser;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class GetUserHandlerTests
{
	private IUserReadRepository _userReadRepository = null!;
	private GetUserHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userReadRepository = Substitute.For<IUserReadRepository>();
		_handler = new GetUserHandler(userReadRepository: _userReadRepository);
	}

	[Test]
	public async Task Handle_WhenUserExists_ShouldReturnUser()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_userReadRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);

		FinanceTracker.Core.Domains.User.User? result = await _handler.Handle(
			query: new GetUserQuery(UserId: user.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: user.Id);
	}

	[Test]
	public async Task Handle_WhenUserNotFound_ShouldReturnNull()
	{
		_userReadRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		FinanceTracker.Core.Domains.User.User? result = await _handler.Handle(
			query: new GetUserQuery(UserId: Guid.NewGuid()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNull();
	}
}