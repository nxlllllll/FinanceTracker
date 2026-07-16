using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Tests.Unit.Core.Converters.Json;

public sealed class ResultJsonConverterFactoryTests
{
	private readonly ResultJsonConverterFactory _factory = new ResultJsonConverterFactory();

	[Test]
	public async Task CanConvert_WithResultType_ShouldReturnTrue()
		=> await Assert.That(value: _factory.CanConvert(typeToConvert: typeof(Result<int, ValidationException>))).IsTrue();

	[Test]
	public async Task CanConvert_WithNonGenericType_ShouldReturnFalse()
		=> await Assert.That(value: _factory.CanConvert(typeToConvert: typeof(int))).IsFalse();

	[Test]
	public async Task CanConvert_WithUnrelatedGenericType_ShouldReturnFalse()
		=> await Assert.That(value: _factory.CanConvert(typeToConvert: typeof(List<int>))).IsFalse();

	[Test]
	public async Task CreateConverter_ShouldReturnConverterOfCorrectGenericType()
	{
		JsonConverter converter = _factory.CreateConverter(
			typeToConvert: typeof(Result<int, ValidationException>),
			options: new JsonSerializerOptions()
		);

		await Assert.That(value: converter).IsTypeOf<ResultJsonConverter<int, ValidationException>>();
	}

	[Test]
	public async Task CreateConverter_ShouldProduceAFunctioningConverter()
	{
		JsonSerializerOptions options = new JsonSerializerOptions { Converters = { _factory } };
		Result<int, ValidationException> value = Result<int, ValidationException>.Success(value: 5);

		string json = JsonSerializer.Serialize(value: value, options: options);
		Result<int, ValidationException> roundTripped = JsonSerializer.Deserialize<Result<int, ValidationException>>(json: json, options: options);

		await Assert.That(value: roundTripped.IsSuccess).IsTrue();
		await Assert.That(value: roundTripped.Value).IsEqualTo(expected: value.Value);
	}
}
