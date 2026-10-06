# Работа с открытой Unity через MCP

Прямой запрос автора от 2026-10-06: работать через MCP, сохраняя возможность ручной работы в том же Editor; весь игровой контент должен быть доступен для осмотра в Edit Mode.

## Подключение

- Unity: существующий 6000.2.14f1. Установлен MCP for Unity 10.0.0 через фиксированный Git tag: `com.coplaydev.unity-mcp`. Другие прямые зависимости не обновляются.
- Источник: [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp/tree/v10.0.0), [инструкция установки](https://coplaydev.github.io/unity-mcp/getting-started/install). Пакет поддерживает Unity 2021.3+; сервер требует Python 3.10+.
- Python server: изолированный `.local-tools/unity-mcp/venv`, версия `mcpforunityserver==10.0.0`. Восстановление окружения — `tools/unity-mcp/install-server.ps1`.
- Endpoint: `http://127.0.0.1:8766/mcp`; сервер слушает только локальный интерфейс. Аккаунт Coplay или облачный ключ для этого подключения не нужны.
- Codex: секция `mcp_servers.unityChefShow` в `C:\Users\User\.codex\config.toml`. Другие настройки сохранены; резервная копия `config.toml.before-unity-mcp-20261006` лежит рядом. После добавления MCP конфигурации нужно обновить подключения Codex/перезапустить его приложение, если сервер ещё не появился в списке инструментов. [Официальная настройка MCP](https://learn.chatgpt.com/docs/extend/mcp?surface=cli).
- `UnityMcpConnection.cs` — Editor-служба соединения. При наличии `.local-tools/unity-mcp/enabled` запускает локальный сервер, если он ещё не отвечает, и подключает Editor. Не создаёт игровые объекты, не сохраняет и не регенерирует сцену. Соединение восстанавливается после domain reload; команда повторного подключения — `Tools → Chef Show → MCP → Connect Local Server`.
- Статус моста доступен в `Window → MCP for Unity`. Отчёт первого подключения — `TestResults/unity-mcp-connected.txt`; ошибки — `TestResults/unity-mcp-connection-error.txt`.

Первичный импорт пакета требует Assets → Refresh (Ctrl+R) в уже открытом Editor, если Unity не обновила проект сама. Во время компиляции команды сцены откладываются. На время установки может появиться штатный мастер настройки пакета.

## Совместная работа

MCP не занимает мышь/клавиатуру. Его команды исполняются на стороне Editor. Это общий проект и общая сцена, поэтому одновременная правка одного объекта не является независимой транзакцией.

1. Перед изменением агент читает instance/project, активную сцену, dirty-state, Play Mode/компиляцию и выделение автора. При нескольких Editor выбирает только Unity_3D_Battle_Cheff через `set_active_instance`.
2. Агент сообщает, какую группу объектов/файлов меняет. Автор может работать в другой группе. Если выделение или несохранённые изменения пересекаются с задачей, сначала согласуется порядок работы.
3. Изменения проходят через Unity Editor API/MCP, с Undo там, где это поддерживается. Нельзя заменять рабочую сцену generated-копией или закрывать её с потерей несохранённых правок.
4. Не запускать автоматическую регенерацию, массовый reimport и изменение версий пакетов. Play Mode-тесты и операции, переключающие сцены, выполнять в отдельном объявленном интервале проверки.
5. После этапа сохранить согласованную сцену, проверить результат, записать отчёт и перечислить объекты для ручного осмотра. Dirty-state не доказывает, кто изменил сцену; при неопределённости не подменять данные на диске.

## Что всегда доступно в Edit Mode

Рабочая сцена: `Assets/_ChefShow/Scenes/ChefShow_Prototype.unity`. Generated-сцена служит результатом явной генерации и не заменяет ручную копию.

| Группа Hierarchy | Что можно посмотреть руками |
|---|---|
| Arena | Floor, стены, TeamAStations/TeamBStations, Pantry, Judging Table, места подачи и маршруты |
| Contestants | NPC_A2…A6, NPC_B1…B6 при игроке A; PrototypeActor, Collider и Transform |
| Player | CharacterController, FirstPersonRig, PrototypeActor; дочерняя MainCamera |
| Chefs | CHEF_SAVORY и CHEF_PASTRY, маркеры актёров и примитивы |
| Systems | GameBootstrap со ссылками на Config, InputDefinition, Player, HUD и UI input |
| UI / EventSystem / Lighting | Canvas/HUD, InputSystemUIInputModule и освещение |

Игровые объекты не создаются только при Play и не скрываются через HideFlags. MonoBehaviour и ссылки доступны в Inspector; конфиги и input actions являются видимыми assets в Project. Чистые C#-данные попытки появляются при запуске — это состояние игры, а не спрятанная геометрия.

MCP — Editor-пакет и локальный процесс, поэтому ему не нужен компонент на игровом объекте. Его исходники доступны в `Packages/MCP for Unity`; наш соединитель — в `Assets/_ChefShow/Scripts/Editor/Integration/UnityMcpConnection.cs`.

## Диагностика без управления окном

Если Codex ещё не обновил список MCP-инструментов, сервер можно проверить тем же MCP-протоколом через Python SDK:

```powershell
& ./.local-tools/unity-mcp/venv/Scripts/python.exe tools/unity-mcp/mcp_client.py read mcpforunity://instances
& ./.local-tools/unity-mcp/venv/Scripts/python.exe tools/unity-mcp/mcp_client.py resources
```

Для tool-call помощник принимает JSON-файл аргументов. Целевая instance при диагностике хранится в локальном `.local-tools/unity-mcp/instance.txt`. Логи, окружение и локальный статус исключены из Git; пакет, Editor-служба, инструкции и инструменты восстановления остаются частью проекта.

## Результат настройки

2026-10-06: Python MCP 10.0.0 запущен, endpoint и список MCP resources проверены. Конфигурация Codex добавлена. После Refresh мост подключился к `Unity_3D_Battle_Cheff@2c173f3a9cfcaaec`, Unity 6000.2.14f1. Через настоящий MCP проверены editor state, selection, активная сохранённая сцена и Hierarchy; выполнены Editor-команда пространственной правки, добавление URP Camera Data и сохранение сцены. Управление окном не использовалось. Проверки запущены через MCP: EditMode 5/5, Play Mode 4/4 PASS. Редактор возвращён в Edit Mode с рабочей сценой. Новый кадр: `TestResults/layout-overview.png`. Техническое соединение и текущий пространственный срез проверены; полный gameplay ещё не реализован.
