using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Api.Http;

/// <summary>
/// Reads an enum out of a query string, accepting whatever casing the caller sent.
/// </summary>
public static class EnumQuery
{
	/// <summary>
	/// Parses an optional filter value.
	/// </summary>
	public static Result<TEnum?, ValidationException> ParseOptional<TEnum>(
		string? value,
		string parameterName
	) where TEnum : struct, Enum
	{
		if (String.IsNullOrWhiteSpace(value: value))
			return Result<TEnum?, ValidationException>.Success(value: null);

		if (Enum.TryParse(value: value, ignoreCase: true, result: out TEnum parsed) && Enum.IsDefined(value: parsed))
			return Result<TEnum?, ValidationException>.Success(value: parsed);

		return Result<TEnum?, ValidationException>.Failure(error: new ValidationException(errors: new Dictionary<string, string[]>
		{
			[parameterName] = [$"'{value}' is not a valid value. Expected one of: {String.Join(separator: ", ", values: Enum.GetNames<TEnum>().Select(selector: name => name.ToLowerInvariant()))}."]
		}));
	}
}
