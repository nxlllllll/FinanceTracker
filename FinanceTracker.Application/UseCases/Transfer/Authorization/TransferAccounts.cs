namespace FinanceTracker.Application.UseCases.Transfer.Authorization;

/// <summary>
/// Carries both sides of a transfer after <see cref="TransferLoader"/> has loaded and
/// authorized them, so the currency for both accounts always comes from the persisted
/// entities — never from client-supplied command fields.
/// </summary>
public sealed record TransferAccounts(
	Core.Domains.Account.Account FromAccount,
	Core.ValueObjects.Currency ToAccountCurrency
);
