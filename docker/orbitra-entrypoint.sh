#!/usr/bin/env bash
set -euo pipefail

cd /home/container

image_version="$(cat /opt/orbitra/.orbitra-image-version)"

if [[ ! -f config.toml ]]; then
    cp /opt/orbitra/config.template.toml config.toml
fi

# Значения передаются отдельными аргументами, без shell-eval и правки TOML.
port="${SERVER_PORT:-1212}"
if [[ ! "$port" =~ ^[1-9][0-9]{0,4}$ ]] || (( port > 65535 )); then
    echo "Invalid SERVER_PORT: $port" >&2
    exit 1
fi

discord_url="${DISCORD_URL:-}"
if [[ -n "$discord_url" && ! "$discord_url" =~ ^https://(discord\.gg|discord\.com)/ ]]; then
    echo "Invalid DISCORD_URL: expected an https://discord.gg or https://discord.com URL" >&2
    exit 1
fi

echo "Orbitra image: ${image_version}"
echo "Orbitra port: ${SERVER_PORT:-1212}"
mkdir -p logs
# Движок ищет ресурсы и архив клиента рядом с исполняемым файлом.
exec /opt/orbitra/Robust.Server --config-file config.toml --data-dir data \
    --cvar "net.port=$port" \
    --cvar "game.hostname=${SERVER_NAME:-Orbitra Station}" \
    --cvar "game.desc=${SERVER_DESC:-Русскоязычный сервер Orbitra Station}" \
    --cvar "infolinks.discord=$discord_url"
