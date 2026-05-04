using FinanceTracker.Application.RecurringTransactions.Queries.GetRecurringTransaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
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
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction dto = RecurringTransactionFactory.Create(userId: userId);
		_readRepository.GetByIdAsync(
			recurringTransactionId: dto.Id, 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: dto);

		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction result = await _handler.Handle(
			query: new GetRecurringTransactionQuery(UserId: userId, RecurringTransactionId: dto.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Id).IsEqualTo(expected: dto.Id);
	}

	[Test]
	public async Task Handle_WhenNotFound_ShouldThrowNotFoundException()
	{
		_readRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction?)null);

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
		_readRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.Create(userId: Guid.NewGuid()));

		await Assert.That(action: async () => await _handler.Handle(
			query: new GetRecurringTransactionQuery(
				UserId: Guid.NewGuid(),
				RecurringTransactionId: Guid.NewGuid()
			),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}
}