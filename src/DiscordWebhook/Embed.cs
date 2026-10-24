using System.ComponentModel;
using System.Text.Json.Serialization;

namespace DiscordWebhook;

// 同一組型別給 AI 的 schema 和送給 Discord 的 JSON 共用，所以屬性名稱要跟 Discord API 一樣
[Description("A Discord embed. All properties are optional, but an embed must not be empty.")]
public sealed class Embed
{
    [Description("Up to 256 characters.")]
    public string? Title { get; init; }

    [Description("Up to 4096 characters. Markdown and [text](url) links work.")]
    public string? Description { get; init; }

    [Description("Link opened when the title is clicked.")]
    public string? Url { get; init; }

    [Description("Color of the left bar as an integer, e.g. 5793266 for #5865F2.")]
    public int? Color { get; init; }

    [Description("Up to 25 fields.")]
    public EmbedField[]? Fields { get; init; }

    public EmbedFooter? Footer { get; init; }

    public EmbedAuthor? Author { get; init; }

    public EmbedMedia? Image { get; init; }

    public EmbedMedia? Thumbnail { get; init; }

    [Description("Time shown next to the footer, ISO 8601.")]
    public DateTimeOffset? Timestamp { get; init; }
}

public sealed class EmbedField
{
    [Description("Up to 256 characters.")]
    public required string Name { get; init; }

    [Description("Up to 1024 characters.")]
    public required string Value { get; init; }

    [Description("Show this field on the same line as the fields next to it.")]
    public bool? Inline { get; init; }
}

public sealed class EmbedFooter
{
    [Description("Up to 2048 characters.")]
    public required string Text { get; init; }

    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; init; }
}

public sealed class EmbedAuthor
{
    [Description("Up to 256 characters.")]
    public required string Name { get; init; }

    public string? Url { get; init; }

    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; init; }
}

public sealed class EmbedMedia
{
    public required string Url { get; init; }
}
