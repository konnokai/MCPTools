using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace DiscordWebhook;

[McpServerToolType]
public sealed class WebhookTools(WebhookRegistry registry, HttpClient http)
{
    private const int MAX_CONTENT_LENGTH = 2000;

    [McpServerTool(Name = "list_webhooks", ReadOnly = true, OpenWorld = false)]
    [Description("List the names of the Discord webhooks that send_message can post to.")]
    public string ListWebhooks()
        => "Available webhooks: " + string.Join(", ", registry.Names);

    [McpServerTool(Name = "send_message", Destructive = false, OpenWorld = true)]
    [Description("Post a text message to a Discord channel through a configured webhook. "
                 + "Mentions such as @everyone or <@id> are shown but never notify anyone.")]
    public async Task<string> SendMessage(
        [Description("Webhook name, as returned by list_webhooks.")] string webhook,
        [Description("Message text, 1 to 2000 characters. Discord markdown works.")] string content,
        CancellationToken ct)
    {
        if (!registry.TryGet(webhook, out var url))
            throw new McpException($"Unknown webhook '{webhook}'. Available webhooks: {string.Join(", ", registry.Names)}.");
        if (string.IsNullOrWhiteSpace(content))
            throw new McpException("content is empty.");
        if (content.Length > MAX_CONTENT_LENGTH)
            throw new McpException(
                $"content has {content.Length} characters, Discord allows {MAX_CONTENT_LENGTH}. Shorten it or send several messages.");

        HttpResponseMessage response;
        try
        {
            // parse 留空：AI 寫的內容可能被別人塞了 @everyone，不能讓它真的通知
            response = await http.PostAsJsonAsync(
                WithWait(url),
                new { content, allowed_mentions = new { parse = Array.Empty<string>() } },
                ct);
        }
        catch (HttpRequestException ex)
        {
            // 例外訊息可能帶著網址，網址裡有 token
            throw new McpException($"Could not reach Discord ({ex.HttpRequestError}).");
        }

        using (response)
        using (var body = await ReadJsonAsync(response, ct))
        {
            var root = body?.RootElement;
            if (response.IsSuccessStatusCode)
            {
                var id = root?.TryGetProperty("id", out var idProp) == true ? idProp.GetString() : null;
                return $"Sent to '{webhook}'. Message ID: {id ?? "unknown"}.";
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var wait = root?.TryGetProperty("retry_after", out var retry) == true ? $"{retry.GetRawText()} seconds" : "a moment";
                throw new McpException($"Discord rate limit reached. Try again after {wait}.");
            }

            var message = root?.TryGetProperty("message", out var msg) == true ? msg.GetString() : response.ReasonPhrase;
            throw new McpException($"Discord returned {(int)response.StatusCode}: {message}");
        }
    }

    // wait=true 讓 Discord 等訊息建好才回應，才拿得到訊息 ID 和錯誤內容
    private static Uri WithWait(Uri url)
    {
        var builder = new UriBuilder(url);
        builder.Query = builder.Query.Length > 1 ? builder.Query[1..] + "&wait=true" : "wait=true";
        return builder.Uri;
    }

    private static async Task<JsonDocument?> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (JsonException)
        {
            // Cloudflare 擋下來時會回 HTML，不是 JSON
            return null;
        }
    }
}
