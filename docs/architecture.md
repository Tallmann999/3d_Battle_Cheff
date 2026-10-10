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

Во время исходного снимка `Get-Process Unity` не показал открытого Editor. После поиска по сгенерированным csproj найден и использован `D:\Program-All\Unity_hub_launcher\Unity_programm\6000.2.14f1\Editor\Unity.exe`. На этапе 1 Unity MCP ещё не был подключён; генерация и проверки выполнены скрытыми batch-процессами установленного Editor, без закрытия окна автора.

2026-10-06 автор запросил прямую работу с открытым Editor через MCP. В manifest добавлен MCP for Unity 10.0.0, установлен изолированный Python server и добавлена секция `mcp_servers.unityChefShow` в конфигурацию Codex. Локальный HTTP MCP отвечает на `127.0.0.1:8766`; после первичного Refresh мост подключился к Unity 6000.2.14f1. Связь, чтение контекста/Hierarchy, пространственная правка и сохранение через MCP проверены. Детали и правила совместной работы: [unity-mcp.md](unity-mcp.md). Служба соединения не создаёт и не регенерирует игровую сцену; игровой контент хранится в рабочей сцене и виден в Edit Mode.

Для ручного открытия: Unity Hub → Projects → Add project from disk → корень `D:\Unity-Project\Unity_3D_Battle_Cheff` → Editor 6000.2.14f1. Не выбирать папку Assets. Рабочая сцена теперь создана: `Assets/_ChefShow/Scenes/ChefShow_Prototype.unity`; [инструкция первого запуска](first-playable.md).

## Реализовано на этапе 1

`Assets/_ChefShow` содержит Runtime/Editor сборки, конфиг, clock/event bus/контекст пробной попытки, bootstrap, action maps, CharacterController/камеру/интеракцию, маркеры участников, uGUI HUD, паузу/чувствительность/restart и первые debug-команды. Builder создаёт примитивную студию и две сцены через Editor API; Validator проверяет текущий этап. NPC и шефы пока статические placeholders; pantry и станции показывают контекст без операций готовки.

Runtime использует копию input asset, общую игровую delta и отдельные данные попытки. Настройки чувствительности в меню не записываются в исходный asset. Команда/место игрока на этом этапе определяются генерацией; mismatch конфиг/сцена останавливает запуск с диагностикой.

uGUI Text и TextMesh используют встроенный LegacyRuntime.ttf: Builder проверяет весь русский алфавит, Play Mode-кадр подтверждает отрисовку. TMP пакет установлен, но его Essential Resources в шаблоне не импортированы; первый этап использует предусмотренный инструкцией uGUI-вариант без импорта дополнительного контента. Переход на TMP возможен при дальнейшем UI-проходе. Это не изменение правил кириллицы/доступности.

Рабочая сцена добавлена после существующей SampleScene в EditorBuildSettings; существующая сцена и input config object сохранены. При будущей Windows-сборке надо явно выбрать Chef Show стартовой сценой, сохранив пользовательский список. Unity также создала SceneTemplateSettings.json через собственный Editor. Packages/версии и Source SampleScene не менялись.

Код подтверждён компиляцией Editor, автоматическими EditMode/PlayMode сценариями и отрисованным кадром. Полная игровая архитектура ниже остаётся планом, пока не появились готовка, полноценный flow, NPC-симуляция, реакции, scoring и финал.

## Реализовано в F-004 — 2026-10-06

`PrototypeRun` владеет `InventoryState`: корзиной 10 предметов, лотком 24, одной рукой и двумя snap-местами (доска/рабочее место). Один `FoodPortion` сохраняет ID и владельца при каждом переносе; возвращённые/утилизированные записи учитываются явно. Упаковка — один отдельный предмет с `IngredientDefinition.Contents`; распаковка/количество содержимого относятся к F-005. `InventoryChanged` передаёт снимок ID, run_id, round_id="prototype", actor_id/team, simulation_time, действия/количества/местоположения. Полных игровых раундов пока нет.

`InventoryController` принимает команды только через текущую карту Gameplay/Station, повторно проверяет цель под прицелом и принадлежность станции, затем вызывает владельца данных. `FoodDisplay` показывает данные на заранее сохранённых объектах корзины, лотка, руки и перелётов; runtime GameObjects/definition assets не создаются. Rigidbody корзины активен после G, пауза/timeup замораживают его, restart возвращает пустую корзину к pantry. В руках корзина показывается в масштабе 0.65; на столе/полу — 1.

