# Архитектура: фактическая база и целевые границы

Аудит и начало реализации: 2026-10-05. Исходный аудит был статическим; затем этап 1 скомпилирован и проверен в Editor/Play Mode. Windows-сборка не запускалась. Каноническая инструкция: [CODEX_PROJECT_INSTRUCTIONS.md](../CODEX_PROJECT_INSTRUCTIONS.md).

## Исходное состояние до изменений

| Область | Найдено | Источник |
|---|---|---|
| Корень Unity | Assets, Packages, ProjectSettings, Library, Logs, UserSettings | Файловая система |
| Editor | 6000.2.14f1, revision 589824c1fc31 | ProjectSettings/ProjectVersion.txt |
| URP | com.unity.render-pipelines.universal 17.2.0 | manifest, lock, cache/package.json |
| Input System | 1.16.0; activeInputHandler: 1 | manifest, lock, cache, ProjectSettings.asset |
| UI | com.unity.ugui 2.0.0 | manifest, lock, cache |
| Тесты | com.unity.test-framework 1.6.0 | manifest, lock, cache |
| Дополнительные пакеты | AI Navigation 2.0.9, Timeline 1.8.9, Visual Scripting 1.9.8 и пакеты шаблона | manifest/lock; наличие не означает использование в gameplay |
| Render pipeline | Graphics и PC Quality ссылаются на PC_RPAsset; Mobile на Mobile_RPAsset | GraphicsSettings, QualitySettings, соответствующие .meta |
| Renderer | PC_RPAsset → PC_Renderer; GUID разрешён | Assets/Settings/PC_RPAsset.asset, PC_Renderer.asset.meta |
| Quality | Текущее качество PC, Standalone default PC | QualitySettings.asset |
| Цвет | m_ActiveColorSpace: 1 (Linear) | ProjectSettings.asset |
| Сериализация/Git | Force Text; Visible Meta Files; Unity .gitignore; исходно чистое дерево | EditorSettings, VersionControlSettings, git status |
| Сцены | Только Assets/Scenes/SampleScene.unity включена в EditorBuildSettings | Assets и EditorBuildSettings |
| Ввод шаблона | InputSystem_Actions.inputactions: Player, UI; Interact использует Hold | Assets/InputSystem_Actions.inputactions |
| Код автора | _ChefShow отсутствует; найден только Readme/ReadmeEditor шаблона | Перечень Assets |
| UI и шрифты игры | Не созданы; кириллический font asset в Assets не найден | Перечень Assets |

Версии всех прямых зависимостей manifest совпадают с lock. Четыре базовых пакета также проверены в локальном PackageCache. Из этого не следует, что конкретная игровая сцена уже компилируется или запускается.

## Доступ к Editor и открытие проекта

Во время исходного снимка `Get-Process Unity` не показал открытого Editor. После поиска по сгенерированным csproj найден и использован `D:\Program-All\Unity_hub_launcher\Unity_programm\6000.2.14f1\Editor\Unity.exe`. Подключённого Unity MCP/Editor-инструмента нет; генерация и проверки выполнены скрытыми batch-процессами установленного Editor, без закрытия окна автора.

Для ручного открытия: Unity Hub → Projects → Add project from disk → корень `D:\Unity-Project\Unity_3D_Battle_Cheff` → Editor 6000.2.14f1. Не выбирать папку Assets. Рабочая сцена теперь создана: `Assets/_ChefShow/Scenes/ChefShow_Prototype.unity`; [инструкция первого запуска](first-playable.md).

## Реализовано на этапе 1

`Assets/_ChefShow` содержит Runtime/Editor сборки, конфиг, clock/event bus/контекст пробной попытки, bootstrap, action maps, CharacterController/камеру/интеракцию, маркеры участников, uGUI HUD, паузу/чувствительность/restart и первые debug-команды. Builder создаёт примитивную студию и две сцены через Editor API; Validator проверяет текущий этап. NPC и шефы пока статические placeholders; pantry и станции показывают контекст без операций готовки.

Runtime использует копию input asset, общую игровую delta и отдельные данные попытки. Настройки чувствительности в меню не записываются в исходный asset. Команда/место игрока на этом этапе определяются генерацией; mismatch конфиг/сцена останавливает запуск с диагностикой.

