# Обновления Orbitra в Discord

Workflow `.github/workflows/orbitra_changelog.yml` отправляет только новые ID
из `Resources/Changelog/_Orbitra/updates.yml` после push в `master`.
Коммиты и ванильный `Changelog.yml` не публикуются. Автор берётся из записи,
категории отображаются по-русски. Ченджлог сообщает об изменении в репозитории,
а не о завершении сборки или обновлении работающего сервера.

## Настройка

1. В Discord создайте webhook нужного текстового канала и скопируйте URL.
2. В GitHub откройте **Settings → Secrets and variables → Actions → New repository secret**.
3. Имя секрета: `ORBITRA_CHANGELOG_WEBHOOK`. Значение: обычный URL Discord
   вида `https://discord.com/api/webhooks/…/…`, **без `/github`**.
4. Закоммитьте workflow и скрипты, отправьте их в `master`.
5. Для проверки откройте **Actions → Orbitra Changelog → Run workflow**,
   укажите существующий `entry_id` и оставьте `dry_run` включённым. В логах
   появится JSON предпросмотра. Для реальной отправки снимите `dry_run`.
6. Если в этот канал уже подключён прямой webhook через **Settings → Webhooks**,
   отключите его, чтобы перестали приходить сообщения о коммитах.

Никакой GitHub webhook Secret или отдельный токен бота не требуется.
Не публикуйте URL Discord в коде, issue или чате.
Старый шаг Discord в `publish.yml` оставлен только для upstream-репозитория;
секрет `CHANGELOG_DISCORD_WEBHOOK` новым workflow не используется.

## Правила публикации

- Добавляйте новую запись с новым уникальным числовым `id`. Для ваших обновлений
  используйте `author: Endennsss`. Поддерживаются `Add`, `Fix`, `Tweak`, `Remove`.
- Сравниваются версии файла до и после всего push, поэтому несколько коммитов
  в одном push обрабатываются вместе. Изменение старого ID не отправляется снова.
- Первый запуск не отправляет весь архив. Если предыдущий файл недоступен,
  запуск останавливается: используйте ручную публикацию нужного ID.
- При отсутствии секрета запуск завершается ошибкой с инструкцией настройки.
- При HTTP 429 отправка ждёт указанный Discord интервал (до 30 секунд,
  максимум пять попыток). Сетевые ошибки не повторяются автоматически,
  поскольку сообщение уже могло быть доставлено.
- Постоянного журнала доставки нет. **Re-run jobs** и повторная ручная отправка
  могут создать дубликаты. После частичного сбоя сначала проверьте канал;
  восстановите только недоставленные записи через `entry_id`. Если доставлена
  лишь часть длинной записи, удалите её части перед повторной отправкой.
- Длинные записи делятся на несколько сообщений без потери текста.
  Упоминания отключены. Секрет не выводится в ошибках HTTP.
- Workflow не меняет файлы, не коммитит и имеет только `contents: read`.

## Локальная проверка без отправки

```powershell
python -m pip install PyYAML==6.0.3 requests==2.32.5
python -B -m unittest discover -s Tools/_Orbitra -p test_discord_changelog.py
$env:CHANGELOG_ENTRY_ID = '56'
$env:CHANGELOG_DRY_RUN = 'true'
python -B Tools/_Orbitra/discord_changelog.py
Remove-Item Env:CHANGELOG_ENTRY_ID, Env:CHANGELOG_DRY_RUN
```

Документация API: [Discord Execute Webhook](https://docs.discord.com/developers/resources/webhook#execute-webhook),
[GitHub Actions workflow syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax).
