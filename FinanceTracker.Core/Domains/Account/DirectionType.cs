namespace FinanceTracker.Core.Domains.Account;

/// <summary>Indicates the direction of money flow for a transaction or recurring transaction.</summary>
public enum DirectionType
{
	/// <summary>Money flowing into the account (income, deposit, refund).</summary>
	Credit,

	/// <summary>Money flowing out of the account (expense, withdrawal).</summary>
	Debit
}
