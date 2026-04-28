using FinanceTracker.Application.RecurringTransactions.Commands.DeactivateRecurringTransaction;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class DeactivateRecurringTransactionHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private IRecurringTransactionReadRepository _readRepository = null!;
	private DeactivateRecurringTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_readRepository = Substitute.For<IRecurringTransactionReadRepository>();

		_handler = new DeactivateRecurringTransactionHandler(
			recurringTransactionWriteRepository: _writeRepository,
			recurringTransactionReadRepository: _readRepository
		);
	}

	[Test]
	public async Task Handle_WhenActive_ShouldCallDeactivateAsync()
	{
		Guid userId = Guid.NewGuid();
		Guid id = Guid.NewGuid();
		_readRepository.GetByIdAsync(
			recurringTransactionId: id,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.Create(userId: userId, id: id, isActive: true));

		await _handler.Handle(
			command: new DeactivateRecurringTransactionCommand(UserId: userId, RecurringTransactionId: id),
			ct: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).DeactivateAsync(
			recurringTransactionId: id,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenAlreadyInactive_ShouldNotCallRepository()
	{
		Guid userId = Guid.NewGuid();
		Guid id = Guid.NewGuid();
		_readRepository.GetByIdAsync(
			recurringTransactionId: id,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.Create(userId: userId, id: id, isActive: false));

		await _handler.Handle(
			command: new DeactivateRecurringTransactionCommand(UserId: userId, RecurringTransactionId: id),
			ct: CancellationToken.None
		);

		await _writeRepository.DidNotReceive().DeactivateAsync(
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
			command: new DeactivateRecurringTransactionCommand(
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
		).Returns(returnThis: RecurringTransactionFactory.Create(userId: Guid.NewGuid(), id: id, isActive: true));

		await Assert.That(action: async () => await _handler.Handle(
			command: new DeactivateRecurringTransactionCommand(
				UserId: Guid.NewGuid(),
				RecurringTransactionId: id
			),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}
}