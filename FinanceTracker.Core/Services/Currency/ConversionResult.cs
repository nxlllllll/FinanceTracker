using FinanceTracker.Core.Domains.Abstractions.Rate;

namespace FinanceTracker.Core.Services.Currency;

/// <summary>
/// Result of a currency conversion rate lookup: the rate to use, and how much to trust it.
/// </summary>
public sealed record ConversionResult(
	decimal Rate,
	RateStatus Status
);