Визуалы лотка имеют SizeMultiplier=0.5, области выбора 0.16 × 0.10 × 0.10 м. У пустых мест Collider отключён, чтобы они не перекрывали видимую еду; GameObjects/компоненты остаются в Hierarchy. После смены родителя/положения корзины Physics.SyncTransforms синхронизирует немедленный raycast с новой позой. Совместимость доски проверяет модель; E целится в видимую доску/рабочее место.

`InventorySceneInstaller` — явная узкая установка в working/generated через Editor API, с резервными копиями и проверкой dirty. Повтор не переставляет установленные объекты/ручные ссылки. Builder вызывает тот же installer только для своей новой generated-сцены. IngredientCatalog и 14 вариантов подбора (12 базовых + мешок картошки/коробка яиц), материалы/меши созданы Unity API с сохранением GUID. Input asset дополнен без смены GUID: Tab корзина, ЛКМ подбор/выгрузка, G уронить, Q задание, E перенос, RMB отмена/фокус. `FirstPersonRig` исключает собственный CharacterController/предметы в руках из raycast, сохраняя occlusion ближайшими внешними коллайдерами; фокус допускает ограниченный обзор мышью по столу.

## Целевая организация по инструкции

### Рабочее место D-012…D-015 — результат и план от 2026-10-06

Фактическая база: F-004, квадратные столы и четыре зоны. D-012/D-013 уже применены по следующему запросу автора: передние Board/Work Surface и дальние Basket Dock/Ingredient Tray на всех 12 станциях; сохранённый Transform Station Focus назначен в PrototypeInteractable. FirstPersonRig хранит обычную позу/FOV и плавно смещает/сужает вид в фокусе; движение ограничивает SphereCast с исключением игрока. Выход/Restart восстанавливают вид, пауза использует общую delta. Play Mode 9/9 PASS; нарезка F-005 ещё не реализована.

Целевой вариант: прямоугольные станции с шестью основными зонами, включая сковороду/кастрюлю (F-002/F-006), затем длинные столы за участниками и выбор посуды (F-002/F-007). Посуда имеет вид/размер/глубину/вместимость; её параметры и фактическая еда входят в неизменяемый снимок для F-010. Цвет/меш/дальнейшее положение не являются источником очков. Единицы и правила вместимости/смены — Q-014; вклад в оценку — Q-015; размеры/камера — Q-016.

Все новые объекты/компоненты сохраняются и доступны в Edit Mode по D-009. KitchenWorkspaceUpdate узко меняет working/generated через Editor API/MCP с копиями, Undo и сохранением GUID; не регенерирует рабочую сцену. Builder учитывает настройки при собственной явной генерации. Папка `D:/ROBLOX/CrazeEfGG/Assets/Sort_Pot/` содержит три FBX и текстуру — кандидаты для F-006, ещё не импортированные. D-014/D-015 остаются планом: [дополнение GDD](2026-10-06-kitchen-workspace-gd-spec.md).

### Структура проекта

Собственный контент будет размещаться в `Assets/_ChefShow/` по мере разработки. Документы остаются вне Assets. На первом этапе достаточно Runtime и Editor сборок, с отдельными тестовыми сборками при появлении проверяемой логики. Runtime не зависит от UnityEditor. Имена и файловая схема заданы технической инструкцией, не навыком гейм-дизайна.

Generated-сцена: `Assets/_ChefShow/Generated/Scenes/ChefShow_Prototype_Generated.unity`. Рабочая копия автора: `Assets/_ChefShow/Scenes/ChefShow_Prototype.unity`. Builder создаёт assets через Editor API, выполняется явной командой и сохраняет ручную рабочую копию и GUID. Не модифицировать SampleScene ради стартового прототипа.

## Владельцы данных и зависимости

### Ящики и две руки — фактический срез F-005/D-018, 7 октября 2026

`ToolDrawerInstaller` через Editor API/MCP обновляет working/generated с резервными копиями, сохраняя панель, геометрию и ручную камеру. Ящики/48 ячеек/48 исходных инструментов доступны в Hierarchy, Preview Open работает без Play. D-018 заменяет исторические меню D-016/D-017: меню/дублирующие представления рук удалены через Undo, runtime не создаёт объектов.

`KitchenTool` содержит Kind, ссылку на свой ящик, Rigidbody и PickupCollider. Единственный исходный объект находится в ячейке, правой руке либо мире после G. Смена возвращает прежний инструмент домой; E после падения поднимает тот же экземпляр. Кинематические инструменты не используют физическую интерполяцию, чтобы поза в руке следовала камере. Пауза/timeup замораживают упавшие тела; Restart восстанавливает ячейки.

