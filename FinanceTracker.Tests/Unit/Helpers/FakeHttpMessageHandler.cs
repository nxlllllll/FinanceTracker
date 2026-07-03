using System.Net;

namespace FinanceTracker.Tests.Unit.Helpers;

/// <summary>
/// Fake HttpMessageHandler — возвращает настроенные ответы для каждого base currency code.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
	private readonly Dictionary<string, Queue<HttpResponseMessage>> _responses = new Dictionary<string, Queue<HttpResponseMessage>>();
	private int _callCount;

	public int CallCount => _callCount;

	private readonly Dictionary<string, int> _callsPerCode = new Dictionary<string, int>();

	public void SetupResponse(string baseCode, string json)
	{
		Enqueue(baseCode: baseCode, response: new HttpResponseMessage(statusCode: HttpStatusCode.OK)
		{
			Content = new StringContent(content: json, encoding: System.Text.Encoding.UTF8, mediaType: "application/json")
		});
	}

	public void SetupError(string baseCode, HttpStatusCode statusCode)
	{
		for (int i = 0; i < 5; i++)
			Enqueue(baseCode: baseCode, response: new HttpResponseMessage(statusCode: statusCode));
	}

	public void SetupTransientError(string baseCode, int failCount, string successJson)
	{
		for (int i = 0; i < failCount; i++)
			Enqueue(baseCode: baseCode, response: new HttpResponseMessage(statusCode: HttpStatusCode.InternalServerError));

		Enqueue(baseCode: baseCode, response: new HttpResponseMessage(statusCode: HttpStatusCode.OK)
		{
			Content = new StringContent(content: successJson, encoding: System.Text.Encoding.UTF8, mediaType: "application/json")
		});
	}

	private void Enqueue(string baseCode, HttpResponseMessage response)
	{
		if (!_responses.ContainsKey(key: baseCode))
			_responses[baseCode] = new Queue<HttpResponseMessage>();

		_responses[baseCode].Enqueue(item: response);
	}

	protected override Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		Interlocked.Increment(location: ref _callCount);

		string url = request.RequestUri?.ToString() ?? String.Empty;
		string baseCode = ExtractBaseCode(url: url);

		_callsPerCode[baseCode] = _callsPerCode.TryGetValue(key: baseCode, out int count) ? count + 1 : 1;

		if (_responses.TryGetValue(key: baseCode, out Queue<HttpResponseMessage>? queue) && queue.Count > 0)
			return Task.FromResult(result: queue.Dequeue());

		return Task.FromResult(result: new HttpResponseMessage(statusCode: HttpStatusCode.ServiceUnavailable));
	}

	private static string ExtractBaseCode(string url)
	{
		// URL: https://fake-exchange.test/test-key/latest/{BASE_CODE}
		string[] parts = url.TrimEnd(trimChar: '/').Split(separator: '/');
		return parts.LastOrDefault() ?? String.Empty;
	}
}
