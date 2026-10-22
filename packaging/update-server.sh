#!/usr/bin/env bash
set -euo pipefail

REPO="${NINEMROOM_REPO:-Miyu3710/9mRoom}"
CURRENT="${NINEMROOM_VERSION:-1.0.0}"

atom="$(curl -fsSL "https://github.com/${REPO}/releases.atom")"
tag="$(printf '%s' "$atom" | grep -o 'releases/tag/[^"]*' | head -n 1 | sed 's#releases/tag/##')"

if [ -z "$tag" ]; then
  echo "没有找到 GitHub Release。" >&2
  exit 1
fi

version="${tag#v}"
echo "当前版本: ${CURRENT}"
echo "最新版本: ${version}"

if [ "$(printf '%s\n%s\n' "$CURRENT" "$version" | sort -V | tail -n 1)" = "$version" ] && [ "$CURRENT" != "$version" ]; then
  echo "发现新版本: https://github.com/${REPO}/releases/tag/${tag}"
  echo "更新清单: https://github.com/${REPO}/releases/download/${tag}/update.json"
  echo
  echo "更新服务端："
  echo "  1. 下载清单中的 linux.asset"
  echo "  2. 停止当前服务"
  echo "  3. 解压覆盖程序目录（保留 data/ 与 .env）"
  echo "  4. 运行 ./run-server.sh"
else
  echo "已是最新版本。"
fi