`ToolDrawerController` использует текущую карту Input System и повторный raycast, отклоняет чужую станцию и конфликт левой руки с корзиной. `GameBootstrap` маршрутизирует E/G сначала инструментам, затем оставшиеся команды Inventory; открытый ящик не отключает Gameplay/Station и не создаёт UI-контекст. Панель и `ToolDrawerTarget` на свободном дне — доступные trigger-цели, которые не толкают капсулу. FirstPersonRig допускает эти trigger-цели, сохраняя стены и исключение предметов в руках. MaxDownPitch=85 для обычного вида и фокуса.

`InventoryState` продолжает владеть одной FoodPortion в Hand; сторона руки — её представление, не вторая копия еды. `InventoryController.PresentFoodHand` перемещает сохранённый FoodDisplay в LeftFoodHand при наличии инструмента, возвращает справа после G; ID/источник/количество не меняются, E/RMB сохраняют прежние контракты. KitchenToolChanged — снимок факта выбора; инструменты не становятся едой. G отдельной еды, нарезка/распаковка/нагрев не добавлены.

HUD показывает сохранённую `Interaction Key E` с пульсацией и русское действие отдельно. По уточнению автора оба текста находятся над прицелом: E +42, название +78 единиц Canvas, шрифт 18; центрированные anchors прицела/подсказок сохраняют взаимное положение при смене размера окна. `InstallPromptLayout` обновляет только эти RectTransform/Text в working/generated через Editor API. Canvas находится перед мешами в руках, чтобы они не закрывали текст. Play Mode 15/15 PASS; полный пищевой цикл F-005 и Windows build остаются впереди.

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

### Модель порций F-005 — 7 октября 2026, следующий срез

`FoodPortion` вынесена из InventoryState в отдельный файл. У каждой записи собственные подготовка/нагрев, количество (для ещё не открытой упаковки — один контейнер), дозы и загрязнение; исходные Whole/Raw не являются реализованной готовкой. Происхождение и история действий доступны только для чтения. InventoryState остаётся владельцем операций и записывает перенос/выгрузку/возврат/утилизацию со временем GameClock. FoodPortionSnapshot копирует значения, ID ингредиента и массивы; последующие изменения записи/definition не меняют снимок. Нарезка/распаковка и изменение тепловых полей пока не подключены, Q-017 ожидает ответа. Новых GameObject/MonoBehaviour на этом шаге не требуется; существующая рабочая сцена/ручная камера сохранены. EditMode 8/8 и регрессия Play Mode 15/15 PASS; полная F-005 не завершена.

### Нарезка D-019 — фактический срез 7 октября 2026

InventoryState.TryChop принимает выбранный KitchenToolKind; проверяет активность попытки, свою единственную board-порцию, совместимость и свободную руку, считает шесть отдельных команд без ритма. ChopPresses принадлежат FoodPortion и входят в FoodPortionSnapshot; шестая команда меняет только Preparation на Chopped. Перед публикацией модель записывает операцию/версию и создаёт PreparationChanged со значениями run/round/actor/team/time/IDs/progress/quantity/preparation/cooking. Завершение повторно не выполняется даже из callback. Начальные тепловые поля/дозы пока не получают игровых операций F-006.

GameBootstrap.Update: Player → Tools (E/G priority) → Preparation → Inventory → HUD. PreparationController повторно проверяет реальную цель ChoppingBoard и выбранный нож, использует GameClock.Delta для движения единственного исходного KitchenTool. Смена/G отменяют управление его позой, pause/timeup не продолжают анимацию. InventoryController теперь принимает сбор/выгрузку/перенос по E, Primary переносу не передаётся; Tab/G/RMB/Q сохранены. Семь Renderer/Cube без Collider заранее сохранены для каждого из 37 переносимых FoodDisplay (259 на сцену); представление меняется без runtime GameObject/новой порции, состояние после переноса сохраняется.

На Systems — PreparationController/Boards; на 12 Inventory/Board — ChoppingBoard с Target/Caption/KnifeContactPoint. Надпись доски у края, прогресс и E/русское действие — в HUD над прицелом. FoodPreparationInstaller делает узкую установку working/generated через MCP/Editor API с guards, копиями/Undo/Save; builder учитывает срез при будущей явной генерации. Геометрия станций и камеры/GUID сохранены. Working/generated Validate PASS: 12 досок/37 views/259 активных объектов кусочков (Renderer выключен при пустой порции), 48 инструментов, hidden0, dirtyFalse; камеры2.20/1.65.

