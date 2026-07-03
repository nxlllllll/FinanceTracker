using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;

namespace FinanceTracker.Application.UseCases.Transaction.Utilities;

public static class CategoryDirectionValidator
{
	public static DomainException? Validate(
		CategoryReadModel category,
		DirectionType? direction)
	{
		bool valid = (direction, category.Type) switch
		{
			(DirectionType.Debit, CategoryType.Expense) => true,
			(DirectionType.Credit, CategoryType.Income) => true,
			_ => false
		};

		if (!valid)
			return new InvalidTransactionDirectionException(message: $"Direction '{direction}' is not compatible with category type '{category.Type}'.");

		return null;
	}
}
