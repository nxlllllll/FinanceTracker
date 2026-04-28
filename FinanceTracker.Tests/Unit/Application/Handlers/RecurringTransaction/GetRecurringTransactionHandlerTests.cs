using FinanceTracker.Application.RecurringTransactions.Queries.GetRecurringTransaction;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class GetRecurringTransactionHandlerTests
{
	private IRecurringTransactionReadRepository _readRepository = null!;
	private GetRecurringTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_readRepository = Substitute.For<IRecurringTransactionReadRepository>();
		_handler = new GetRecurringTransactionHandler(
			recurringTransactionReadRepository: _readRepository
		);
	}

	[Test]
	public async Task Handle_WhenFound_ShouldReturnDto()
	{
		Guid userId = Guid.NewGuid();
		Guid id = Guid.NewGuid();
		RecurringTransactionDto dto = RecurringTransactionFactory.Create(userId: userId, id: id);
		_readRepository.GetByIdAsync(
			recurringTransactionId: id, 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: dto);

		RecurringTransactionDto result = await _handler.Handle(
			query: new GetRecurringTransactionQuery(UserId: userId, RecurringTransactionId: id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Id).IsEqualTo(expected: id);
	}

	[Test]
	public async Task Handle_WhenNotFound_ShouldThrowNotFoundException()
	{
		_readRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (RecurringTransactionDto?)null);

		await Assert.That(action: async () => await _handler.Handle(
			query: new GetRecurringTransactionQuery(
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
		).Returns(returnThis: RecurringTransactionFactory.Create(userId: Guid.NewGuid(), id: id));

		await Assert.That(action: async () => await _handler.Handle(
			query: new GetRecurringTransactionQuery(
				UserId: Guid.NewGuid(),
				RecurringTransactionId: id
			),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}
}