Для фоновых Input-тестов GameBootstrap.AutoPauseOnFocusLoss имеет временное runtime override=False в fixture; default=True в обычной игре/сценах. Это изоляция смены окна Editor, без изменения ручной паузы/Esc/таймера/asset settings. Временные InputSettings clone/device filter сохраняются и восстанавливаются fixture. Проверено: Edit 10/10, Play 17/17 PASS 10:55:40–10:56:17 UTC. Распаковка/нагрев/полный выпуск не реализованы этим срезом.

### Распаковка D-020 — архитектура первого среза (место открытия заменено D-021)

IngredientDefinition.ContentsQuantity на двух упаковках (5/6), IngredientCatalog.Validate проверяет допустимое содержимое/количество. FoodPortion фиксирует PackedIngredient/PackedQuantity при сборе; snapshot копирует значения. InventoryState.TryUnpack проверяет активность/свободную руку/WorkSurface/места в лотке, создаёт N отдельных FoodPortion, копирует историю/загрязнение и FoodOrigin(package.Id, contents.Id, 1), переводит упаковку в Unpacked и освобождает socket. Данные/Version меняются полностью до публикации immutable PackageUnpacked со snapshots; callback не может повторить открытие.

PreparationController направляет Primary на доску (TryChop) либо своё место продукта (TryUnpack). E-пререносы InventoryController сохранены, инструменты для открытия не обязательны. Sync по Version показывает продукты через уже сохранённые FoodDisplay; runtime геометрия не создаётся. PackagePreparationInstaller адресно сохраняет defs/подсказки/подпись у края в working/generated с backups/Undo/guards; builder учитывает будущую явную генерацию. Сцены/GUID/ручная камера2.20 сохранены. F-005 verified: 14Edit/19Play PASS. Оба fixture Play-тестов используют изолированный synthetic input и временное AutoPauseOnFocusLoss=False; стандарт игры остаётся True. Нагрев — следующая F-006.

### Распаковка в лотке и чистая зона — D-021

`InventoryState.TryUnpack(int index, out reason)` читает упаковку прямо из Tray[index], проверяет активную попытку/свободную пищевую руку/состояние упаковки и N−1 дополнительных мест. После проверки создаёт N отдельных порций с прежним происхождением/историей/загрязнением, архивирует родителя Unpacked, выполняет RemoveAt/InsertRange в том же индексе, затем Version/immutable PackageUnpacked. Socket больше не участвует; порядок соседних предметов и повторный callback защищены.

PreparationController направляет Primary на доску либо конкретный TrayItem своей станции с упаковкой. Для обычного содержимого ЛКМ ничего не берёт/не режет; E остаётся переносом. Сообщение об открытии привязано к TrayItem и сочетается с текущей E-подсказкой нового содержимого. InventoryState.IsReadyForServing допускает только чистую еду с завершённым состоянием нагрева, без упаковки/дозового контейнера. TryPlaceSocket(WorkSurface) отвергает остальные состояния без изменения руки/Version; HUD поясняет назначение зоны.

Существующий Work Surface (Index1) сохранил ссылки/позицию, DisplayName/Label стали «Готовое блюдо» на всех12 станциях. PackagePreparationInstaller сохраняет обе сцены через Editor API/Undo/guards; будущий InventorySceneInstaller использует новую подпись. Никаких новых игровых GameObject/HideFlags. Edit16/16, Play19/19 PASS, обе сцены Validate/dirtyFalse; камеры2.20/1.65/GUID сохранены. Реальное нагревание и тарелки ещё относятся к F-006/F-007. Инъекция Cooked и contamination в модельных тестах проверяет правила размещения, не готовку.

### Нагрев и расширение станции — D-022

`CookingConfig` — сохранённый ScriptableObject; `Capture()` создаёт независимый CookingSettings при запуске попытки. InventoryState остаётся единственным владельцем FoodPortion, включая две очереди приборов и режимы нагрева. PortionLocation.Appliance добавлен в конец enum; отмена переноса восстанавливает прежний прибор/индекс. В FoodPortion/Snapshot сохранены HeatProgress, LastCooker, StirPresses, RequiredStirs; подготовка/ID/количество/история/загрязнение независимы от нагрева.

PrototypeRun.Tick ограничивает последний simulation delta остатком таймера, затем Inventory.TickCooking применяет скорости текущих режимов. Все изменения порций/Version фиксируются до CookingChanged callbacks; immutable факты cook_started, food_state_changed, food_burned, heat_changed, food_stirred содержат run/actor/team/time. Burned необратим, его факт один раз. Снятая еда отсутствует в списке нагрева. Пауза/timeout/disposal блокируют команды; при timeout ящик завершает закрытие сразу, поскольку игровое время больше не движется. Настоящие seasoning/mixture/food_perfect/реплики — будущие срезы.

