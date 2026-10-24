using System.Text.Json;

namespace DiscordWebhook;

public sealed class WebhookRegistry
{
    public const string ENV_NAME = "DISCORD_WEBHOOKS";

    private static readonly string[] _hosts = ["discord.com", "discordapp.com", "ptb.discord.com", "canary.discord.com"];

    private readonly Dictionary<string, Uri> _urls;

    private WebhookRegistry(Dictionary<string, Uri> urls)
    {
        _urls = urls;
        Names = urls.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<string> Names { get; }

    public static WebhookRegistry Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new FormatException($"{ENV_NAME} 沒有設定。格式是 JSON 物件：{{\"名稱\": \"webhook 網址\"}}");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"{ENV_NAME} 不是合法的 JSON：{ex.Message}");
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new FormatException($"{ENV_NAME} 要是 JSON 物件，key 是名稱，value 是 webhook 網址");

            var urls = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(prop.Name))
                    throw new FormatException($"{ENV_NAME} 裡有空白的名稱");
                if (prop.Value.ValueKind != JsonValueKind.String)
                    throw new FormatException($"{ENV_NAME} 的「{prop.Name}」不是字串");
                if (!urls.TryAdd(prop.Name, ToWebhookUrl(prop.Name, prop.Value.GetString()!)))
                    throw new FormatException($"{ENV_NAME} 的「{prop.Name}」重複了（名稱不分大小寫）");
            }

            if (urls.Count == 0)
                throw new FormatException($"{ENV_NAME} 至少要有一個 webhook");

            return new WebhookRegistry(urls);
        }
    }

    public bool TryGet(string name, out Uri url)
        => _urls.TryGetValue(name, out url!);

    private static Uri ToWebhookUrl(string name, string value)
    {
        // 錯誤訊息只寫名稱：網址裡有 token，這段訊息會出現在 gateway 的 log
        if (!Uri.TryCreate(value, UriKind.Absolute, out var url)
            || url.Scheme != Uri.UriSchemeHttps
            || !_hosts.Contains(url.Host, StringComparer.OrdinalIgnoreCase)
            || !url.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.Ordinal))
            throw new FormatException($"{ENV_NAME} 的「{name}」不是 Discord webhook 網址，要是 https://discord.com/api/webhooks/ 開頭");

        return url;
    }
}
