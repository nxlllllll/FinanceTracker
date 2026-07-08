using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Tests.Unit.Core.Converters.Json;

public sealed class ResultJsonConverterTests
{
	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		Converters = { new ResultJsonConverter<int, ValidationException>() }
	};

	[Test]
	public async Task Write_WithSuccessResult_ShouldWriteIsSuccessTrueAndValue()
	{
		Result<int, ValidationException> result = Result<int, ValidationException>.Success(value: 42);

		string json = JsonSerializer.Serialize(value: result, options: Options);
		using JsonDocument doc = JsonDocument.Parse(json: json);

		await Assert.That(value: doc.RootElement.GetProperty(propertyName: "IsSuccess").GetBoolean()).IsTrue();
		await Assert.That(value: doc.RootElement.GetProperty(propertyName: "Value").GetInt32()).IsEqualTo(expected: 42);
	}

	[Test]
	public async Task Read_WithSuccessResult_ShouldReconstructValue()
	{
		Result<int, ValidationException> result = JsonSerializer.Deserialize<Result<int, ValidationException>>(
			json: """{"IsSuccess":true,"Value":7}""",
			options: Options
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: 7);
	}

	[Test]
	public async Task Read_WithFailureResult_ShouldThrowJsonException()
	{
		await Assert.That(action: () => JsonSerializer.Deserialize<Result<int, ValidationException>>(
			json: """{"IsSuccess":false,"Value":null}""",
			options: Options
		)).Throws<JsonException>();
	}

	[Test]
	public async Task Read_ThenWrite_ShouldRoundTripSuccessValue()
	{
		Result<int, ValidationException> original = Result<int, ValidationException>.Success(value: 123);

		string json = JsonSerializer.Serialize(value: original, options: Options);
		Result<int, ValidationException> roundTripped = JsonSerializer.Deserialize<Result<int, ValidationException>>(json: json, options: Options);

		await Assert.That(value: roundTripped.IsSuccess).IsTrue();
		await Assert.That(value: roundTripped.Value).IsEqualTo(expected: original.Value);
	}
}