GameBootstrap обрабатывает Tools → Cooking → Preparation/Inventory, предотвращая двойное действие E/ЛКМ. CookingTarget различает vessel/food/knob; чужая станция отклоняется. FirstPersonRig проверяет индивидуальную дальность каждой цели после общего raycast, включая препятствия: default3.1м, только cooking targets4.2м. HUD использует зафиксированный профиль текущей попытки, а не изменённый во время Play asset. CookingStation обновляет только сохранённые FoodDisplay/ссылки/PropertyBlock/Smoke; runtime кухня не создаётся. Лопатка анимируется тем же исходным KitchenTool, сохраняет pause/drop/reset.

CookingSceneInstaller применяет адресную установку с guards/Undo/backups в clean working/generated. Расширение стола сохраняет мировые позиции/повороты/масштабы всех прежних дочерних групп. Native Editor API создаёт CookingConfig, материалы и24 прибора/120 целей/72 FoodDisplay/504 Cut Piece. Пустые картинки/дым имеют disabled Renderer, объекты активны и доступны Inspector. GUID сцен/мета сохранены, авто-regenerate отсутствует. Геометрия/радиус требуют явного installer после сохранения; тепловые настройки применяются при Restart. Builder содержит лишь hook для будущей явно запрошенной генерации.

D-022/T-032: Edit22/22 и вся Play-сборка22/22 PASS; дополнительный профиль/HUD pan-тест1/1 PASS. Все12 столов3.6×2.3м/верх0.9, hidden0/dirtyFalse, камераworking2.20/generated1.65, капсула1.8. Полный B-инвентарь, миска/духовка/дозы/посуда/Windows build не входят в проверенный срез.

### Ширина станции и правая колонка — D-023

Пространственный override к D-022: мироваяZ теперь ширина3.6, X глубина2.3. CookingConfig.TableWidth мигрирует изTableLength через FormerlySerializedAs, TableDepth2.3; GUID asset прежний, тепловой capture не изменён. CookingSceneInstaller.ArrangeRows фиксирует первыйz и вычисляет центры с gap.2, перемещает совпадающие StableId актёров вместе со столами. ResizeTable сохраняет внешнийXкрай/повороты/мировые масштабы детей; существующие две колонки кухни сдвигает влево на половину добавленной ширины. Cooking и Tool Drawer Contents исключены из этого сдвига; исходные24 CookingStation перемещаются в правую колонку абсолютными мировыми точками, ящики сохраняют центр. Renderer/MonoBehaviour/FoodSlot/ссылки/мета/факты не пересоздаются.

FocusPoint теперь в свободной щели между подготовкой и приборами, FirstPersonRig.FocusYawRange80 даёт широкой станции доступ к обоим краям. Высота2.20/1.65, FOV60/52, offset/пауза/cancel/радиусы сохранены. Initial-test камера наводится на реальную свободную цель, foreign drawer fixture использует относительное место новой станции, длинные end-routes огибают также Pantry. Игровые команды/модель нагрева не изменены. Нативная установка/сохранение guard/Undo/backups, без регенерации или YAML; сохраняется только изменённый собственный CookingConfig через SaveAssetIfDirty.

D-023/T-033: Edit22/22, Play22/22 PASS, обе сцены Validate/dirtyFalse/hidden0. Все24 исходных прибора/48 инструментов/101 цели инвентаря сохранили ссылки; новая трансляция зон соответствует запросу. Сценовые GUID/камера/763 Cut Piece прежние. Полный B-инвентарь/Windows build не проверены этим срезом.

### Дискретные дозы — D-024

CookingSettings захватывает DosesPerPress и отдельные ID соли/масла из CookingConfig при запуске попытки. InventoryState.TryApplySeasoning проверяет активность, удерживаемый контейнер, прибор и конкретный индекс. Меняет только выбранную порцию, сохраняет загрязнение источника через OR, записывает истории еды/контейнера, Version и затем immutable SeasoningApplied (контекст, тип/дозы, снимки источника/цели). Контейнер остаётся отдельным предметом Quantity1 в Hand; число порций не меняется. Счётчики SaltDoses/OilDoses уже принадлежали FoodPortion/Snapshot, поэтому перенос/отмена сохраняют их.

