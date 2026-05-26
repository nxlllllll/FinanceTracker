using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceTracker.Infrastructure.Database.Converters;

public sealed class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
	convertToProviderExpression: value => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()),
	convertFromProviderExpression: value => _fromDb[value]
)
	where TEnum : struct, Enum
{
	private static readonly Dictionary<string, TEnum> _fromDb = Enum.GetValues<TEnum>().ToDictionary(
		keySelector: e => JsonNamingPolicy.SnakeCaseLower.ConvertName(name: e.ToString()),
		elementSelector: e => e
	);
}
