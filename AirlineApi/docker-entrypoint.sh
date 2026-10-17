#!/bin/sh
set -eu
mkdir -p /data
if [ "$(id -u)" = "0" ]; then
  chown -R 1654:1654 /data || true
  if command -v setpriv >/dev/null 2>&1; then
    exec setpriv --reuid=1654 --regid=1654 --init-groups dotnet AirlineApi.dll
  fi
fi
exec dotnet AirlineApi.dll