CookingController направляет Primary на дозирование только при удерживаемом дозовом контейнере и реальном CookingTarget Food; Vessel/ручка не выбирают произвольную первую порцию. В остальных случаях прежнее перемешивание/инструменты сохранены. E продолжает перенос/ручку. HUD показывает реальные счётчики конкретной еды и ЛКМ; E-значок для этого действия выключен. Native InstallHud обновляет только Q/Controls в working/generated; собственный конфиг сохраняется SaveAssetIfDirty. Никаких новых runtime/hidden игровых объектов. Геометрия D-023/GUID/2.20/дальность/AutoPause сохранены.

T-034: Edit25/25, Play24/24 PASS. После TestRunner EditorOptions1 точечно восстановлено 0; ошибок Console нет. Миска/духовка и judging ещё впереди.

## Реализованная поправка D-025/D-026 — 2026-10-09

CookingSettings.Accepts проверяет вид прибора и отдельный пищевой ingredient без Contents/IsDoseContainer; списки рецепта и обязательная нарезка Pot удалены из переноса. TryPlaceCooker отдельно сообщает о пакете/контейнере, сохраняет capacity/access/таймер. CanPlaceOnServingSurface разрешает съедобную порцию независимо от качества; IsReadyForServing остаётся отдельным качественным признаком. История последовательных способов/смеси ещё требуют расширения F-006.

GameBootstrap.Player.CancelInteraction сначала вызывает ToolDrawerController.ReturnAimedTool, затем Inventory.CancelHeld; потреблённый ПКМ не отменяет продукт/фокус. ToolDrawerTarget.CompartmentIndex=-1 для панели/дна,0…3 для Cell Bottom. В пустой ячейке trigger/PrototypeInteractable сохранены; занятая ячейка определяется исходным KitchenTool. Own/open-ready/home-kind проверки предотвращают потери. Возвращается тот же объект, после него еда показывается справа. HUD выбирает [E]/[ПКМ] и русский текст над прицелом.

Native InstallToolReturns добавил48×3 компонентов на существующие Cell Bottom каждой сцены; новые игровые GameObjects/Transforms не создавались. Все2248 исходных GameObjects и2248 Transform/RectTransform записи каждой сцены сохранены; all original fileIDs present. GUID/геометрия/камера/HideFlags прежние. **EditMode 26/26 и Play Mode 27/27 PASS**; T-035. [Edit XML](../TestResults/free-cooking-editmode-final.xml), [Play XML](../TestResults/free-cooking-playmode-final.xml), [Edit proof](../TestResults/free-cooking-edit-proof.json). Временные TestRunner EditorOptions восстановлены0, AutoPauseTrue.

## D-028/D-029 — выявленная граница перед ранним срезом F-007

По фактическому коду на7c416cc передняяWorkSurface — один socket: занятое место отклоняет вторую порцию. Дозирование выбирает только CookingTarget/Food, поэтому тарелка не поддерживает соль/масло. Новое целевое правило — много реальных порций с горкой и пост-добавки, без подмены состава/состояния. Архитектурные решения нового среза ещё не реализованы; Q-018 определяет цель/учёт дозы, Q-014 предел переполнения. Источник количества — порции/их происхождение, не число отображённых кусочков. Четыре serving roles будущего дизайна не означают жёсткие четыре элемента коллекции. Технический план должен сохранить один owner еды, атомарный перенос, независимый snapshot и видимый вEditModeконтент. Текущий игровойкод/сцены этой правкой документации не меняются.

## D-032 — миска и духовка у стены, 9 октября 2026

Через MCP сохранены12 MixingStation и12 Oven (всего36 CookingStation). Миска между рабочими зонами, столы/щели/камера2.20 прежние; духовкиx±28.7,z своего участника за рядами возле боковых стен. Входы смешиваются3с удержанияПКМ ложкой/лопаткой: новый FoodPortion имеет Quantity суммы, Components неизменяемых снимков и OriginComponents; исходные записи архивируются какMixed ровно один раз. Загрязнение/дозы/сгорание сохраняются. Обычный снятый продукт/смесь можно переносить между приборами/лотком/тарелкой.

Oven — одна сохранённая форма/порция; открытая дверца блокирует нагрев, закрытая — помещение/съём. Нагрев отдельный,45/60/75с на среднем. Перенос и пауза не сбрасывают еду, дым followsBurned, Restart открывает/очищает. CookingConfig/ссылки видны в Inspector.

EditMode36/36 PASS, адресные физические PlayMode2/2 PASS: смесь четырёх ингредиентов→форма→закрыть/нагреть→открыть/снять→тарелка; отдельное подгорание/пауза/reset. XML TestResults/bowl-oven-editmode.xml иbowl-oven-playmode.xml, кадрыwall-oven-ready.png/wall-oven-burned.png. Полная регрессия ввода ещё проверяется. Это проверка текущего старта четырёх приборов, не готовность полного выпуска/двенадцати рецептов.

