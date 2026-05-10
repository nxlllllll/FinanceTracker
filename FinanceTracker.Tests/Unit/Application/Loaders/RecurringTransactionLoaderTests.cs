using FinanceTracker.Application.UseCases.RecurringTransactions.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ActivateRecurringTransaction;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Loaders;

public sealed class RecurringTransactionLoaderTests
{
	private IRecurringTransactionReadRepository _readRepository = null!;
	private RecurringTransactionLoader _loader = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_readRepository = Substitute.For<IRecurringTransactionReadRepository>();
		_loader = new RecurringTransactionLoader(recurringTransactionReadRepository: _readRepository);
	}

	[Test]
	public async Task LoadAsync_WhenNotFound_ShouldThrowNotFoundException()
	{
		_readRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<RecurringTransaction?>(result: null));

		Result<RecurringTransaction, NotFoundException> result = await _loader.LoadAsync(
			request: new ActivateRecurringTransactionCommand(UserId: Guid.CreateVersion7(), RecurringTransactionId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenBelongsToAnotherUser_ShouldThrowNotFoundException()
	{
		RecurringTransaction recurringTransaction = RecurringTransactionFactory.Create().Value!;
		_readRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: recurringTransaction);

		Result<RecurringTransaction, NotFoundException> result = await _loader.LoadAsync(
			request: new ActivateRecurringTransactionCommand(UserId: Guid.CreateVersion7(), RecurringTransactionId: recurringTransaction.Id),
			ct: CancellationToken.None
		);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenOwner_ShouldReturnDto()
	{
		RecurringTransaction recurringTransaction = RecurringTransactionFactory.Create().Value!;
		_readRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: recurringTransaction);

		Result<RecurringTransaction, NotFoundException> result = await _loader.LoadAsync(
			request: new ActivateRecurringTransactionCommand(UserId: recurringTransaction.UserId, RecurringTransactionId: recurringTransaction.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.Id).IsEqualTo(expected: recurringTransaction.Id);
	}
}