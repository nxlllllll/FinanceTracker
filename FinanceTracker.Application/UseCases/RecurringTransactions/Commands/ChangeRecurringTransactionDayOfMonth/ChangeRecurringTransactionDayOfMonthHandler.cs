using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionDayOfMonth;

public sealed class ChangeRecurringTransactionDayOfMonthHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository
) : IAuthorizedHandler<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeRecurringTransactionDayOfMonthCommand command,
		RecurringTransaction recurringTransaction,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = recurringTransaction.ChangeDayOfMonth(dayOfMonth: command.DayOfMonth);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);
		
		await recurringTransactionWriteRepository.ChangeDayOfMonthAsync(
			recurringTransactionId: command.RecurringTransactionId,
			dayOfMonth: command.DayOfMonth,
			ct: ct
		);
		
		return Result<Guid, DomainException>.Success(value: recurringTransaction.Id);
	}
}