### Владельцы еды после D-030…D-032

InventoryState.Served владеет всеми порциями на тарелке; Socket(1) возвращает верхнюю порцию только для совместимости прежних вызовов. Для целевого снятия используется TryTakeServing(index). DishSnapshot копирует Portions и общиеPlateSaltDoses/PlateOilDoses/PlateContaminated. Общую дозу нельзя ещё раз суммировать по каждой порции. Submit/00:00 фиксирует тарелку; при прямом Submit остаток игры пока остаётся кухонным прототипом, judging не запускается.

InventoryState.Bowl владеет входами; Mixed — архив исходных записей. После смешивания только новая порция активна, Quantity=sum входов; Components — их независимые снимки на момент смешивания, OriginComponents — исходные единицы. Для распознавания/оценки не считать Quantity смеси иComponents дважды. Нагрев смеси относится к её внешнему CookState/LastCooker; до смешивания сохранены отдельные состояния входов. Burned/contamination не исправляются смешиванием.

CookerKindPan0/Pot1 сохранены, Oven2 добавлен; InventoryCooking имеет3 массива приборов. OvenDoorOpen находится в данных попытки. CookingSettings захватывает3/6/1 вместимость и30/45/60 или45/60/75; данные asset не меняются при Play.

Новый порядок ввода: Tools→Cooking→Mixing→Serving→Preparation→Inventory; каждый обработчик возвращает consumed, один клик имеет одного владельца. Interact теперьF (фокус), PrimaryЛКМ, SecondaryПКМ, CancelBackspace. ПКМ по реальному другому инструменту меняет инструмент; толькоПКМ по ячейке означает возврат. Упаковка берётсяЛКМ, открываетсяПКМ в лотке.

## T-039/T-040 — новая компоновка и выбор посуды

T-039: на обеих командах духовки имеют мировой масштаб2 и стоят около приближённых стен; рабочие столы/участники не передвинуты. Корпусы не пересекают стены, увеличенная дверца открывается, форма/ручка доступны. Физический CharacterController проходит за спинами NPC и обходит первый/шестой стол на всех четырёх маршрутах; межстоловые щели по-прежнему непроходимы.

T-040: два длинных стола/60 сохранённых целей/5 профилей видны в Edit Mode. Физический ЛКМ берёт выбранную посуду слева, ставит на своё место блюда и меняет наполненную посуду без потерь/дубликатов. Проверить каждый профиль, возвращение Backspace, соседнюю/чужую команду, паузу, Submit/00:00, занятость левой руки корзиной/едой, мягкое переполнение и Restart. Снимок хранит профиль/количество/консистенцию-совместимость/дозы независимо от последующих изменений asset. Очки и жидкости пока не реализованы.

**EditMode 41/41 и полная Play Mode 34/34 PASS** (10 октября 2026). Итоговый Edit: 09:37:04–09:37:05 UTC, TestResults/dishware-final-editmode.xml; Play: 09:32:43–09:35:07 UTC, job `2dc7315077ea46ffafc6e57d38a0ad22`, TestResults/dishware-full-playmode.xml. Адресный физический прогон 4/4 также пройден. Проверены все пять вариантов, смена наполненной тарелки с сохранением мяса/яйца/доз, занятость левой руки, чужая команда, пауза, Restart, неизменяемый снимок и мягкое переполнение; реальная смесь проходит увеличенную духовку, все четыре обхода сохранены.

Обе сохранённые сцены Validate PASS, dirty=False/hidden=0, EditorOptions=0/AutoPause=True. На каждой 9771 GameObject, 60 DishwareTarget, 5 профилей, 36 приборов/12 тарелок/12 мисок/48 инструментов/101 прежняя InventoryInteractable. Все 8705 исходных объектов и сериализованных записей сохранены, прежние .meta/GUID не менялись. Из исходных поз изменены только Floor/4 стены/12 Oven/12 Plate/Status; камера working2.20/generated1.65, участники, рабочие столы, щели и ручное освещение сохранены. Доказательства: TestResults/dishware-final-scene-proof.txt и dishware-preservation.json. После просьбы Ctrl+S получено подтверждение автора; перед установкой обе сцены были чистыми, сохранены резервные копии в TestResults/dishware-compact-before-*.

## Контракты посуды D-034

