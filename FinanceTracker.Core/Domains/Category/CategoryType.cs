namespace FinanceTracker.Core.Domains.Category;

/// <summary>Classifies a category as either income or expense.</summary>
public enum CategoryType
{
	/// <summary>Category used for income transactions (salary, freelance, etc.).</summary>
	Income,

	/// <summary>Category used for expense transactions (food, transport, etc.).</summary>
	Expense
}