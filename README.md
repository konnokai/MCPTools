# MCPTools

自己用的 MCP server 合集。給 McpGateway（`E:\Project\Discord\Nadeko-Medusa\McpGateway`）載入，之後也可能包成 Claude Code plugin。

| 工具 | 說明 |
|---|---|
| [DiscordWebhook](src/DiscordWebhook/README.md) | 用設定好的 Discord webhook 發訊息 |

## 結構

```
src/<工具名稱>/       MCP server，一個工具一個專案
tests/<工具名稱>.Tests/
Directory.Build.props  共用設定（net10.0、Nullable）
Directory.Packages.props  套件版本統一寫在這裡
```

## 建置與測試

```powershell
dotnet build MCPTools.slnx -c Release
dotnet test --solution MCPTools.slnx -c Release
```

測試用 xunit v3，跑在 Microsoft.Testing.Platform 上（`global.json` 的 `test.runner`）。所以 `dotnet test` 指定 solution 時要寫 `--solution`。

## 發佈

```powershell
dotnet publish src/DiscordWebhook -c Release -o publish/DiscordWebhook
```

產物是 framework-dependent 的 dll，執行的機器要有 .NET 10 runtime。McpGateway 的映像檔（`aspnet:10.0`）已經有了。

## 授權

[MIT](LICENSE)
