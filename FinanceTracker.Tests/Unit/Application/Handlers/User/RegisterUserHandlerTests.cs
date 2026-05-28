using FinanceTracker.Application.UseCases.User.Commands.RegisterUser;
using FinanceTracker.Core.Domains.Abstractions.DomainEvent;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Core.Services.DomainEvents;
using FinanceTracker.Core.Services.Password;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class RegisterUserHandlerTests
{
	private IUserWriteRepository _userWriteRepository = null!;
	private IUserAuthRepository _userAuthRepository = null!;
	private IPasswordHasher _passwordHasher = null!;
	private IDomainOutboxWriter _domainOutboxWriter = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ICorrelationContext _correlationContext = null!;
	private RegisterUserHandler _handler = null!;

	private const string HashedPassword = "hashed_password_value";

	[Before(hookType: Test)]
	public void Setup()
	{
		_userAuthRepository = Substitute.For<IUserAuthRepository>();
		_userWriteRepository = Substitute.For<IUserWriteRepository>();
		_passwordHasher = Substitute.For<IPasswordHasher>();
		_domainOutboxWriter = Substitute.For<IDomainOutboxWriter>();
		_correlationContext = Substitute.For<ICorrelationContext>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_passwordHasher.Hash(password: Arg.Any<string>()).Returns(returnThis: HashedPassword);
		_correlationContext.CorrelationId.Returns(returnThis: Guid.CreateVersion7());
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());

		_handler = new RegisterUserHandler(
			userWriteRepository: _userWriteRepository,
			passwordHasher: _passwordHasher,
			userAuthRepository: _userAuthRepository,
			domainOutboxWriter: _domainOutboxWriter,
			unitOfWork: _unitOfWork,
			correlationContext: _correlationContext,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<RegisterUserHandler>>()
		);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldCreateUser()
	{
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		await _handler.Handle(
			command: RegisterUserCommandFactory.Create(),
			ct: CancellationToken.None
		);

		await _userWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			user: Arg.Is<FinanceTracker.Core.Domains.User.User>(u =>
				u.Email == "test@test.com" &&
				u.BaseCurrency == "RUB"
			),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldWriteDomainEventToOutbox()
	{
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		await _handler.Handle(
			command: RegisterUserCommandFactory.Create(),
			ct: CancellationToken.None
		);

		await _domainOutboxWriter.Received(requiredNumberOfCalls: 1).WriteAsync(
			entity: Arg.Is<IHasDomainEvents>(e => e is FinanceTracker.Core.Domains.User.User),
			correlationId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldHashPassword()
	{
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		await _handler.Handle(
			command: RegisterUserCommandFactory.Create(password: "password123"),
			ct: CancellationToken.None
		);

		await _passwordHasher.Received(requiredNumberOfCalls: 1).Hash(password: "password123");
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldReturnUserId()
	{
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		Result<Guid, DomainException> result = await _handler.Handle(
			command: RegisterUserCommandFactory.Create(),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsNotDefault();
	}

	[Test]
	public async Task Handle_WithDuplicateEmail_ShouldReturnEmailException()
	{
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: UserFactory.Create().Value!);

		Result<Guid, DomainException> result = await _handler.Handle(
			command: RegisterUserCommandFactory.Create(),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<EmailException>();
	}

	[Test]
	public async Task Handle_WithDuplicateEmail_ShouldNotWriteToOutbox()
	{
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: UserFactory.Create().Value!);

		await _handler.Handle(
			command: RegisterUserCommandFactory.Create(),
			ct: CancellationToken.None
		);

		await _domainOutboxWriter.DidNotReceive().WriteAsync(
			entity: Arg.Any<IHasDomainEvents>(),
			correlationId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
