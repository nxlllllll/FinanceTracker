using FinanceTracker.Application.UseCases.User.Commands.ChangeUserBaseCurrency;
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

public sealed class ChangeUserBaseCurrencyHandlerTests
{
	private IUserWriteRepository _userWriteRepository = null!;
	private IDomainOutboxWriter _domainOutboxWriter = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ICorrelationContext _correlationContext = null!;
	private ChangeUserBaseCurrencyHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userWriteRepository = Substitute.For<IUserWriteRepository>();
		_domainOutboxWriter = Substitute.For<IDomainOutboxWriter>();
		_correlationContext = Substitute.For<ICorrelationContext>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_correlationContext.CorrelationId.Returns(returnThis: Guid.CreateVersion7());
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());

		_handler = new ChangeUserBaseCurrencyHandler(
			userWriteRepository: _userWriteRepository,
			domainOutboxWriter: _domainOutboxWriter,
			unitOfWork: _unitOfWork,
			correlationContext: _correlationContext,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldChangeBaseCurrency()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: Currency.Create(value: "USD").Value),
			user: user,
			ct: CancellationToken.None
		);

		await _userWriteRepository.Received(requiredNumberOfCalls: 1).ChangeBaseCurrencyAsync(
			userId: Arg.Is(value: user.Id),
			newBaseCurrencyCode: Arg.Is<Currency>(value: Currency.Create(value: "USD").Value),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldWriteDomainEventToOutbox()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: Currency.Create(value: "USD").Value),
			user: user,
			ct: CancellationToken.None
		);

		await _domainOutboxWriter.Received(requiredNumberOfCalls: 1).WriteAsync(
			entity: Arg.Is<IHasDomainEvents>(e => e is FinanceTracker.Core.Domains.User.User),
			correlationId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithSameCurrency_ShouldNotChangeBaseCurrency()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;
		user.ClearDomainEvents();
		
		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: Currency.Create(value: "RUB").Value),
			user: user,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userWriteRepository.DidNotReceive().ChangeBaseCurrencyAsync(
			userId: Arg.Any<Guid>(),
			newBaseCurrencyCode: Arg.Any<Currency>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
