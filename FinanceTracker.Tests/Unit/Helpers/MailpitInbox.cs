using System.Net.Http.Json;
using System.Text.Json;

namespace FinanceTracker.Tests.Unit.Helpers;

public sealed class MailpitInbox(HttpClient http)
{
	public sealed record ReceivedMail(string Subject, string Text);

	private sealed record SearchResult(List<MessageSummary> Messages);

	private sealed record MessageSummary(string Id);

	private sealed record Message(string Subject, string Text);

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
	
	public async Task<List<ReceivedMail>> GetMailToAsync(string address)
	{
		string query = Uri.EscapeDataString(stringToEscape: $"to:\"{address}\"");
		SearchResult search = (await http.GetFromJsonAsync<SearchResult>(requestUri: $"/api/v1/search?query={query}", options: JsonOptions))!;

		List<ReceivedMail> mail = [];

		foreach (MessageSummary summary in search.Messages)
		{
			Message message = (await http.GetFromJsonAsync<Message>(requestUri: $"/api/v1/message/{summary.Id}", options: JsonOptions))!;
			mail.Add(item: new ReceivedMail(Subject: message.Subject, Text: message.Text));
		}

		return mail;
	}
}
