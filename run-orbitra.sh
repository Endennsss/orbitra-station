#!/usr/bin/env bash
set -euo pipefail

cd -- "$(dirname -- "${BASH_SOURCE[0]}")"

if [[ ${1:-} == --help || ${1:-} == -h ]]; then
    printf '%s\n' 'Использование: ./run-orbitra.sh [--skip-build] [аргументы клиента]'
    printf '%s\n' 'Запускает локальную карту Dev в песочнице на 127.0.0.1:1214.'
    exit 0
fi

skip_build=false
if [[ ${1:-} == --skip-build ]]; then
    skip_build=true
    shift
fi

for command in dotnet curl flock; do
    if ! command -v "$command" >/dev/null; then
        printf 'Требуется команда %s.\n' "$command" >&2
        exit 1
    fi
done

state="$PWD/bin/OrbitraLocal"
mkdir -p -- "$state"
exec 9>"$state/run.lock"
if ! flock -n 9; then
    printf 'Запускатор Orbitra уже работает в этой копии репозитория.\n' >&2
    exit 1
fi

address=127.0.0.1:1214
status_url="http://$address/status"
server_name="Orbitra Local $$"
server_log="$state/server.log"
server_pid=
client_pid=

cleanup() {
    # Сигналы отправляются только дочерним процессам этого запуска.
    for pid in "$client_pid" "$server_pid"; do
        if [[ -n $pid ]]; then
            kill -TERM "$pid" 2>/dev/null || true
        fi
    done
    if [[ -n $client_pid ]]; then
        wait "$client_pid" 2>/dev/null || true
    fi
    if [[ -n $server_pid ]]; then
        wait "$server_pid" 2>/dev/null || true
    fi
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM HUP

if curl --silent --noproxy '*' --max-time 1 "$status_url" >/dev/null; then
    printf 'Адрес %s уже занят.\n' "$address" >&2
    exit 1
fi

if ! $skip_build; then
    if [[ ! -f RobustToolbox/Robust.Client/Robust.Client.csproj ]]; then
        git submodule update --init --recursive
    fi
    # Сборки идут последовательно с одним процессом MSBuild.
    for project in Content.Server Content.Client; do
        dotnet build "$project/$project.csproj" -c Debug -m:1 -nodeReuse:false -p:UseSharedCompilation=false
    done
fi

if [[ ! -f bin/Content.Server/Content.Server.dll || ! -f bin/Content.Client/Content.Client.dll ]]; then
    printf 'Сборка не найдена. Запусти скрипт без --skip-build.\n' >&2
    exit 1
fi

printf 'Запускаю локальный сервер. Лог: %s\n' "$server_log"
dotnet bin/Content.Server/Content.Server.dll \
    --config-file "$state/server.toml" --data-dir "$state/data" \
    --cvar net.port=1214 --cvar net.bindto=127.0.0.1 --cvar net.upnp=false \
    --cvar status.enabled=true --cvar "status.bind=$address" \
    --cvar hub.advertise=false --cvar auth.mode=2 --cvar "game.hostname=$server_name" \
    --cvar game.defaultpreset=sandbox --cvar game.map=Dev \
    --cvar game.lobbyenabled=true --cvar game.lobbyduration=600 \
    --cvar database.engine=sqlite >"$server_log" 2>&1 &
server_pid=$!

deadline=$((SECONDS + 90))
while kill -0 "$server_pid" 2>/dev/null && (( SECONDS < deadline )); do
    if response=$(curl --silent --fail --noproxy '*' --max-time 1 "$status_url") &&
        [[ $response == *"\"$server_name\""* && $response == *'"run_level"'* ]]; then
        printf 'Сервер готов. Подключаю клиент к %s.\n' "$address"
        dotnet bin/Content.Client/Content.Client.dll --connect --connect-address "$address" "$@" &
        client_pid=$!
        result=0
        wait "$client_pid" || result=$?
        client_pid=
        exit "$result"
    fi
    sleep 0.5
done

printf 'Сервер не запустился. Последние строки лога:\n' >&2
tail -n 25 "$server_log" >&2
exit 1
