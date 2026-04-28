using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class ChangeRecurringTransactionCurrencyHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private IRecurringTransactionReadRepository _readRepository = null!;
	private ChangeRecurringTransactionCurrencyHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_readRepository = Substitute.For<IRecurringTransactionReadRepository>();

		_handler = new ChangeRecurringTransactionCurrencyHandler(
			recurringTransactionWriteRepository: _writeRepository,
			recurringTransactionReadRepository: _readRepository
		);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldCallChangeCurrencyAsync()
	{
		Guid userId = Guid.NewGuid();
		Guid id = Guid.NewGuid();
		_readRepository.GetByIdAsync(
			recurringTransactionId: id, 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.Create(userId: userId, id: id));

		await _handler.Handle(command: new ChangeRecurringTransactionCurrencyCommand(
			UserId: userId,
			RecurringTransactionId: id,
			Currency: "USD"
		), ct: CancellationToken.None);

		await _writeRepository.Received(requiredNumberOfCalls: 1).ChangeCurrencyAsync(
			recurringTransactionId: id,
			currency: "USD",
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
			command: new ChangeRecurringTransactionCurrencyCommand(
				UserId: Guid.NewGuid(),
				RecurringTransactionId: Guid.NewGuid(),
				Currency: "USD"
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
		).Returns(returnThis: RecurringTransactionFactory.Create(userId: Guid.NewGuid(), id: id));

		await Assert.That(action: async () => await _handler.Handle(
			command: new ChangeRecurringTransactionCurrencyCommand(
				UserId: Guid.NewGuid(),
				RecurringTransactionId: id,
				Currency: "USD"
			),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}
}