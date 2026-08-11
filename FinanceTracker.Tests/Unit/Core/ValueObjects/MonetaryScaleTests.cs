using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Core.ValueObjects;

public sealed class MonetaryScaleTests
{
	[Test]
	[Arguments(0)]
	[Arguments(100)]
	[Arguments(100.5)]
	[Arguments(100.55)]
	[Arguments(-100.55)]
	[Arguments(0.01)]
	public async Task FitsScale_WithTwoDecimalsOrFewer_ShouldAccept(decimal amount)
		=> await Assert.That(value: MonetaryScale.FitsScale(value: amount, scale: MonetaryScale.Amount)).IsTrue();

	[Test]
	[Arguments(100.555)]
	[Arguments(100.123456)]
	[Arguments(0.001)]
	[Arguments(-0.001)]
	public async Task FitsScale_WithMoreThanTwoDecimals_ShouldReject(decimal amount)
		=> await Assert.That(value: MonetaryScale.FitsScale(value: amount, scale: MonetaryScale.Amount)).IsFalse();

	[Test]
	public async Task FitsScale_WithTrailingZeros_ShouldAccept()
	{
		await Assert.That(value: MonetaryScale.FitsScale(value: 100.5000m, scale: MonetaryScale.Amount)).IsTrue();
	}

	[Test]
	[Arguments(1.123456)]
	[Arguments(0.000001)]
	public async Task FitsScale_AtRateScale_ShouldAcceptSixDecimals(decimal rate)
		=> await Assert.That(value: MonetaryScale.FitsScale(value: rate, scale: MonetaryScale.Rate)).IsTrue();

	[Test]
	public async Task FitsScale_AtRateScale_ShouldRejectSevenDecimals()
		=> await Assert.That(value: MonetaryScale.FitsScale(value: 1.1234567m, scale: MonetaryScale.Rate)).IsFalse();

	[Test]
	public async Task Scales_ShouldMatchTheStorageSchema()
	{
#pragma warning disable TUnitAssertions0005
		await Assert.That(value: MonetaryScale.Amount).IsEqualTo(expected: 2);
		await Assert.That(value: MonetaryScale.Rate).IsEqualTo(expected: 6);
#pragma warning restore TUnitAssertions0005
	}
}
