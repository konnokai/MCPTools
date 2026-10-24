using DiscordWebhook;

namespace DiscordWebhook.Tests;

public class WebhookRegistryTests
{
    [Fact]
    public void Parse_ReadsAllWebhooks_SortedByName()
    {
        var registry = WebhookRegistry.Parse("""
            {
              "dev-log": "https://discord.com/api/webhooks/456/def?thread_id=789",
              "alerts": "https://discordapp.com/api/webhooks/123/abc"
            }
            """);

        Assert.Equal(["alerts", "dev-log"], registry.Names);
        Assert.True(registry.TryGet("dev-log", out var url));
        Assert.Equal("https://discord.com/api/webhooks/456/def?thread_id=789", url.ToString());
    }

    [Fact]
    public void TryGet_IgnoresCase()
    {
        var registry = WebhookRegistry.Parse("""{ "Alerts": "https://discord.com/api/webhooks/1/a" }""");

        Assert.True(registry.TryGet("alerts", out _));
        Assert.False(registry.TryGet("other", out _));
    }

    [Theory]
    [InlineData("https://ptb.discord.com/api/webhooks/1/a")]
    [InlineData("https://canary.discord.com/api/webhooks/1/a")]
    public void Parse_AcceptsDiscordClientHosts(string url)
    {
        var registry = WebhookRegistry.Parse($$"""{ "a": "{{url}}" }""");

        Assert.Equal(["a"], registry.Names);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_Missing_Throws(string? json)
    {
        var ex = Assert.Throws<FormatException>(() => WebhookRegistry.Parse(json));
        Assert.Contains("DISCORD_WEBHOOKS", ex.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[\"https://discord.com/api/webhooks/1/a\"]")]
    [InlineData("{ \"a\": 1 }")]
    [InlineData("{}")]
    [InlineData("{ \"\": \"https://discord.com/api/webhooks/1/a\" }")]
    [InlineData("{ \"a\": \"https://discord.com/api/webhooks/1/a\", \"A\": \"https://discord.com/api/webhooks/2/b\" }")]
    public void Parse_BadShape_Throws(string json)
    {
        Assert.Throws<FormatException>(() => WebhookRegistry.Parse(json));
    }

    [Theory]
    [InlineData("http://discord.com/api/webhooks/1/a")]
    [InlineData("https://example.com/api/webhooks/1/a")]
    [InlineData("https://discord.com.evil.test/api/webhooks/1/a")]
    [InlineData("https://discord.com/api/channels/1")]
    [InlineData("discord.com/api/webhooks/1/a")]
    public void Parse_BadUrl_ThrowsWithNameButNotUrl(string url)
    {
        var ex = Assert.Throws<FormatException>(() => WebhookRegistry.Parse($$"""{ "alerts": "{{url}}" }"""));

        Assert.Contains("alerts", ex.Message);
        Assert.DoesNotContain(url, ex.Message);
    }
}
