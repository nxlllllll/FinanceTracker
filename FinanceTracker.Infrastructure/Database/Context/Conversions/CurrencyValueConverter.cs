using FinanceTracker.Core.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceTracker.Infrastructure.Database.Context.Conversions;

/// <summary>
/// Converts <see cref="Currency"/> to its ISO code and back.
/// </summary>
public sealed class CurrencyValueConverter() : ValueConverter<Core.ValueObjects.Currency, string>(
	convertToProviderExpression: currency => currency.Value,
	convertFromProviderExpression: value => Core.ValueObjects.Currency.Reconstitute(value: value)
);
