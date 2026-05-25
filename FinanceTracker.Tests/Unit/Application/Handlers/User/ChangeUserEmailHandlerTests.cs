using FinanceTracker.Application.UseCases.Users.Commands.ChangeUserEmail;
using FinanceTracker.Core.Domains.Abstractions.DomainEvent;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Core.Services.DomainEvents;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class ChangeUserEmailHandlerTests
{
	private IUserReadRepository _userReadRepository = null!;
	private IUserWriteRepository _userWriteRepository = null!;
	private IDomainEventOutboxWriter _domainEventOutboxWriter = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ICorrelationContext _correlationContext = null!;
	private ChangeUserEmailHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userReadRepository = Substitute.For<IUserReadRepository>();
		_userWriteRepository = Substitute.For<IUserWriteRepository>();
		_domainEventOutboxWriter = Substitute.For<IDomainEventOutboxWriter>();
		_correlationContext = Substitute.For<ICorrelationContext>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_correlationContext.CorrelationId.Returns(returnThis: Guid.CreateVersion7());
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());

		_handler = new ChangeUserEmailHandler(
			userReadRepository: _userReadRepository,
			userWriteRepository: _userWriteRepository,
			domainEventOutboxWriter: _domainEventOutboxWriter,
			unitOfWork: _unitOfWork,
			correlationContext: _correlationContext,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldChangeEmail()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_userReadRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		await _handler.HandleAsync(
			command: new ChangeUserEmailCommand(UserId: user.Id, NewEmail: "new@test.com"),
			user: user,
			ct: CancellationToken.None
		);

		await _userWriteRepository.Received(requiredNumberOfCalls: 1).ChangeEmailAsync(
			userId: user.Id,
			newEmail: Email.Create(value: "new@test.com").Value,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldWriteDomainEventToOutbox()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_userReadRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		await _handler.HandleAsync(
			command: new ChangeUserEmailCommand(UserId: user.Id, NewEmail: "new@test.com"),
			user: user,
			ct: CancellationToken.None
		);

		await _domainEventOutboxWriter.Received(requiredNumberOfCalls: 1).WriteAsync(
			entity: Arg.Is<IHasDomainEvents>(e => e is FinanceTracker.Core.Domains.User.User),
			correlationId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithDuplicateEmail_ShouldReturnEmailException()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;
		FinanceTracker.Core.Domains.User.User anotherUser = UserFactory.Create(email: "new@test.com").Value!;

		_userReadRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: anotherUser);

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ChangeUserEmailCommand(UserId: user.Id, NewEmail: "new@test.com"),
			user: user,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<EmailException>();
	}

	[Test]
	public async Task HandleAsync_WithDuplicateEmail_ShouldNotWriteToOutbox()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;
		FinanceTracker.Core.Domains.User.User anotherUser = UserFactory.Create(email: "new@test.com").Value!;

		_userReadRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: anotherUser);

		await _handler.HandleAsync(
			command: new ChangeUserEmailCommand(UserId: user.Id, NewEmail: "new@test.com"),
			user: user,
			ct: CancellationToken.None
		);

		await _domainEventOutboxWriter.DidNotReceive().WriteAsync(
			entity: Arg.Any<IHasDomainEvents>(),
			correlationId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
