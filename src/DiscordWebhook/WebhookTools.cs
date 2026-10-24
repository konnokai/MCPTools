using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace DiscordWebhook;

[McpServerToolType]
public sealed class WebhookTools(WebhookRegistry registry, HttpClient http)
{
    private const int MAX_CONTENT_LENGTH = 2000;
    private const int MAX_EMBEDS = 10;
    private const int MAX_EMBED_TEXT = 6000;

    // 沒填的欄位不要送 null 給 Discord
    private static readonly JsonSerializerOptions _discordJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool(Name = "list_webhooks", ReadOnly = true, OpenWorld = false)]
    [Description("List the names of the Discord webhooks that send_message can post to.")]
    public string ListWebhooks()
        => "Available webhooks: " + string.Join(", ", registry.Names);

    [McpServerTool(Name = "send_message", Destructive = false, OpenWorld = true)]
    [Description("Post a message to a Discord channel through a configured webhook. Give content, embeds, or both. "
                 + "Mentions such as @everyone or <@id> are shown but never notify anyone.")]
    public async Task<string> SendMessage(
        [Description("Webhook name, as returned by list_webhooks.")] string webhook,
        [Description("Message text, up to 2000 characters. Discord markdown works. Can be left out when embeds are given.")]
        string? content = null,
        [Description("Up to 10 embeds. Title, description, field, footer and author text of all embeds together must not exceed 6000 characters.")]
        Embed[]? embeds = null,
        CancellationToken ct = default)
    {
        if (!registry.TryGet(webhook, out var url))
            throw new McpException($"Unknown webhook '{webhook}'. Available webhooks: {string.Join(", ", registry.Names)}.");

        content = string.IsNullOrWhiteSpace(content) ? null : content;
        embeds = embeds is { Length: > 0 } ? embeds : null;
        if (content is null && embeds is null)
            throw new McpException("Nothing to send. Give content, embeds, or both.");
        if (content?.Length > MAX_CONTENT_LENGTH)
            throw new McpException(
                $"content has {content.Length} characters, Discord allows {MAX_CONTENT_LENGTH}. Shorten it or send several messages.");
        if (embeds?.Length > MAX_EMBEDS)
            throw new McpException($"{embeds.Length} embeds given, Discord allows {MAX_EMBEDS} per message. Send several messages.");

        var embedText = embeds?.Sum(EmbedTextLength) ?? 0;
        if (embedText > MAX_EMBED_TEXT)
            throw new McpException(
                $"The embeds have {embedText} characters of text in total, Discord allows {MAX_EMBED_TEXT}. Shorten them or send several messages.");

        HttpResponseMessage response;
        try
        {
            // parse 留空：AI 寫的內容可能被別人塞了 @everyone，不能讓它真的通知
            response = await http.PostAsJsonAsync(
                WithWait(url),
                new { content, embeds, allowed_mentions = new { parse = Array.Empty<string>() } },
                _discordJson,
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
            var details = root?.TryGetProperty("errors", out var errors) == true ? FlattenErrors(errors, "").ToList() : [];
            throw new McpException(details.Count == 0
                ? $"Discord returned {(int)response.StatusCode}: {message}"
                : $"Discord returned {(int)response.StatusCode}: {message} ({string.Join("; ", details)})");
        }
    }

    // Discord 用這幾個欄位算 6000 字的上限
    private static int EmbedTextLength(Embed embed)
        => (embed.Title?.Length ?? 0)
           + (embed.Description?.Length ?? 0)
           + (embed.Fields?.Sum(f => f.Name.Length + f.Value.Length) ?? 0)
           + (embed.Footer?.Text.Length ?? 0)
           + (embed.Author?.Name.Length ?? 0);

    // 400 的 errors 照欄位路徑一層層包起來，最底下的 _errors 才是訊息。攤平成「embeds.0.title: ...」，AI 才知道要改哪裡
    private static IEnumerable<string> FlattenErrors(JsonElement node, string path)
    {
        if (node.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var prop in node.EnumerateObject())
        {
            if (prop.Name == "_errors" && prop.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var error in prop.Value.EnumerateArray())
                {
                    if (error.TryGetProperty("message", out var text))
                        yield return $"{path}: {text.GetString()}";
                }
            }
            else
            {
                foreach (var line in FlattenErrors(prop.Value, path.Length == 0 ? prop.Name : $"{path}.{prop.Name}"))
                    yield return line;
            }
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
