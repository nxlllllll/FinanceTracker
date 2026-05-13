using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Converters.Json;

public sealed class ResultJsonConverterFactory : JsonConverterFactory
{
	public override bool CanConvert(Type typeToConvert)
		=> typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Result<,>);
 
	public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
	{
		Type[] args = typeToConvert.GetGenericArguments();
		Type converterType = typeof(ResultJsonConverter<,>).MakeGenericType(args);
		return (JsonConverter)Activator.CreateInstance(type: converterType)!;
	}
}