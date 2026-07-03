namespace FinanceTracker.Core.Domains.Account;

/// <summary>Classifies the purpose and behaviour of an account.</summary>
public enum AccountType
{
	/// <summary>Standard current/debit account for everyday transactions.</summary>
	Checking,

	/// <summary>Savings account, typically with restricted withdrawals.</summary>
	Savings,

	/// <summary>Physical cash — not linked to a bank.</summary>
	Cash
}
