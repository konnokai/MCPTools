#!/usr/bin/env bash
# 把 src/ 底下每個工具發佈到 <輸出資料夾>/<工具名稱>/。說明見 README.md 的「發佈」。
# 用法：./publish.sh [輸出資料夾]，沒給時用 repo 裡的 publish/
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
mkdir -p "${1:-$repo/publish}"
out="$(cd "${1:-$repo/publish}" && pwd)"

for project in "$repo"/src/*/*.csproj; do
  name="$(basename "$project" .csproj)"
  echo "== $name -> $out/$name"

  # 先發佈到暫存資料夾再換掉：gateway 還在跑的舊程序開著舊的 dll，直接覆蓋可能讓它當掉；發佈失敗時舊版也還在
  rm -rf "$out/.$name.new" "$out/.$name.old"
  dotnet publish "$project" -c Release -o "$out/.$name.new" --nologo
  if [[ -d "$out/$name" ]]; then
    mv "$out/$name" "$out/.$name.old"
  fi
  mv "$out/.$name.new" "$out/$name"
  rm -rf "$out/.$name.old"
done

echo "完成。McpGateway 要重啟才會載入新版：docker compose restart mcp-gateway"
