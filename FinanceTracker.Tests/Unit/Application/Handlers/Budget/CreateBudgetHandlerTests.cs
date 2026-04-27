using FinanceTracker.Application.Budgets.Commands.CreateBudget;
using FinanceTracker.Core.Repositories.Budget;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class CreateBudgetHandlerTests
{
	private IBudgetWriteRepository _budgetWriteRepository = null!;
	private CreateBudgetHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();
		
		_handler = new CreateBudgetHandler(budgetWriteRepository: _budgetWriteRepository);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldReturnBudgetId()
	{
		CreateBudgetCommand command = new CreateBudgetCommand(
			UserId: Guid.NewGuid(),
			CategoryId: Guid.NewGuid(),
			Currency: "RUB",
			Amount: 10000m,
			From: new DateOnly(year: 2025, month: 1, day: 1),
			To: new DateOnly(year: 2025, month: 1, day: 31)
		);

		Guid result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result).IsNotEqualTo(notExpected: Guid.Empty);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldCallCreateAsync()
	{
		CreateBudgetCommand command = new CreateBudgetCommand(
			UserId: Guid.NewGuid(),
			CategoryId: Guid.NewGuid(),
			Currency: "RUB",
			Amount: 10000m,
			From: new DateOnly(year: 2025, month: 1, day: 1),
			To: new DateOnly(year: 2025, month: 1, day: 31)
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _budgetWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			budgetId: Arg.Any<Guid>(),
			userId: command.UserId,
			categoryId: command.CategoryId,
			currency: command.Currency,
			amount: command.Amount,
			from: command.From,
			to: command.To,
			ct: Arg.Any<CancellationToken>()
		);
	}
}