# MCPTools

MCP server 合集。每個工具是 `src/<名稱>/` 下的一個 console 專案，測試在 `tests/<名稱>.Tests/`。

## 慣例

- 全部 `net10.0`，寫在 `Directory.Build.props`。McpGateway 的容器只有 .NET 10 runtime，target 更新的版本會跑不起來。
- 套件版本只寫在 `Directory.Packages.props`。`ModelContextProtocol` 跟 McpGateway 用同一版。
- 只走 stdio。stdout 是 MCP 協定用的，log 一律寫到 stderr（`LogToStandardErrorThreshold = LogLevel.Trace`）。
- 設定從環境變數讀。McpGateway 只把 `mcp.json` 裡 `env` 寫的變數傳給子程序。設定缺了或格式錯，啟動時就印錯誤到 stderr 並結束，不要等到工具被呼叫才失敗。
- 給 AI 看的錯誤用 `McpException` 丟，訊息要讓 AI 知道怎麼改。訊息裡不能有密鑰（例如 webhook 網址裡的 token）。
- 工具說明（`[Description]`）用英文。程式碼註解用中文，只寫為什麼。

## 驗證

- `dotnet test --solution MCPTools.slnx -c Release`（Microsoft.Testing.Platform 模式，不能省略 `--solution`）
- 本機沒有 Docker。要在 McpGateway 容器裡測，用 SSH MCP 的 `debian-test-account`。那台是 `MainServer`，上面有正式服務在跑：用別的名稱建映像檔和容器，不開 port，測完刪掉。
