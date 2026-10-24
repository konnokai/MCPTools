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

        var text = await _tools.SendMessage("Alerts", "hello @everyone", ct: TestContext.Current.CancellationToken);

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

        await _tools.SendMessage("thread", "hi", ct: TestContext.Current.CancellationToken);

        Assert.Equal(
            $"https://discord.com/api/webhooks/456/{SECRET}?thread_id=789&wait=true",
            Assert.Single(_handler.Requests).Uri.ToString());
    }

    [Fact]
    public async Task SendMessage_UnknownWebhook_ListsNames()
    {
        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tools.SendMessage("nope", "hi", ct: TestContext.Current.CancellationToken));

        Assert.Contains("alerts", ex.Message);
        Assert.Empty(_handler.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendMessage_EmptyContent_Throws(string content)
    {
        await Assert.ThrowsAsync<McpException>(
            () => _tools.SendMessage("alerts", content, ct: TestContext.Current.CancellationToken));

        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task SendMessage_2000Chars_IsSent()
    {
        await _tools.SendMessage("alerts", new string('a', 2000), ct: TestContext.Current.CancellationToken);

        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task SendMessage_TooLong_Throws()
    {
        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tools.SendMessage("alerts", new string('a', 2001), ct: TestContext.Current.CancellationToken));

        Assert.Contains("2000", ex.Message);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task SendMessage_RateLimited_SaysHowLongToWait()
    {
        _handler.Respond((HttpStatusCode)429, """{ "message": "You are being rate limited.", "retry_after": 1.5, "global": false }""");

        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tools.SendMessage("alerts", "hi", ct: TestContext.Current.CancellationToken));

        Assert.Contains("1.5", ex.Message);
        Assert.DoesNotContain(SECRET, ex.Message);
    }

    [Fact]
    public async Task SendMessage_DiscordError_PassesMessageWithoutUrl()
    {
        _handler.Respond(HttpStatusCode.NotFound, """{ "message": "Unknown Webhook", "code": 10015 }""");

        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tools.SendMessage("alerts", "hi", ct: TestContext.Current.CancellationToken));

        Assert.Contains("Unknown Webhook", ex.Message);
        Assert.Contains("404", ex.Message);
        Assert.DoesNotContain(SECRET, ex.Message);
    }

    [Fact]
    public async Task SendMessage_NetworkError_HidesUrl()
    {
        _handler.Fail(new HttpRequestException($"connect failed: https://discord.com/api/webhooks/123/{SECRET}"));

        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tools.SendMessage("alerts", "hi", ct: TestContext.Current.CancellationToken));

        Assert.DoesNotContain(SECRET, ex.Message);
    }

    [Fact]
    public async Task SendMessage_EmbedOnly_PostsEmbedWithoutContentOrNulls()
    {
        Embed[] embeds = [new() { Title = "24 小時 AI 圈內新聞摘要｜2026-10-24", Description = "1. **[A](https://a.test)**\n內容" }];

        await _tools.SendMessage("alerts", embeds: embeds, ct: TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(Assert.Single(_handler.Requests).Body);
        Assert.False(body.RootElement.TryGetProperty("content", out _));
        var embed = Assert.Single(body.RootElement.GetProperty("embeds").EnumerateArray());
        Assert.Equal(
            ["title", "description"],
            embed.EnumerateObject().Select(p => p.Name));
        Assert.Equal("24 小時 AI 圈內新聞摘要｜2026-10-24", embed.GetProperty("title").GetString());
        Assert.Empty(body.RootElement.GetProperty("allowed_mentions").GetProperty("parse").EnumerateArray());
    }

    [Fact]
    public async Task SendMessage_EmbedUsesDiscordFieldNames()
    {
        Embed[] embeds =
        [
            new()
            {
                Title = "t",
                Color = 5793266,
                Fields = [new() { Name = "n", Value = "v", Inline = true }],
                Footer = new() { Text = "f", IconUrl = "https://a.test/f.png" },
                Author = new() { Name = "a", IconUrl = "https://a.test/a.png" },
                Image = new() { Url = "https://a.test/i.png" },
                Thumbnail = new() { Url = "https://a.test/t.png" },
                Timestamp = new DateTimeOffset(2026, 10, 24, 8, 0, 0, TimeSpan.FromHours(8))
            }
        ];

        await _tools.SendMessage("alerts", "hi", embeds, ct: TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(Assert.Single(_handler.Requests).Body);
        var embed = body.RootElement.GetProperty("embeds")[0];
        Assert.Equal("hi", body.RootElement.GetProperty("content").GetString());
        Assert.Equal(5793266, embed.GetProperty("color").GetInt32());
        Assert.True(embed.GetProperty("fields")[0].GetProperty("inline").GetBoolean());
        Assert.Equal("https://a.test/f.png", embed.GetProperty("footer").GetProperty("icon_url").GetString());
        Assert.Equal("https://a.test/a.png", embed.GetProperty("author").GetProperty("icon_url").GetString());
        Assert.Equal("https://a.test/i.png", embed.GetProperty("image").GetProperty("url").GetString());
        Assert.Equal("https://a.test/t.png", embed.GetProperty("thumbnail").GetProperty("url").GetString());
        Assert.Equal("2026-10-24T08:00:00+08:00", embed.GetProperty("timestamp").GetString());
    }

    [Fact]
    public async Task SendMessage_NoContentNoEmbeds_Throws()
    {
        await Assert.ThrowsAsync<McpException>(
            () => _tools.SendMessage("alerts", "  ", [], ct: TestContext.Current.CancellationToken));

        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task SendMessage_MoreThan10Embeds_Throws()
    {
        var embeds = Enumerable.Range(0, 11).Select(i => new Embed { Title = $"{i}" }).ToArray();

        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tools.SendMessage("alerts", embeds: embeds, ct: TestContext.Current.CancellationToken));

        Assert.Contains("10", ex.Message);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task SendMessage_EmbedTextOver6000_Throws()
    {
        // description 4000 + field 1500 + footer 250 + author 250 + title 1 = 6001
        Embed[] embeds =
        [
            new() { Description = new string('d', 4000) },
            new()
            {
                Title = "t",
                Fields = [new() { Name = new string('n', 500), Value = new string('v', 1000) }],
                Footer = new() { Text = new string('f', 250) },
                Author = new() { Name = new string('a', 250) }
            }
        ];

        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tools.SendMessage("alerts", embeds: embeds, ct: TestContext.Current.CancellationToken));

        Assert.Contains("6000", ex.Message);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task SendMessage_EmbedTextExactly6000_IsSent()
    {
        Embed[] embeds = [new() { Title = new string('t', 200), Description = new string('d', 4000) }, new() { Description = new string('d', 1800) }];

        await _tools.SendMessage("alerts", embeds: embeds, ct: TestContext.Current.CancellationToken);

        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task SendMessage_BadRequest_PassesFieldErrors()
    {
        _handler.Respond(HttpStatusCode.BadRequest, """
            {
              "code": 50035,
              "message": "Invalid Form Body",
              "errors": {
                "embeds": {
                  "0": {
                    "title": { "_errors": [{ "code": "BASE_TYPE_MAX_LENGTH", "message": "Must be 256 or fewer in length." }] }
                  }
                }
              }
            }
            """);

        var ex = await Assert.ThrowsAsync<McpException>(
            () => _tools.SendMessage("alerts", embeds: [new() { Title = "x" }], ct: TestContext.Current.CancellationToken));

        Assert.Contains("Invalid Form Body", ex.Message);
        Assert.Contains("embeds.0.title: Must be 256 or fewer in length.", ex.Message);
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
