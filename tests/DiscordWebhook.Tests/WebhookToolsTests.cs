using System.Net;
using System.Text;
using System.Text.Json;
using DiscordWebhook;
using ModelContextProtocol;

namespace DiscordWebhook.Tests;

public class WebhookToolsTests
{
    private const string SECRET = "s3cr3t-token";

    private readonly FakeHandler _handler = new();
    private readonly WebhookTools _tools;

    public WebhookToolsTests()
    {
        var registry = WebhookRegistry.Parse($$"""
            {
              "alerts": "https://discord.com/api/webhooks/123/{{SECRET}}",
              "thread": "https://discord.com/api/webhooks/456/{{SECRET}}?thread_id=789"
            }
            """);
        _tools = new WebhookTools(registry, new HttpClient(_handler));
    }

    [Fact]
    public void ListWebhooks_ShowsNamesWithoutUrls()
    {
        var text = _tools.ListWebhooks();

        Assert.Contains("alerts", text);
        Assert.Contains("thread", text);
        Assert.DoesNotContain(SECRET, text);
    }

    [Fact]
    public async Task SendMessage_PostsContentWithoutMentions()
    {
        _handler.Respond(HttpStatusCode.OK, """{ "id": "1111" }""");

        var text = await _tools.SendMessage("Alerts", "hello @everyone", TestContext.Current.CancellationToken);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"https://discord.com/api/webhooks/123/{SECRET}?wait=true", request.Uri.ToString());

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("hello @everyone", body.RootElement.GetProperty("content").GetString());
        Assert.Empty(body.RootElement.GetProperty("allowed_mentions").GetProperty("parse").EnumerateArray());
        Assert.False(body.RootElement.TryGetProperty("username", out _));
        Assert.False(body.RootElement.TryGetProperty("avatar_url", out _));

        Assert.Contains("1111", text);
    }

    [Fact]
    public async Task SendMessage_KeepsThreadId()
    {
        _handler.Respond(HttpStatusCode.OK, """{ "id": "2222" }""");

        await _tools.SendMessage("thread", "hi", TestContext.Current.CancellationToken);

        Assert.Equal(
            $"https://discord.com/api/webhooks/456/{SECRET}?thread_id=789&wait=true",
            Assert.Single(_handler.Requests).Uri.ToString());
    }

    [Fact]
    public async Task SendMessage_UnknownWebhook_ListsNames()
    {
        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tools.SendMessage("nope", "hi", TestContext.Current.CancellationToken));

        Assert.Contains("alerts", ex.Message);
        Assert.Empty(_handler.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendMessage_EmptyContent_Throws(string content)
    {
        await Assert.ThrowsAsync<McpException>(
            () => _tools.SendMessage("alerts", content, TestContext.Current.CancellationToken));

        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task SendMessage_2000Chars_IsSent()
    {
        await _tools.SendMessage("alerts", new string('a', 2000), TestContext.Current.CancellationToken);

        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task SendMessage_TooLong_Throws()
    {
        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tools.SendMessage("alerts", new string('a', 2001), TestContext.Current.CancellationToken));

        Assert.Contains("2000", ex.Message);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task SendMessage_RateLimited_SaysHowLongToWait()
    {
        _handler.Respond((HttpStatusCode)429, """{ "message": "You are being rate limited.", "retry_after": 1.5, "global": false }""");

        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tools.SendMessage("alerts", "hi", TestContext.Current.CancellationToken));

        Assert.Contains("1.5", ex.Message);
        Assert.DoesNotContain(SECRET, ex.Message);
    }

    [Fact]
    public async Task SendMessage_DiscordError_PassesMessageWithoutUrl()
    {
        _handler.Respond(HttpStatusCode.NotFound, """{ "message": "Unknown Webhook", "code": 10015 }""");

        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tools.SendMessage("alerts", "hi", TestContext.Current.CancellationToken));

        Assert.Contains("Unknown Webhook", ex.Message);
        Assert.Contains("404", ex.Message);
        Assert.DoesNotContain(SECRET, ex.Message);
    }

    [Fact]
    public async Task SendMessage_NetworkError_HidesUrl()
    {
        _handler.Fail(new HttpRequestException($"connect failed: https://discord.com/api/webhooks/123/{SECRET}"));

        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tools.SendMessage("alerts", "hi", TestContext.Current.CancellationToken));

        Assert.DoesNotContain(SECRET, ex.Message);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _body = """{ "id": "0" }""";
        private Exception? _error;

        public List<(HttpMethod Method, Uri Uri, string Body)> Requests { get; } = [];

        public void Respond(HttpStatusCode status, string body)
            => (_status, _body) = (status, body);

        public void Fail(Exception error)
            => _error = error;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.Method, request.RequestUri!, body));

            if (_error is not null)
                throw _error;

            return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
        }
    }
}
