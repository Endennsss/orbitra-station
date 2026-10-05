#!/usr/bin/env bash
set -euo pipefail

cd /home/container

image_version="$(cat /opt/orbitra/.orbitra-image-version 2>/dev/null || printf '%s' unknown)"
installed_version="$(cat .orbitra-image-version 2>/dev/null || printf '%s' missing)"

# Обновляем только файлы production-пакета. Конфиг и данные сервера живут в
# каталоге Pelican и намеренно не заменяются новым образом.
if [[ ! -x ./Robust.Server || "$image_version" != "$installed_version" ]]; then
    rsync -a \
        --exclude='config.toml' \
        --exclude='data/' \
        --exclude='logs/' \
        --exclude='saves/' \
        /opt/orbitra/ /home/container/
fi

if [[ ! -f config.toml ]]; then
    cp /opt/orbitra/config.template.toml config.toml
fi

# Значения передаются отдельными аргументами, без shell-eval и правки TOML.
port="${SERVER_PORT:-1212}"
if [[ ! "$port" =~ ^[1-9][0-9]{0,4}$ ]] || (( port > 65535 )); then
    echo "Invalid SERVER_PORT: $port" >&2
    exit 1
fi

printf '%s\n' "$image_version" > .orbitra-image-version

discord_url="${DISCORD_URL:-}"
if [[ -n "$discord_url" && ! "$discord_url" =~ ^https://(discord\.gg|discord\.com)/ ]]; then
    echo "Invalid DISCORD_URL: expected an https://discord.gg or https://discord.com URL" >&2
    exit 1
fi

echo "Orbitra image: ${image_version}"
echo "Orbitra port: ${SERVER_PORT:-1212}"
exec ./Robust.Server --config-file config.toml --data-dir data \
    --cvar "net.port=$port" \
    --cvar "game.hostname=${SERVER_NAME:-Orbitra Station}" \
    --cvar "game.desc=${SERVER_DESC:-Русскоязычный сервер Orbitra Station}" \
    --cvar "infolinks.discord=$discord_url"
