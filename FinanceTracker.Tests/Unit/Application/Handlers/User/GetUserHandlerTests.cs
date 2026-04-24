using FinanceTracker.Application.Users.Queries.GetUser;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class GetUserHandlerTests
{
	private IUserRepository _userRepository = null!;
	private GetUserHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userRepository = Substitute.For<IUserRepository>();
		_handler = new GetUserHandler(userRepository: _userRepository);
	}

	[Test]
	public async Task Handle_WhenUserExists_ShouldReturnUser()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create();

		_userRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);

		GetUserQuery query = new GetUserQuery(UserId: user.Id);
		FinanceTracker.Core.Domains.User.User? result = await _handler.Handle(query: query, ct: CancellationToken.None);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result.Id).IsEqualTo(expected: user.Id);
	}

	[Test]
	public async Task Handle_WhenUserNotFound_ShouldReturnNull()
	{
		_userRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		GetUserQuery query = new GetUserQuery(UserId: Guid.NewGuid());
		FinanceTracker.Core.Domains.User.User? result = await _handler.Handle(query: query, ct: CancellationToken.None);

		await Assert.That(value: result).IsNull();
	}
}