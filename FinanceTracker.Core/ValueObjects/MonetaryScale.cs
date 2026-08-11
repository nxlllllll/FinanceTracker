namespace FinanceTracker.Core.ValueObjects;

/// <summary>
/// How many decimal places the storage schema keeps for monetary values and exchange rates.
/// </summary>
public static class MonetaryScale
{
	/// <summary>Decimal places kept for amounts.</summary>
	public const int Amount = 2;

	/// <summary>Decimal places kept for exchange rates.</summary>
	public const int Rate = 6;

	/// <summary>
	/// True when <paramref name="value"/> survives storage unchanged at the given scale.
	/// </summary>
	public static bool FitsScale(decimal value, int scale)
		=> Decimal.Round(d: value, decimals: scale, mode: MidpointRounding.ToEven) == value;
}