DishwareConfig и пять DishwareDefinition — сохранённые ScriptableObject. При Restart конфиги захватываются в неизменяемые DishwareSettings/DishwareSnapshot; изменённый в ходе выпуска asset не меняет текущую посуду/цели. InventoryState владеет CurrentDishware/HeldDishware; посуда занимает левую руку вместе с едой/корзиной взаимно исключительно. Смена меняет только профиль, не Served/ID/Quantity/дозы/историю. PlateFillRatio = сумма Quantity / NominalCapacity; части нарезки не умножают количество. DishSnapshot содержит профиль и заполнение на момент Submit/00:00.

DishwareController обслуживает ЛКМ до Cooking/Mixing/Serving/Preparation/Inventory с consumed-guard; действие на источнике не проходит дальше. 60 источников и Held Dishware сохранены в сцене, DishwareView меняет существующие sphere/rim renderers. Исходная Plate сохранена; формы разных видов собираются из плоской сферы и 12 частей бортика. Материалы источников — настоящие assets, цвета видны после переоткрытия сцены. Переполнение не уничтожает еду, существующие 48 FoodDisplay показывают горку.

KitchenLayoutConfig задаёт стены/масштаб духовок; CompactKitchenInstaller и DishwareInstaller работают адресно через Editor API с preflight/Undo/backup/Validate/save обеих сцен. Автоматической генерации кухни при Play и HideFlags нет.

## Контракты D-035/D-036

RemoveInstalledDishware атомарно архивирует Served как Trash, очищает общие приправы/загрязнение, снимает CurrentDishware и увеличивает PresentationPenalty до callbacks. TryTakePlacedDishware оставляет профиль в HeldDishware; TryPlaceDishware штрафует только при существующей CurrentDishware. Nullable CurrentDishware/DishSnapshot.Dishware и FillRatio0 позволяют пустую автоподачу; без посуды еда отклоняется. Snapshot.PresentationPenalty — неизменяемый вычет для F-010.

ServingController различает Submit, порцию Index≥0 и посуду Index<0. ServingStation показывает штраф и управляет сохранёнными SubmitButton/SubmitLight; MaterialPropertyBlock даёт эмиссию после SubmittedDish, Point Light красный. Ненулевая базовая эмиссия сохраняет URP keyword, runtime доSubmit задаёт0. Restart сбрасывает данные/свет. Порядок ввода/consumed guard сохранён.

KitchenLayoutConfig.ArenaCenterZ задаёт асимметричные торцы. SubmissionRulesInstaller работает адресно с backup/preflight/Undo/Validate/save без rebuild; generated WorkSurface подняты до tabletop+.03, рабочие позы автора сохранены. Edit43/Play35/материал1 PASS, результаты в implementation-log.

## D-037…D-039 — полки, сброс и справочные профили

DishwareShelfInstaller перепрофилирует два старых корня/три детали лавок в первые полки, переносит60 прежних DishwareTarget к12 духовкам через Editor API и добавляет оставшиеся уровни/опоры. Старые объекты/профили/GUID сохранены; геометрия вне лавок не меняется. SubmissionResetInstaller добавляет12 сохранённых ServingTarget.ResetSubmission. InventoryState.TryResetSubmission очищает только SubmittedDish до DishChanged; Active/station/acceptInput guards защищают состояние, old snapshot immutable.

RecipeDefinition и RecipeBookCatalog — реальные ScriptableObject; Capture при Restart копирует строки/количества/этапы в read-only RecipePage. Gameplay-catalog/распознаватель и judging не подключаются к справочнику. GameBootstrap.RecipeBook ссылается на сохранённый UI/Recipe Book; RecipeBookController показывает12 страниц/постоянный выбранный список. RecipeIconGraphic строит картинки через VertexHelper/CanvasRenderer, без внешних текстур; пиктограммы Pan/Pot/Oven/Knife/Mix различимы. Installer создаёт UI/assets через Editor API, сохраняет исходный GUID InputDefinition и добавляет только UI/RecipeBook(I), UI/RecipeRightHeld.

Modal barrier отключает Gameplay/Station и передаёт acceptInput=False всем контроллерам; UI map включается целиком, поскольку UI module включает только используемые им отдельные actions. Run.Tick продолжает нагрев/таймер; после закрытия barrier ждёт отпускания обеих кнопок и минимум1frame. Курсор UI при книге/паузе, иначе locked. Выбор не меняет TaskCard/Run; ResetRun очищает только выбор справочника. UI inactive в сохранённой сцене, все элементы/ссылки существуют в Edit Mode. Никаких новых скрытых runtime-сущностей кухни.
