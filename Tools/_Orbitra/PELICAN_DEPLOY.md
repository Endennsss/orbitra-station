# Развёртывание Orbitra в Pelican

Репозиторий собирается GitHub Actions в публичный Docker-образ:

```text
ghcr.io/endennsss/orbitra-station:latest
```

В образ попадает production-пакет, созданный через `Content.Packaging` с
платформой `linux-x64` и `--hybrid-acz`: клиент того же коммита раздаётся сервером лаунчеру. Pelican запускает образ через Wings от пользователя
`container`; сборка исходников на VPS не требуется.

После первой публикации откройте GitHub Packages → orbitra-station → Package settings
и установите видимость Public. До этого анонимное скачивание Wings не работает.

## Первый импорт

1. Откройте **Admin → Eggs → Import**.
2. Импортируйте `pelican/egg-orbitra.json`.
3. Создайте сервер из Egg `Orbitra Station` на нужной Node.
4. Выделите порт `1212` и разрешите его в Firewall Netcup для TCP и UDP.
5. На странице Startup проверьте Docker image:

   ```text
   ghcr.io/endennsss/orbitra-station:latest
   ```

6. Оставьте переменные по умолчанию либо измените `SERVER_NAME` и
   `SERVER_DESC`.
7. Выполните **Reinstall**, затем запустите сервер.

При первом старте image синхронизирует production-файлы в `/home/container`.
`config.toml`, `data`, `logs` и `saves` сохраняются между обновлениями.

## Конфигурация Hub

В образ уже попадает безопасный шаблон с отключённым локальным входом
администратора и включённой публикацией в Hub. Домен не требуется:

```toml
[net]
port = 1212

[hub]
advertise = true
server_url = ""
```

При открытом порте сервер будет доступен как `ss14://152.53.20.79:1212`.

## Обновление после merge

1. Откройте **GitHub → Actions → Orbitra Server Image**.
2. Нажмите **Run workflow** на `master`.
3. После успешной сборки откройте Pelican.
4. Нажмите **Reinstall** у сервера.
5. Запустите сервер и проверьте логи.

Workflow публикует два тега:

```text
latest
sha-<полный SHA коммита>
```

Для отката временно укажите в Pelican предыдущий `sha-...` тег и выполните
`Reinstall` ещё раз.

## Проверка запуска

В консоли Pelican:

```text
status
```

С VPS можно проверить status API:

```bash
curl http://127.0.0.1:1212/status
```

Снаружи используйте публичный IP VPS. Если API не отвечает, сначала проверьте
выделение порта в Pelican, Firewall Netcup и системный firewall Debian.

Название, описание и порт из Pelican передаются через `--cvar` и имеют приоритет над конфигом.
Entrypoint запускает фиксированную команду; произвольные shell-команды из Startup не исполняются.
Перед обновлением остановите сервер и создайте резервную копию volume. Откат образа не откатывает базу данных.
Проверяйте SHA в строке `Orbitra image:` после обновления: если он прежний, выберите явный `sha-...` image.
