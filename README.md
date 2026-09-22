# Orbitra

Русскоязычный форк [Space Station 14](https://github.com/space-wizards/space-station-14) на движке [RobustToolbox](https://github.com/space-wizards/RobustToolbox), написанном на C#.

Этот репозиторий содержит игровой контент, клиент и сервер Orbitra. Это отдельный форк, а не основной репозиторий Space Station 14.

## О проекте

- Русская локализация интерфейса, названий и описаний игровых объектов.
- Собственное оформление чата, эмоций и всплывающих сообщений.
- Графика Forge и Goob, а также отдельные сохранённые ресурсы Monolith и Dead Space.
- Правила разработки и работы с ИИ, адаптированные для Orbitra.

Проект развивается: локализация и визуальная согласованность могут требовать доработки. Импорт графики не означает перенос механик соответствующего сервера.

## Ссылки

- [Репозиторий Orbitra](https://github.com/Endennsss/lime-station)
- [Задачи и сообщения об ошибках](https://github.com/Endennsss/lime-station/issues)
- [Пулл-реквесты](https://github.com/Endennsss/lime-station/pulls)
- [Документация Space Station 14](https://docs.spacestation14.com/)
- [Сайт Space Station 14](https://spacestation14.com/)

Ссылки на документацию и сайт SS14 относятся к исходному проекту, а не к отдельным сервисам Orbitra.

## Сборка и локальный запуск

Понадобятся Git, Python 3 и .NET SDK, совместимый с [global.json](global.json). Сейчас указан SDK `10.0.100` с `rollForward: latestFeature`.

Клонируйте репозиторий и подготовьте подмодули движка:

```powershell
git clone https://github.com/Endennsss/lime-station.git
cd lime-station
python RUN_THIS.py
```

Соберите клиент и сервер:

```powershell
dotnet build Content.Client/Content.Client.csproj --configuration Debug
dotnet build Content.Server/Content.Server.csproj --configuration Debug
```

Запустите сервер и клиент в отдельных терминалах:

```powershell
dotnet run --project Content.Server/Content.Server.csproj --configuration Debug --no-build
```

```powershell
dotnet run --project Content.Client/Content.Client.csproj --configuration Debug --no-build
```

Для локальной игры подключитесь клиентом к своему серверу. Запуск из исходников предназначен для разработки и не заменяет подготовку публичного сервера или релизного пакета.

## Участие в разработке

Одна задача — один связный набор изменений. Новые файлы форка размещайте в соответствующих каталогах `_Orbitra`; изменения исходных файлов сохраняйте минимальными.

Перед работой прочитайте:

- [Общие инструкции](AGENTS.md)
- [Правила разработки](.agents/rules/)
- [Навыки для подсистем SS14](.agents/skills/)
- [Правила участия и пулл-реквестов](CONTRIBUTING.md)
- [Шаблон пулл-реквеста](.github/PULL_REQUEST_TEMPLATE.md)

Заголовки новых коммитов и PR оформляйте как `<type>: <описание на русском>`, например `fix: исправить отображение сообщений`. Типы и примеры перечислены в [правиле коммитов](.agents/rules/git-commit-format.md).

ИИ можно использовать как помощника при разработке Orbitra. Автор изменений отвечает за проверку результата, качество, безопасность и лицензии ресурсов. Сгенерированный код и описание PR не заменяют фактические тесты. При отправке изменений в другие проекты соблюдайте их собственную политику.

## Проверки

После изменений прототипов или локализации выполните YAML linter. При первом запуске предварительно соберите его:

```powershell
dotnet build Content.YAMLLinter/Content.YAMLLinter.csproj --configuration Debug
dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj --no-build
```

Для C# соберите затронутый проект; визуальные изменения дополнительно проверьте в клиенте. Запуск до главного меню подтверждает загрузку, но не работу механики в игре.

После изменения правил или навыков синхронизируйте файлы совместимости и выполните:

```powershell
pwsh ./.agents/rules/check-rule-bridges.ps1
pwsh ./.agents/skills/check-skill-bridges.ps1
```

Полные требования приведены в [правилах проверок](.agents/rules/ss14-testing-guidelines.md). Остановите процессы, запущенные для проверки, перед завершением работы.

## Обновления из апстрима

`origin` — репозиторий Orbitra; `upstream` — исходный Space Station 14. Если `upstream` ещё не настроен:

```powershell
git remote add upstream https://github.com/space-wizards/space-station-14.git
```

Сначала сохраните текущую работу в коммите и убедитесь, что рабочая копия чистая. Выполняйте обновление в отдельной ветке:

```powershell
git fetch upstream
git switch -c codex/upstream-update
git merge upstream/master
git submodule update --init --recursive
```

Разрешите конфликты с сохранением особенностей Orbitra, выполните проверки и подготовьте PR. Для нового merge-коммита используйте `upstream: <описание на русском>`; историю исходных коммитов не переписывайте.

## Лицензии и авторство

Основной код контента распространяется по [MIT](LICENSE.TXT). Движок, зависимости и отдельные ресурсы имеют собственные условия распространения.

Большинство графических ресурсов используют CC-BY-SA-3.0, но лицензия конкретного файла определяется его метаданными и сопутствующими файлами лицензий. MIT для кода не заменяет лицензию изображения или звука.

Сохраняйте авторство, метаданные и тексты лицензий при распространении. Это не является юридической гарантией или аудитом всех ресурсов репозитория.

В ранее существующем контенте могут присутствовать ресурсы с некоммерческими ограничениями. Для коммерческого использования требуется отдельная проверка состава ресурсов и соблюдение их условий.

Orbitra основана на работе Space Wizards Federation и участников SS14. Авторство исходного проекта и сторонних ресурсов сохраняется.
