#!/usr/bin/env bash
set -euo pipefail

REPO="${NINEMROOM_REPO:-Miyu3710/9mRoom}"
CURRENT="${NINEMROOM_VERSION:-1.0.0}"
MANIFEST_URL="https://raw.githubusercontent.com/${REPO}/main/update-server.json"

tag=""
version=""
asset=""
sha256=""
release=""
manifest="$(curl -fsSL "${MANIFEST_URL}" 2>/dev/null || true)"

if [ -n "$manifest" ]; then
  version="$(printf '%s' "$manifest" | grep -o '"version"[[:space:]]*:[[:space:]]*"[^"]*"' | head -n 1 | sed 's/.*"\([^"]*\)"$/\1/')"
  tag="$(printf '%s' "$manifest" | grep -o '"tag"[[:space:]]*:[[:space:]]*"[^"]*"' | head -n 1 | sed 's/.*"\([^"]*\)"$/\1/')"
  asset="$(printf '%s' "$manifest" | grep -o '"asset"[[:space:]]*:[[:space:]]*"[^"]*"' | head -n 1 | sed 's/.*"\([^"]*\)"$/\1/')"
  sha256="$(printf '%s' "$manifest" | grep -o '"sha256"[[:space:]]*:[[:space:]]*"[^"]*"' | head -n 1 | sed 's/.*"\([^"]*\)"$/\1/')"
  release="$(printf '%s' "$manifest" | grep -o '"release"[[:space:]]*:[[:space:]]*"[^"]*"' | head -n 1 | sed 's/.*"\([^"]*\)"$/\1/')"
fi

if [ -z "$tag" ]; then
  atom="$(curl -fsSL "https://github.com/${REPO}/releases.atom")"
  tag="$(printf '%s' "$atom" | grep -o 'releases/tag/server-v[^"]*' | head -n 1 | sed 's#releases/tag/##')"
  version="${tag#server-v}"
fi

if [ -z "$tag" ]; then
  echo "没有找到 GitHub Server Release。" >&2
  exit 1
fi

if [ -z "$release" ]; then
  release="https://github.com/${REPO}/releases/tag/${tag}"
fi

echo "当前版本: ${CURRENT}"
echo "最新版本: ${version}"
echo "发布页面: ${release}"

if [ "$(printf '%s\n%s\n' "$CURRENT" "$version" | sort -V | tail -n 1)" = "$version" ] && [ "$CURRENT" != "$version" ]; then
  echo "发现新版本: ${release}"
  if [ -n "$asset" ]; then
    echo "更新包: https://github.com/${REPO}/releases/download/${tag}/${asset}"
  fi
  if [ -n "$sha256" ]; then
    echo "SHA-256: ${sha256}"
  fi
  echo
  echo "更新服务端："
  echo "  1. 下载上面的更新包"
  echo "  2. 停止当前服务"
  echo "  3. 解压覆盖程序目录（保留 data/ 与 .env）"
  echo "  4. 运行 ./run-server.sh"
else
  echo "已是最新版本。"
fi
