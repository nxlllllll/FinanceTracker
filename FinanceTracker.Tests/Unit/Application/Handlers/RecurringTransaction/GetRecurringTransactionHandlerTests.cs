using FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class GetRecurringTransactionHandlerTests
{
	private IRecurringTransactionReadRepository _recurringTransactionReadRepository = null!;
	private GetRecurringTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_recurringTransactionReadRepository = Substitute.For<IRecurringTransactionReadRepository>();
		_handler = new GetRecurringTransactionHandler(recurringTransactionReadRepository: _recurringTransactionReadRepository);
	}

	[Test]
	public async Task Handle_WhenRecurringTransactionExists_ShouldReturnSuccess()
	{
		RecurringTransactionReadModel model = RecurringTransactionFactory.CreateReadModel();
		GetRecurringTransactionQuery query = new GetRecurringTransactionQuery(
			RecurringTransactionId: model.Id,
			UserId: model.UserId
		);

		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: model.Id,
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: model);

		Result<RecurringTransactionReadModel, AppException> result = await _handler.Handle(
			query: query,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: model);
	}

	[Test]
	public async Task Handle_WhenRecurringTransactionNotFound_ShouldReturnNotFound()
	{
		Guid recurringTransactionId = Guid.CreateVersion7();
		GetRecurringTransactionQuery query = new GetRecurringTransactionQuery(
			RecurringTransactionId: recurringTransactionId,
			UserId: Guid.CreateVersion7()
		);

		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: recurringTransactionId,
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (RecurringTransactionReadModel?)null);

		Result<RecurringTransactionReadModel, AppException> result = await _handler.Handle(
			query: query,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}
}