uGUI Text и TextMesh используют встроенный LegacyRuntime.ttf: Builder проверяет весь русский алфавит, Play Mode-кадр подтверждает отрисовку. TMP пакет установлен, но его Essential Resources в шаблоне не импортированы; первый этап использует предусмотренный инструкцией uGUI-вариант без импорта дополнительного контента. Переход на TMP возможен при дальнейшем UI-проходе. Это не изменение правил кириллицы/доступности.

Рабочая сцена добавлена после существующей SampleScene в EditorBuildSettings; существующая сцена и input config object сохранены. При будущей Windows-сборке надо явно выбрать Chef Show стартовой сценой, сохранив пользовательский список. Unity также создала SceneTemplateSettings.json через собственный Editor. Packages/версии и Source SampleScene не менялись.

Код подтверждён компиляцией Editor, автоматическими EditMode/PlayMode сценариями и отрисованным кадром. Полная игровая архитектура ниже остаётся планом, пока не появились готовка, полноценный flow, NPC-симуляция, реакции, scoring и финал.

## Целевая организация по инструкции

Собственный контент будет размещаться в `Assets/_ChefShow/` по мере разработки. Документы остаются вне Assets. На первом этапе достаточно Runtime и Editor сборок, с отдельными тестовыми сборками при появлении проверяемой логики. Runtime не зависит от UnityEditor. Имена и файловая схема заданы технической инструкцией, не навыком гейм-дизайна.

Generated-сцена: `Assets/_ChefShow/Generated/Scenes/ChefShow_Prototype_Generated.unity`. Рабочая копия автора: `Assets/_ChefShow/Scenes/ChefShow_Prototype.unity`. Builder создаёт assets через Editor API, выполняется явной командой и сохраняет ручную рабочую копию и GUID. Не модифицировать SampleScene ради стартового прототипа.

## Владельцы данных и зависимости

| Граница | Ответственность | Направление зависимости |
|---|---|---|
| Запуск и жизненный цикл | Проверка ссылок, создание/освобождение одной сессии | Связывает системы; не рассчитывает блюда |
| Состояния выпуска | Разрешённые фазы, задания, начало/закрытие раунда | Использует общие время, roster, результаты |
| Игровое время | Один ход симуляции, пауза и согласованное ускорение | Таймер, нагрев, NPC и реакции потребляют одинаковую delta |
| Ввод/интеракция | Движение, видимость цели, фокус, разрешённые команды | Запрашивает операции у владельца станции/инвентаря |
| Еда и станции | Единственное местоположение порции, происхождение, подготовка/нагрев | Передаёт неизменяемый снимок на подачу |
| Рецепты и оценка | Состав, применимые критерии, причины и пределы | Получают снимок и конфигурацию; не читают цвет/положение визуала |
| NPC и roster | Постоянные участники, seeded результаты, активность | Результат не зависит от балла игрока или RNG реплик |
| Судейство и выбывание | Показ готовых итогов; применение 2 выбывших на команду один раз | Presentation не пересчитывает решение при Skip |
| Реакции | Событие → фильтр → очередь → текст/жест | Не изменяют score, таймер или состав |
| Финал | Анонимные записи, фиксированные номера, закрытый mapping, reveal | До reveal UI не получает автора/команду |
| UI/звук/debug | Представление фактов и явные диагностические команды | Не являются источником игрового состояния |

Это целевые контракты из инструкции. Их присутствие в документе не означает, что соответствующие классы уже реализованы.

## Инварианты первой реализации

- Перезапуск очищает контекст, подписки, реплики и старые отложенные действия.
- Исходные ScriptableObject assets содержат определения; текущая попытка не меняет их.
- У каждой порции одно местоположение; отказ переноса не расходует продукт.
- Подготовка и нагрев независимы; смешивание сохраняет происхождение и загрязнение.
- Подача создаёт неизменяемый результат; поздние команды после закрытия раунда отвергаются.
- Results и выбывание рассчитываются/применяются однократно; Skip меняет только показ.
- Одна camera/listener, один EventSystem с InputSystemUIInputModule; кириллица проверяется в Unity.

У шаблона уже есть input asset с Player/UI. Перед F-003 выбрать одну конфигурацию карт Gameplay/Station/UI/Debug для Chef Show согласно инструкции, сохранить шаблонный asset и устранить случайное одновременное чтение двух наборов. Hold у Interact шаблона не считается готовым выполнением мгновенного E из GDD.
