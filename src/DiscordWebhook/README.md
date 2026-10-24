# DiscordWebhook

讓 AI 用事先設定好的 Discord webhook 發訊息。AI 只看得到 webhook 的名稱，看不到網址。

## 設定

環境變數 `DISCORD_WEBHOOKS`，內容是 JSON 物件：key 是名稱，value 是 webhook 網址。

```json
{
  "alerts": "https://discord.com/api/webhooks/123/abc",
  "dev-log": "https://discord.com/api/webhooks/456/def?thread_id=789"
}
```

- 至少要有一個。名稱不分大小寫，不能重複。
- 網址要是 `https`，主機是 `discord.com`、`discordapp.com`、`ptb.discord.com` 或 `canary.discord.com`，路徑是 `/api/webhooks/` 開頭。
- 網址可以帶 `?thread_id=`，訊息會發到那個討論串。
- 沒設定或格式錯時，程式把錯誤印到 stderr 後就結束。McpGateway 的 `.mcpstatus` 只會顯示 `The transport was closed.`，錯誤原因要看 `docker logs mcp-gateway`，找 `received stderr log`。

## 工具

| 工具 | 說明 |
|---|---|
| `list_webhooks` | 列出可以用的名稱 |
| `send_message` | 參數 `webhook`（名稱）和 `content`（1 到 2000 個字元）。成功時回傳訊息 ID |

- 不設定 username 和 avatar。訊息用 webhook 在 Discord 上設定的名稱和頭像。
- 不會通知任何人。送出時 `allowed_mentions` 是空的，`@everyone`、`@here`、`<@使用者 ID>` 會照樣顯示，但不會 ping。
- 被 Discord 限速時回錯誤，附上要等幾秒，不會自己重試。
- 錯誤訊息不含網址，因為網址裡有 webhook 的 token。

## 給 McpGateway 用

1. 發佈：

   ```powershell
   dotnet publish src/DiscordWebhook -c Release -o publish/DiscordWebhook
   ```

2. 把 `publish/DiscordWebhook/` 複製到伺服器，放在 bot 的 `docker-compose.yml` 旁邊的 `mcp-tools/DiscordWebhook/`。

3. `.env` 加上設定。用單引號包住，JSON 裡的雙引號才不會被拿掉：

   ```
   DISCORD_WEBHOOKS='{"alerts":"https://discord.com/api/webhooks/123/abc"}'
   ```

4. `docker-compose.yml` 的 `mcp-gateway` 加上：

   ```yaml
   environment:
     DISCORD_WEBHOOKS: ${DISCORD_WEBHOOKS}
   volumes:
     - ${PWD}/mcp-tools:/app/tools:ro
   ```

5. `mcp.json` 加上：

   ```json
   "discord-webhook": {
     "command": "dotnet",
     "args": ["/app/tools/DiscordWebhook/DiscordWebhook.dll"],
     "env": { "DISCORD_WEBHOOKS": "${DISCORD_WEBHOOKS}" },
     "dataTool": false
   }
   ```

   - `env` 一定要寫。gateway 不會把自己的環境變數傳給 MCP server。
   - 沒寫 `requiredPerm` 時，只有 `Administrator` 能用。
   - AI 看到的工具名稱是 `discord-webhook__list_webhooks` 和 `discord-webhook__send_message`。

6. 改完 compose 要 `docker compose up -d mcp-gateway`，環境變數和掛載才會生效。只改 `mcp.json` 的話不用重啟。

## 給 Claude Code 用

先在系統設定使用者環境變數 `DISCORD_WEBHOOKS`，再在 `.mcp.json` 加上：

```json
{
  "mcpServers": {
    "discord-webhook": {
      "command": "dotnet",
      "args": ["E:/Project/MCPTools/publish/DiscordWebhook/DiscordWebhook.dll"],
      "env": { "DISCORD_WEBHOOKS": "${DISCORD_WEBHOOKS}" }
    }
  }
}
```
