using FinanceTracker.Application.RecurringTransactions.Commands.ActivateRecurringTransaction;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class ActivateRecurringTransactionHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private IRecurringTransactionReadRepository _readRepository = null!;
	private ActivateRecurringTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_readRepository = Substitute.For<IRecurringTransactionReadRepository>();

		_handler = new ActivateRecurringTransactionHandler(
			recurringTransactionWriteRepository: _writeRepository,
			recurringTransactionReadRepository: _readRepository
		);
	}

	[Test]
	public async Task Handle_WhenInactive_ShouldCallActivateAsync()
	{
		Guid userId = Guid.NewGuid();
		Guid id = Guid.NewGuid();
		_readRepository.GetByIdAsync(
			recurringTransactionId: id, 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.Create(userId: userId, id: id, isActive: false));

		await _handler.Handle(
			command: new ActivateRecurringTransactionCommand(UserId: userId, RecurringTransactionId: id),
			ct: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).ActivateAsync(
			recurringTransactionId: id,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenAlreadyActive_ShouldNotCallRepository()
	{
		Guid userId = Guid.NewGuid();
		Guid id = Guid.NewGuid();
		_readRepository.GetByIdAsync(
			recurringTransactionId: id, 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.Create(userId: userId, id: id, isActive: true));

		await _handler.Handle(
			command: new ActivateRecurringTransactionCommand(UserId: userId, RecurringTransactionId: id),
			ct: CancellationToken.None
		);

		await _writeRepository.DidNotReceive().ActivateAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenNotFound_ShouldThrowNotFoundException()
	{
		_readRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (RecurringTransactionDto?)null);

		await Assert.That(action: async () => await _handler.Handle(
			command: new ActivateRecurringTransactionCommand(
				UserId: Guid.NewGuid(),
				RecurringTransactionId: Guid.NewGuid()
			),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}

	[Test]
	public async Task Handle_WhenBelongsToAnotherUser_ShouldThrowNotFoundException()
	{
		Guid id = Guid.NewGuid();
		_readRepository.GetByIdAsync(
			recurringTransactionId: id, 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.Create(userId: Guid.NewGuid(), id: id, isActive: false));

		await Assert.That(action: async () => await _handler.Handle(
			command: new ActivateRecurringTransactionCommand(
				UserId: Guid.NewGuid(),
				RecurringTransactionId: id
			),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}
}