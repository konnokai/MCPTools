using System.Text;
using DiscordWebhook;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

WebhookRegistry registry;
try
{
    registry = WebhookRegistry.Parse(Environment.GetEnvironmentVariable(WebhookRegistry.ENV_NAME));
}
catch (FormatException ex)
{
    // 設定錯了就不啟動，不要等到 AI 呼叫工具時才失敗。
    // MCP client 用 UTF-8 讀 stderr，Windows 上 Console.Error 預設是系統代碼頁，中文會變亂碼
    await using var stderr = new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false));
    await stderr.WriteLineAsync(ex.Message);
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);
// stdout 是 MCP 協定用的，log 寫進去會弄壞訊息
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(registry);
// 程式會一直開著，連線定期換掉，Discord 的 DNS 換了才跟得上
builder.Services.AddSingleton(new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10) }));
builder.Services.AddMcpServer()
       .WithStdioServerTransport()
       .WithTools<WebhookTools>();

await builder.Build().RunAsync();
return 0;
