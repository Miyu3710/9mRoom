#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

export NINEMROOM_DB="${NINEMROOM_DB:-$PWD/data/9mroom.db}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:5080}"

if [ -x ./NineMRoom.Server ]; then
  exec ./NineMRoom.Server "$@"
fi

exec dotnet NineMRoom.Server.dll "$@"
