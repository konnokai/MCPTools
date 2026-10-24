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

```bash
./publish.sh [輸出資料夾]
```

- 把 `src/` 底下每個工具發佈到 `<輸出資料夾>/<工具名稱>/`。沒給輸出資料夾時用 repo 裡的 `publish/`。
- 給 McpGateway 用時，輸出到 bot 的 `docker-compose.yml` 旁邊的 `mcp-tools/`，例如 `./publish.sh /root/bot/nadeko-docker/mcp-tools`。gateway 把它掛載成 `/app/tools`。
- 會先發佈到暫存資料夾，成功後才換掉舊的。發佈失敗時，舊版本不會被動到。
- gateway 正在跑的工具還是舊版。跑完後要 `docker compose restart mcp-gateway`。
- 要有 .NET 10 SDK。Windows 上可以用 Git Bash 跑。

產物是 framework-dependent 的 dll，執行的機器要有 .NET 10 runtime。McpGateway 的映像檔（`aspnet:10.0`）已經有了。

## 授權

[MIT](LICENSE)
