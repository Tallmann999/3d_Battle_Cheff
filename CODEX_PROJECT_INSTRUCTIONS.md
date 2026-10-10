# CODEX_PROJECT_INSTRUCTIONS — Unity 6 URP прототип кулинарного шоу

Версия: 0.1. Дата: 5 октября 2026. Назначение: инструкция агенту разработки после предоставления автором папки Unity-проекта.

Положить в корень проекта рядом с [GAME_CONCEPT.md](GAME_CONCEPT.md). Этот документ описывает будущую реализацию; наличие файлов не означает, что проект, сцена или код уже созданы и проверены.

## 1. Задача агента и порядок источников

Создать редактируемый в Unity однопользовательский first-person прототип одного кулинарного выпуска: две команды по шесть, три раунда 6→4→2 в каждой команде, смена судей во втором раунде, четыре анонимных финальных блюда и раскрытие победителя. Главная система — реакции двух оригинальных шефов на фактические события готовки.

При начале работы читать:

1. Текущие прямые инструкции автора и применимые `AGENTS.md`.
2. `GAME_CONCEPT.md`: механики, scope, принятые правила и отмеченные предположения.
3. Этот файл: техническая организация и порядок реализации.
4. `docs/feature-index.md`, карточку текущей фичи, `docs/decisions.md`, `docs/architecture.md`, если они существуют.
5. Реальный проект: настройки, установленные пакеты, существующие сцены, префабы и код.

Документ не является автоматически обнаруживаемым именем инструкций Codex. В первом запросе явно попросить прочитать оба корневых файла. Не рассчитывать, что агент сам применит их при любом запуске.

При конфликте кода и документов описать расхождение и обновить затронутый документ после решения. Новое прямое указание автора имеет приоритет. Не переносить в проект неподтверждённые юридические выводы, рыночную статистику или реальные имена ведущих из старых обсуждений.

## 2. Обязательный workflow: концепт → вопросы → Feature Index → код

Автор хочет сначала обсудить концепт с GDS/GDD-скиллом и зафиксировать фичи. На первом входе не создавать сразу весь gameplay.

### 2.1. Дизайн-разбор

Проверить, что идея уже есть в `GAME_CONCEPT.md`; не проводить повторное интервью «придумай игру с нуля». Обсудить компактным пакетом 3–5 вопросов решения, которые влияют на ближайшую реализацию: управление станции, состав трёх блюд, поведение подачи на 00:00, анонимность для игрока и критерии оценки.

Остальные TBD оставить в списке, если они не блокируют этап. Если автор прямо разрешит использовать предложенные значения, записать это как принятые предположения и продолжать. Не запрашивать повторное одобрение уже согласованного scope.

Гейм-дизайнер отвечает за поведение и критерии; техническая структура этого файла — инструкция агенту реализации. Технические предложения допустимо уточнить под существующий проект, сохраняя scope и объясняя причины.

### 2.2. Документы, которые создаются внутри будущего проекта

Базовый канонический набор:

```text
<project_root>/
  GAME_CONCEPT.md
  CODEX_PROJECT_INSTRUCTIONS.md
  docs/
    project-concept.md       # краткая сводка со ссылкой на полный корневой GDD
    feature-index.md         # единственный канонический индекс фич
    decisions.md             # принятые решения, основания, последствия
    architecture.md          # фактические границы систем и их зависимости
    implementation-log.md    # что сделано, проверено и что осталось
    test-plan.md             # сценарии ручной/автоматической проверки
    features/
      F-001-foundation.md
      ...
```

Эти вспомогательные документы создавать после дизайн-разбора, а не считать уже существующими. `project-concept.md` — краткая навигационная сводка; не копировать в него независимую полную версию GDD.

Если в реальном проекте уже есть `FEATURE_INDEX.md` в корне или `ai-docs/feature-index.md`, использовать существующий индекс и зафиксировать путь. Не заводить второй изменяемый индекс. Предпочтительное имя для нового проекта — `docs/feature-index.md`; выражение «Feature Index» обозначает этот документ независимо от регистра имени.

### 2.3. Формат Feature Index

Колонки: `ID | название | приоритет | статус | зависимости | владелец | спецификация | критерий готовности | проверка`.

Приоритеты: `MVP`, `P1`, `P2`. Статусы: `proposed → specified → ready → in_progress → implemented → verified`. Дополнительно `blocked`, `deferred`, с явной причиной. `implemented` означает написано; `verified` означает проверено нужным способом в Unity/сборке, с записанным результатом.

Начальные F-001…F-017 брать из раздела scope GDD, сохраняя ID. F-013 (debug/UI/restart) интегрировать постепенно; F-014 (проверки) начинать с первого среза. Не ждать окончания всех систем, чтобы добавить способ проверки.

Карточка фичи содержит: проблему и цель, игровое поведение, in/out scope, зависимости, данные и параметры, события, UI, негативные случаи, acceptance criteria, способ проверки, TBD. Обязательно объяснить, почему выбран такой объём. В техническом приложении указать затронутые файлы и сервисы.

### 2.4. Цикл одной фичи

Прочитать её карточку → уточнить только блокирующие вопросы → зафиксировать решение → реализовать небольшой сквозной результат → проверить компиляцию и сценарий в Unity → исправить → обновить статус и лог → показать автору, как проверить результат руками.

Не завершать этап только списком новых классов. Нужен видимый результат в Play Mode. Не начинать P1/P2, пока MVP не проверен или автор не изменил приоритет. Изменение scope обновляет GDD, индекс и связанные карточки.

## 3. Осмотр реального проекта перед изменениями

Убедиться, что предоставленная папка действительно корень Unity: есть `Assets`, `Packages`, `ProjectSettings`. Если их нет, это ещё не проект; не имитировать готовый Unity-проект набором произвольных YAML-файлов. Сначала сообщить, что нужно создать проект из URP-шаблона или использовать доступный проверенный механизм создания.

Прочитать `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `Packages/packages-lock.json`, текущую структуру `Assets` и настройки рендера/input. Проверить локальные правила, состояние Git и незавершённые изменения. Не перезаписывать пользовательский код и сцены по совпавшему имени.

Записать фактическую версию Editor и пакетов в `docs/architecture.md`. Под «Unity 6» понимается семейство 6000.x; конкретную минорную версию определяет созданный автором проект. Не обновлять Editor и пакеты до «самых новых» ради этого документа.

## 4. Техническая база и пакеты

- Рендер: **URP**, исходный шаблон Universal 3D. Использовать существующие Pipeline Asset и Renderer; проверить привязку в Graphics и используемых Quality-настройках.
- Ввод: **Unity Input System**, один набор action maps, без параллельной реализации через старый `Input.GetKey`.
- UI MVP: uGUI Canvas и текст с поддержкой кириллицы; предпочтительно TextMeshPro, если доступен в проекте.
- Контроллер: `CharacterController`, без обязательного стороннего FPS-пакета.
- Камера: собственный лёгкий first-person rig. Cinemachine не обязателен.
- NPC/шефы: маршруты по точкам. AI Navigation/NavMesh вводить только при реальной необходимости и после проверки совместимости.
- Проверки: Unity Test Framework для чистой логики и важных переходов, когда этап дошёл до тестируемой системы.
- Контент: примитивы, URP-материалы, собственные тексты. Платные ассеты, внешние сервисы и Asset Store-зависимости не обязательны.

Добавлять только отсутствующие необходимые пакеты, версиями, совместимыми с текущим Editor. Сохранять manifest и lock-файл. Не фиксировать выдуманные номера пакетов в документации. Официальная страница пакета позволяет проверять совместимость для выбранной версии: [Unity Input System для Unity 6.0](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.inputsystem.html).

Для uGUI использовать один `EventSystem` с `InputSystemUIInputModule`, если UI управляется Input System. Не оставлять одновременно второй EventSystem или старый StandaloneInputModule. Настройку action maps UI проверить по установленной версии пакета: [официальная документация UI Support](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/UISupport.html).

URP-материалы создавать совместимым shader, например `Universal Render Pipeline/Lit`; при отсутствии shader вывести ошибку настройки, не сохранять розовые материалы как готовую сцену. Шрифтовый asset должен включать кириллицу; не рассчитывать на то, что любой встроенный шрифт её содержит.

## 5. Структура папок

Весь собственный игровой контент находится в `Assets/_ChefShow/`. Корневые Markdown и `docs` лежат рядом с `Assets`, не внутри него.

```text
Assets/
  _ChefShow/
    Scenes/
      ChefShow_Prototype.unity        # рабочая сцена автора
    Scripts/
      Runtime/
        ChefShow.Runtime.asmdef
        Core/                        # запуск, время, события, жизненный цикл
        Data/                        # определения ScriptableObject
        Flow/                        # состояния выпуска и раундов
        Player/                      # контроллер, прицел, input, focus
        Inventory/                   # корзина, лоток, перенос
        Ingredients/                 # экземпляры порций и визуализация
        Cooking/                     # станции и процесс готовки
        Recipes/                     # рецепты и распознавание блюда
        Contestants/                 # состав, визуальные NPC, симуляция
        Chefs/                       # движение, реакции, реплики
        Judging/                     # scoring, elimination, финал
        Presentation/                # камеры сцен, звук, эффекты
        UI/                          # HUD и экраны
        Debug/                       # команды разработчика
      Editor/
        ChefShow.Editor.asmdef
        Builders/
        Validators/
        Inspectors/
    Data/                            # вручную настраиваемые .asset
      Configs/
      Ingredients/
      Recipes/
      Challenges/
      Rounds/
      Chefs/
      Reactions/
      Contestants/
      Localization/
    Prefabs/
      Player/
      Stations/
      Ingredients/
      Characters/
      Judging/
      UI/
    Art/
      Materials/
      Models/
      Textures/
    Audio/
      SFX/
      Music/
      Voice/
    Input/
      ChefShowInput.inputactions
    Generated/                       # только регенерируемые результаты Builder
      Scenes/
        ChefShow_Prototype_Generated.unity
      Data/
      Prefabs/
      Materials/
    Tests/
      EditMode/
        ChefShow.Tests.EditMode.asmdef
      PlayMode/
        ChefShow.Tests.PlayMode.asmdef
    Gizmos/
```

`Scripts/Runtime/Data` — C#-определения, `Data` — пользовательские экземпляры assets. `Generated` не смешивается с авторским контентом. Скрипты Builder хранятся вне `Generated`, иначе регенерация могла бы удалить свой инструмент.

Не создавать все подпапки ради пустой структуры; заводить их по мере соответствующего этапа. Не хранить собственную логику в `Resources` без конкретной причины. Не вводить Addressables, ECS/DOTS, сетевой стек или контейнер DI для такого MVP.

## 6. Сборки, стиль и сохранность Unity-assets

На первом этапе достаточно одной runtime-сборки и одной editor-сборки. `ChefShow.Editor` с платформой Editor ссылается на Runtime; Runtime не ссылается на Editor. Тестовые сборки подключаются отдельно и не входят в обычный player build. Assembly Definitions задают границы компиляции и ссылки между сборками: [Unity: Introduction to assemblies](https://docs.unity3d.com/6000.0/Documentation/Manual/assembly-definitions-intro.html).

Не дробить каждую папку на asmdef заранее. Если позднее выделяется чистый Domain, зависимости направлять от Unity-адаптеров к Domain, без циклов. Для Input System и UI указать реальные package assembly references, если используются asmdef.

Namespace: `ChefShow.<System>`. Один основной MonoBehaviour/ScriptableObject-класс на файл с совпадающим именем. Публичные типы и методы — PascalCase; приватные поля — стиль, уже принятый в проекте; дизайнерские ID/баланс-ключи — snake_case. `[SerializeField]`, `[Tooltip]`, `[Header]`, проверки диапазонов делают настройки понятными в Inspector.

Правила работы с assets:

- Сохранять `.meta` и GUID. Переименование/перенос делать вместе с `.meta`, предпочтительно через Unity AssetDatabase.
- `Assets`, `Packages`, `ProjectSettings` и документация хранятся в Git. `Library`, `Temp`, `Logs`, `Obj`, локальные builds и личные настройки исключаются по актуальному Unity `.gitignore`.
- Использовать text serialization и видимые meta-файлы; если настройка уже принята в проекте, сохранять её.
- Сцены, префабы и `.asset` создавать через Editor API/доступную Unity-интеграцию, не вручную выдуманным YAML.
- Не редактировать генерируемые Unity `.csproj` как главный способ настройки.
- Не выполнять массовый reimport, удаление Library или пересоздание пакетов без конкретной диагностированной причины.

## 7. Архитектура: владельцы данных и менеджеры

### 7.1. Composition root

`GameBootstrap` в единственной сцене получает явные ссылки на конфигурацию и компоненты. Он создаёт экземпляры runtime-сервисов, связывает зависимости, валидирует сцену и запускает выпуск. Нужные сервисы передаются через `Initialize(...)` или сериализованные ссылки. Не полагаться на случайный порядок Awake разных объектов.

MVP работает в одной сцене. `DontDestroyOnLoad`, глобальный service locator и десятки singleton-менеджеров не нужны. Сервисы сессии принадлежат bootstrap; компоненты сцены представляют объекты и UI.

### 7.2. Границы ответственности

| Компонент/сервис | Владеет | Не должен делать |
|---|---|---|
| GameBootstrap | Создание, wiring, validation, disposal | Считать рецепт или выбирать победителя |
| GameFlowManager | Единственная state machine выпуска, разрешённые переходы | Реализовывать нагрев/инвентарь |
| RoundManager | Контекст текущего раунда, таймер, начало/конец, снимок старта | Самостоятельно менять глобальное состояние |
| ChallengeManager | Назначение задания командам по RoundDefinition | Создавать штрафы из реплик шефа |
| GameClock | Пауза, simulation delta, скорость debug | Использовать время сцены как скрытый баланс |
| ContestantManager | Постоянные участники, команды, активность, станции | Подгонять NPC под балл игрока |
| ContestantSimulation | Детерминированные результаты NPC | Физически готовить одиннадцать блюд |
| CookingSession | Runtime-порции, станции, snapshot/reset | Сохранять текущую еду в SO-assets |
| RecipeResolver | Определение архетипа по составу и истории | Управлять камерой или выбыванием |
| ScoringSystem | Чистая оценка снимка и ScoreBreakdown | Двигать шефов или менять scene objects |
| JudgeSystem | Последовательность дегустации, показ оценки | Повторно вычислять результат при Skip |
| EliminationSystem | Выбор двух выбывающих в каждой команде | Удалять случайных участников ради драматургии |
| FinalRevealSystem | Номера, анонимные записи, победитель, раскрытие | Передавать авторство в анонимную presentation до reveal |
| ChefReactionDirector | Фильтры, очередь, cooldown, историю реплик | Давать тайные очки/штрафы |
| ChefController | Позиция, поворот, жесты ведущего | Принимать решение о победителе |
| CameraDirector | Фокус станции и presentation-позиции | Самостоятельно запускать следующий раунд |
| UIController / AudioManager | Показ фактов и проигрывание контента | Служить источником игрового состояния |
| DebugController | Явные диагностические команды и overrides | Обходить инварианты незаметно |

Имена — предлагаемый контракт, допустимо объединить мелкие классы. Границы ответственности обязательнее количества файлов. `GameFlowManager` не должен стать огромным классом всей игры.

### 7.3. Данные сессии

`ShowRunState`: run_id, seed, player_id, player_team, current_round, roster, история результатов. `RoundContext`: round_id, round_definition, challenge_by_team, judging_chef_by_team, timer, active_contestant_ids. `RoundStartSnapshot`: состав, задания и зерна **до** готовки для Retry. `RoundResult`: неизменяемые снимки блюд, категории, ранжирование и выбывшие.

`IngredientRuntimeState`: instance_id, ingredient_id, portion_count, preparation, heat_progress, cook_state, doses, contamination, origin_components, applied_operations, owner/location. Не хранить один mutable экземпляр состояния на все визуальные копии продукта.

`DishSnapshot`: идентификатор блюда, состав с происхождением, операции, plating, hygiene, submitted_at. `ScoreBreakdown`: категории, unrounded_total, caps, reason_keys. После подачи snapshot неизменяем; дальнейшие анимации сцены не влияют на оценку.

Для финала разделить `AnonymousDishRecord` (номер, snapshot, результаты) и закрытое сопоставление `number → contestant_id`. Scoring не требует автора; presentation анонимной дегустации получает только первую структуру.

## 8. ScriptableObjects: определения, не текущая попытка

ScriptableObject используется как asset с данными, который можно назначать в Inspector; он не является компонентом GameObject. Это соответствует назначению типа в Unity: [ScriptableObject data assets](https://docs.unity3d.com/6000.0/Documentation/Manual/class-ScriptableObject.html).

| Определение | Основные поля |
|---|---|
| PrototypeGameConfig | Длительности, команда по умолчанию, расстояние интеракции, вместимости, скорости, cooldown, сложность, debug flag, ссылки на каталоги |
| IngredientDefinition | stable_id, name_key, tags, portion_unit, prep capabilities, допустимые методы, heat profile, prefab, цвет/материал |
| RecipeDefinition | ID, обязательные группы/заменители, опциональные/несовместимые теги, количества, операции, seasoning ranges, serving roles |
| ChallengeDefinition | ID, текст задания, режим free/recipe, целевой рецепт, допустимые продукты, критерии |
| RoundDefinition | index, duration, team challenges, chef assignments, elimination_count_per_team, final flag |
| ChefDefinition | ID, name_key, персонаж/материал, gesture profile, judging profile, reaction library |
| ChefReactionLine | ID, chef_id, event kind, priority, allowed rounds/phases/team relation, условия, subtitle_key, AudioClip optional, gesture, cooldown/TTL |
| ChefReactionSequence | Набор 2–4 line references, интервалы и правило прерывания |
| ContestantDefinition | ID, имя, исходная команда, base_skill, affinity, variance, внешние настройки |
| ScoringProfile | Веса категорий, пределы ошибок, tiebreak policy; сумма весов валидируется |
| LocalizationTable | Список key/RU, подстановки; резерв для других языков |
| PrototypeBuildConfig | Размеры арены, позиции, ссылки на authoring templates, целевые generated paths |

У ScriptableObject есть `[CreateAssetMenu]` для ручного создания. Списки и простые сериализуемые структуры предпочтительнее сложных polymorphic графов в MVP. Не использовать обычный Dictionary как единственный сериализуемый источник Inspector без специального решения.

Runtime-значения меняются в обычных объектах сессии или специально созданных runtime-копиях, которые уничтожаются при завершении. Не записывать current score, cook_progress, roster eliminated или cooldown в исходные `.asset` во время Play Mode. В режиме Editor такие изменения могут испортить исходные настройки.

Assets с одинаковым ID запрещены. Ссылки на сцены и scene objects не хранить в постоянных SO-определениях; назначать их через bootstrap/сцену. Optional AudioClip не является ошибкой валидации.

## 9. State machine выпуска и время

Один владелец переходов — `GameFlowManager`. Базовая схема:

```text
Boot -> ShowIntro
  -> RoundBriefing(1) -> RoundCooking(1) -> RoundTimeUp(1)
  -> RoundJudgingTeamA(1) -> RoundJudgingTeamB(1) -> RoundElimination(1)
     -> GameOver, если игрок выбыл
     -> RoundBriefing(2), если прошёл
  -> RoundCooking(2) -> RoundTimeUp(2)
  -> RoundJudgingTeamA(2) -> RoundJudgingTeamB(2) -> RoundElimination(2)
     -> GameOver или RoundBriefing(3)
  -> RoundCooking(3) -> RoundTimeUp(3)
  -> FinalAnonymousJudging -> FinalReveal -> Victory или FinalLoss
```

Можно реализовать параметризованные состояния, а не отдельный enum для каждого номера раунда. Каждый переход проверяет исходное состояние и выполняется один раз. Анимации/корутины сообщают о завершении, но не ставят состояние напрямую.

На входе RoundCooking сбросить рабочие объекты и начать таймер. На выходе:

1. Закрыть приём gameplay-команд.
2. Отменить незавершённые действия и зафиксировать фактическое состояние еды.
3. Завершить автоматическую подачу по правилу GDD.
4. Зафиксировать результаты NPC и блюда ровно один раз.
5. Очистить очередь готовочных реакций.
6. Передать готовый RoundResult judging.

Любая команда несёт актуальный run_id/round_id; поздний callback прошлого раунда игнорируется. `Skip Judging` завершает presentation и применяет уже подготовленный результат, не пересчитывает его.

`GameClock` задаёт delta для нагрева, таймера, NPC, коротких игровых операций и реплик. Пауза = delta 0. UI паузы работает от реального времени. Debug speed меняет общую симуляцию согласованно. Не смешивать случайным образом `Time.deltaTime`, `unscaledDeltaTime` и `WaitForSecondsRealtime` для процессов одного раунда.

Предупреждения таймера определять по пересечению порога, а не равенству float. Если время прыгает с 61 на 9 с, отметить все пересечённые пороги, но озвучить самое актуальное предупреждение 10 с; старые «60 секунд» не ставить следом в очередь. «Время!» имеет высший приоритет.

## 10. Typed Event Bus

Создать небольшой **экземплярный** `GameEventBus`, живущий в контексте текущей сессии. Не использовать глобальный static bus, строки без типов или отложенное хранение references на уничтоженные GameObjects.

Предлагаемый интерфейс: `Subscribe<T>(handler)` возвращает IDisposable-токен; `Publish<T>(eventData)` сообщает о свершившемся факте. Подписки компонентов активируются после initialization и снимаются при disable/dispose. Сервисы освобождают токены при завершении run. Отписка работает даже без повторной загрузки сцены.

Базовые требования:

- Выполнять handlers в главном потоке Unity. Публикация синхронная; вложенные публикации помещаются в короткую FIFO-очередь и доставляются после текущего события.
- Итерировать по снимку подписчиков, чтобы отписка внутри callback не ломала обход.
- Событие содержит ID и значения факта; snapshot DTO допустим. Не передавать mutable state как «исторический факт».
- Event bus не хранит всю текущую игру; поздний подписчик читает состояние из владельца и далее слушает изменения.
- Ошибка обработчика логируется с типом события и контекстом. Ошибка критической логики останавливает запуск/переход с понятной диагностикой; UI/аудио не должны мешать доставке остальным.
- Для domain-решений использовать прямой вызов сервиса, а не event ping-pong. Инвентарь сначала атомарно меняет данные, затем публикует `IngredientTaken`.

### 10.1. Минимальная карта событий

Всем gameplay-событиям доступны `run_id`, `round_id`, `simulation_time`; события участников дополнительно содержат `actor_id` и `team_id` там, где авторство не должно быть скрыто. Анонимные final presentation events эти поля не раскрывают.

| Typed event | Payload сверх контекста | Producer | Consumers |
|---|---|---|---|
| GameStateChanged | previous, current | GameFlowManager | UI, input, camera |
| RoundStarted | challenge IDs, active counts | RoundManager | HUD, stations, chefs |
| TimerThresholdReached | threshold, time_remaining | RoundManager | UI, reaction director |
| IngredientTaken | ingredient ID, quantity, slot | BasketInventory | BasketView, reactions |
| BasketCapacityRejected | ingredient ID, reject reason | BasketInventory | UI, reactions |
| BasketPlaced | station ID, transferred instance IDs | StationInventory | Visuals, reactions |
| FoodDropped | instance ID, contamination | FoodTransfer | Reactions, visuals |
| PreparationCompleted | instance ID, prep kind, quality | Cutting/Mixing | UI, reactions |
| CookingStarted | station ID, instance IDs, method | CookingStation | SFX, reactions |
| FoodStateChanged | instance ID, old/new, heat progress | FoodProcessor | Visuals, reactions |
| SeasoningApplied | dish/component ID, kind, dose count | Seasoning action | UI, contextual reaction |
| PlateChanged | plate ID, dirty flag, serving roles | PlatingStation | UI, visuals |
| PlateCleaned | plate ID, cleanliness | Wipe action | Reactions, UI |
| PlayerIdleDetected | actor ID, idle seconds | ActivityTracker | Reactions |
| DishSubmitted | submission ID, snapshot, auto flag | SubmissionService | HUD, reactions |
| RoundCookingEnded | reason, result ID | RoundManager | Flow, input, reactions |
| DishJudged | visible dish ID, ScoreBreakdown | JudgeSystem | Result UI, chef presentation |
| EliminationResolved | eliminated IDs, survivors by team | EliminationSystem | Roster UI, NPC, flow |
| FinalNumbersAssigned | anonymous numbers only | FinalRevealSystem | Final UI, table visuals |
| FinalWinnerRevealed | winning number, contestant ID, player rank | FinalRevealSystem | UI, confetti, chefs |
| ShowEnded | outcome, player rank | GameFlowManager | Result UI, local log |

Текстовые design keys из GDD (`food_burned`, `good_cut` и т.п.) — ключи фильтров контента. Их сопоставляет `ReactionEventAdapter` с typed events: `FoodStateChanged -> burned` создаёт один кандидат `food_burned`, `PreparationCompleted -> high quality` — `good_cut`. Не заводить два независимых event bus с несовместимыми фактами.

## 11. Player, inventory и interaction contracts

`PlayerController`: перемещение и взгляд. `PlayerInputRouter`: action maps. `PlayerInteractor`: raycast по интерактивным слоям, проверка видимости и режима, контекстная подсказка. `StationFocusController`: лёгкий фокус камеры и возврат. Эти роли не должны считать качество еды.

Action maps: `Gameplay`, `Station`, `UI`, `Debug`. При открытой паузе/debug UI не принимать готовочные команды; после закрытия восстанавливать карту текущего состояния, а не всегда Gameplay. Управление курсором связано с UI/фокусом, сохраняется при Esc и потере фокуса окна.

Для station interaction использовать явный интерфейс, например `IInteractable`: доступность, текст действия, выполнение команды. Команда валидирует состояние заново при исполнении; подсказка предыдущего кадра не гарантирует возможность операции.

Предлагаемые слои: Player, Interactable, World, HeldVisual, NPC. Имена слоёв и их индексы валидировать; не зашивать цифры в raycast. HeldVisual не блокирует прицел и не сталкивается с CharacterController. Слои добавлять только в свободные slots, не переименовывать существующие проектные слои.

Инварианты переноса еды:

- У порции ровно один владелец/местоположение: pantry generation, basket, tray, hand selection, station, plate, submitted или trash.
- Логические изменения происходят атомарно перед анимацией.
- Отказ из-за вместимости или несовместимости ничего не расходует.
- Смешивание потребляет входы и создаёт составной выход один раз; нарезка меняет визуал, не дублирует количество.
- Scene object — представление runtime-порции, а не единственный источник её данных.

Корзина у камеры визуальная. Выгрузка переносит весь допустимый набор одним действием. Обычный перенос между лотком, рукой и станциями — snap. Rigidbody допускается для отложенного броска, но рецепт не должен зависеть от положения физической крошки.

## 12. Cooking systems и сохранение происхождения

Не создавать отдельную копию всей системы нагрева для каждого блюда. Вынести общий `FoodProcessor`/heat profile, а станции задают допустимый метод и операции.

| Станция | Runtime-данные | Границы MVP |
|---|---|---|
| CuttingStation | Один input, press progress, интервалы, quality | Результат rough_cut/chopped, примитивная замена визуала |
| PanStation | До трёх порций, heat level, дозы масла, processor | Жарка стейка/омлета, без сложного переворачивания |
| PotStation | До трёх порций, heat, stir progress | Картофельный гарнир, условная вода |
| MixingStation | До шести входных порций, mix progress, composition | Тесто тарта/смесь омлета, неполный состав допустим |
| OvenStation | Одна форма со смесью/яблоком, door, heat | Нагрев при закрытой двери, без жидкости/деформации |
| PlatingStation | Serving roles с несколькими реальными порциями, количество/посуда/дозы, cleanliness, submitted flag | Выкладка с горкой D-029, добавки D-028, вытирание, фиксация snapshot |
| ClearWorkspaceAction | Список удаляемых свободных остатков | Защищает тарелку, приборы, инструменты и корзину |

Рецепт описывает допустимую подготовку, методы и последовательность необходимых операций. История блюда сохраняет входные типы и порции через mixing/baking. Запечённая смесь муки и сахара без яйца не становится полноценным тартом только потому, что у неё prefab тарта.

Данные heat profile включают: скорость для каждого режима, raw threshold, acceptable range, ideal range, overcooked threshold, burned threshold. Проверить монотонность порогов и положительные скорости. `burned` необратим; смешивание и перекладывание не сбрасывают историю нагрева или загрязнение.

Визуализация состояния: цвет, размеры нарезанных частей, простая дымовая система, подпись. Нельзя использовать цвет материала как источник качества: scoring читает runtime data.

Весь countdown и готовка питаются от GameClock. Действия на станции имеют cancellation path, корректное окончание на timeup и reset. На повтор раунда не оставлять старые coroutine, Invoke или callbacks.

## 13. Scoring, NPC, judging и elimination

`ScoringSystem` — по возможности обычный C#-сервис: `DishSnapshot + ChallengeDefinition + ScoringProfile -> ScoreBreakdown`. Формулы и веса брать из GDD. Для Unity assets использовать адаптер к вычислительным данным; расчёт не должен обращаться к сцене.

Обязательные свойства: отсутствие случайности в оценке игрока, предел 0–100, применимые caps, оценка состава реальных поданных продуктов, единая обработка нетипичного блюда и повторяемый результат. Отображение округляет балл, сортировка использует полную точность.

NPC генерируются из `run_seed + round_id + contestant_id` независимыми потоками случайности. Для объединения seed и ID использовать явно выбранный стабильный алгоритм, а не зависящий от процесса `string.GetHashCode()`. Генерация реплик не должна потреблять ту же последовательность RNG и менять результаты NPC. Итоговые шаблоны блюд и категории согласованы с суммой и дефектами.

`JudgeSystem` получает готовый RoundResult, назначенных шефов и порядок обхода. Он двигает шефов, запускает жест «пробует», показывает оценку и реплику. Переход к следующему блюду завершает presentation; данные не пересоздаются. У всех scripted сцен есть максимальная длительность и безопасное завершение, чтобы отсутствующая анимация не приводила к зависанию игры.

`EliminationSystem` чисто вычисляет bottom 2 отдельно для A/B по правилам GDD. Результат применяет ContestantManager один раз, после чего flow проверяет player alive. В MVP исходную команду не менять при смене судьи.

Повторный Skip, повторный callback жеста, проигрыш игрока во время объявления или отсутствие audio clip не должны удваивать выбывание. В логах сохранять ID результата и уже применённую фазу.

## 14. Анонимный финал и reveal

При окончании третьей готовки:

1. Проверить четыре активных ID, по два из каждой команды.
2. Получить четыре immutable dish snapshots, включая `no_dish`, если финалист не приготовил ничего.
3. Один раз выполнить shuffle номеров 1–4 отдельным seeded RNG и сохранить mapping в FinalRevealSystem.
4. Создать нейтральные тарелки/клоши на четырёх финальных slots. Удалить цвета и подписи автора; камера не показывает перемещение игроковой тарелки в её анонимный слот.
5. Предоставить JudgeSystem анонимные записи, не roster mapping.
6. Оба шефа оценивают каждое блюдо. Итог — среднее, затем правила ничьих из GDD.
7. Показать номер победителя; после драматической паузы раскрыть автора и место игрока.
8. Завершить выпуск с Victory/FinalLoss.

Управляемое скрытие авторства — игровое правило, не система безопасности. В редакторе mapping можно смотреть через debug. В обычном HUD авторство раскрывается только событием FinalWinnerRevealed; имена и team colours до этого недоступны компонентам анонимного представления.

Номер не пересоздаётся при смене камеры, открытии меню, Skip или проигрывании реплики. FinalLoss показывает фактический ранг 2–4. Сертификат/деньги — визуальный итог, без валютного аккаунта или сохранения экономики.

## 15. Реакции шефов, звук и presentation

`ChefReactionDirector` слушает typed events через adapter, создаёт кандидатов, фильтрует по фазе/раунду/шефу/отношению к команде, сортирует по приоритету, проверяет cooldown и TTL. Значения — из PrototypeGameConfig/контента GDD.

История использования включает ID реплики, шефа, момент и текущий раунд. Реплики выбираются из подходящих ещё не использованных вариантов отдельным RNG. Если набор исчерпан, разрешить старую реплику после её cooldown или промолчать. Не пытаться озвучить каждый подобранный продукт.

Одна активная речевая последовательность. AudioManager даёт отдельный канал voice; субтитр синхронизирован с клипом или текстовой длительностью. Таймерные объявления имеют приоритет и могут прервать последовательность. Во время judging обычные готовочные кандидаты не играют.

Жесты MVP: поворот к игроку, короткая остановка, наклон головы, движение руки или инструмента. Реакция не требует, чтобы шеф физически успел дойти: он может комментировать с места. Точки патруля не пересекают узкие рабочие проходы игрока; столкновения NPC не должны запирать его.

Вместо AnimatorController при первом срезе допустим небольшой procedural gesture. Если AnimationTrigger отсутствует, реплика всё равно завершается. Позже авторская анимация заменяет view, сохраняя event/reaction contract.

CameraDirector знает три режима: gameplay, station focus, judging/final presentation. По завершении каждой сцены возвращает правильную позицию и input mode. Эффекты и камера не принимают игровые решения.

## 16. Сцена и её hierarchy

```text
ChefShow_Prototype
  Systems
    GameBootstrap
    GameFlowManager
    RoundManager
    ContestantManager
    JudgeSystem
    ChefReactionDirector
    CameraDirector
    AudioManager
    DebugController
  Arena
    Floor
    Walls
    Pantry
    TeamAStations
      Station_A1 ... Station_A6
    TeamBStations
      Station_B1 ... Station_B6
    JudgingArea
      TeamA_DishSlots
      TeamB_DishSlots
      Final_DishSlots
    ChefWaypoints
    ContestantWaypoints
    SpectatorPositions
  Player
    CharacterController
    CameraRig
      MainCamera
      BasketView
      HeldFoodView
  Chefs
    ChefSavory
    ChefPastry
  Contestants
    NPC_01 ... NPC_11
  Lighting
  UI
    EventSystem
    HUD
    Briefing
    PauseMenu
    JudgingPanel
    ResultsPanel
    SubtitlePanel
    DebugPanel
```

Event bus, scoring и elimination могут быть обычными C#-сервисами, поэтому не требуют отдельного пустого GameObject ради каждого названия. В hierarchy оставить только реальные MonoBehaviour/views. Префабы станций, продуктов и персонажей заменяемы; ссылки не искать каждый кадр через `Find`.

Нужны gizmos для interaction distance, станций, chef waypoints, dish slots и spawn positions. Одна камера с AudioListener. Простое освещение без тяжёлых эффектов. Статичный pantry и arena collision; декоративные детали не должны блокировать raycast.

## 17. Editor Scene Builder

### 17.1. Команды

Реализовать меню:

```text
Tools > Chef Show > Build Prototype Scene
Tools > Chef Show > Create Editable Scene Copy
Tools > Chef Show > Validate Prototype
Tools > Chef Show > Create Missing Default Data
```

Builder живёт в editor-сборке и использует Unity Editor API. Создание новой сцены и сохранение выполняются через EditorSceneManager; SaveScene возвращает успешность сохранения, которую нужно проверить: [официальная документация SaveScene](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneManagement.EditorSceneManager.SaveScene.html).

**Не запускать разрушительную регенерацию автоматически** в OnValidate, Awake, Play Mode или при каждом reload скриптов. Команды меню — явный инструмент автора/агента.

### 17.2. Порядок генерации

1. Проверить отсутствие compile errors, Play Mode и ожидающих import операций.
2. Найти конфиг сборки и доступные authoring assets. Сначала использовать ссылки на `Data`, `Prefabs`, `Art`; создать недостающие defaults только в `Generated`.
3. Для открытых несохранённых сцен предложить стандартное сохранение средствами Unity; при отмене остановить Builder. Не уничтожать открытые пользовательские изменения.
4. Создать новую сцену, желательно additive, не модифицируя текущую рабочую.
5. Создать пол, стены, две зоны, 12 станций, pantry, judging/final slots и маршруты.
6. Создать одного игрока, одиннадцать NPC и двух шефов; ID участника/слота назначаются стабильно.
7. Создать примитивные продукты и приборы текущего реализованного этапа, light, UI и единственный EventSystem.
8. Создать/назначить конфиги, каталоги и system references. Не оставлять скрытую зависимость от ручного Inspector-wiring.
9. Прогнать validator. При ошибках не объявлять сцену готовой; показать список.
10. Сохранить в `Assets/_ChefShow/Generated/Scenes/ChefShow_Prototype_Generated.unity`, сохранить assets, вывести короткий итог и путь.

Builder расширяется по этапам: сначала строит арену и контроллер, позднее подключает готовку и финал. На раннем этапе отчёт явно пишет «готовка пока не реализована», а не создаёт фальшивые рабочие кнопки.

### 17.3. Повторяемость и защита ручной работы

- Регенерация затрагивает только явно генерируемую сцену; никогда не перезаписывает `Scenes/ChefShow_Prototype.unity`.
- Повтор запуска не добавляет дубликаты ID, cameras, EventSystem или assets.
- Уже существующие default assets загружаются по стабильным путям и GUID; Builder не сбрасывает их значения каждый запуск. Для полного восстановления defaults нужна отдельная явно обозначенная операция, не скрытое поведение Build.
- `Create Editable Scene Copy` создаёт рабочую копию из generated scene. Если рабочий файл существует, не перезаписывает его: предлагает другое имя или требует явного действия замены.
- Дальнейшие правки автора в рабочей сцене сохраняются. Обновление кода и SO-настроек не требует регенерации арены. Для новых компонентов использовать отдельную проверяемую миграцию/обновление prefab, а не пересоздание всей рабочей сцены.
- Не удалять весь `Assets/_ChefShow`, не пересоздавать `.meta`, не обнулять материалы/настройки, которые автор изменил вручную.

Generated- и рабочая сцены используют явно назначенные assets. При необходимости собственных копий авторских данных делать копию через Unity и назначать новую ссылку; не скрывать общий mutable asset.

### 17.4. Валидация

Проверить обязательные ссылки bootstrap, 12 уникальных contestant IDs, ровно одного игрока, две команды по шесть на старте, станции и dish slots, двух шефов, camera/listener, EventSystem/input module, URP shader, кириллицу и конфиги.

Для RoundDefinitions проверить порядок 1/2/3, переходы 6→4→2, два задания второго раунда с правильными судьями и final flag третьего. Для heat profiles — диапазоны; для scoring — веса и caps; для recipes — ядро и операции; для reactions — ключи, TTL/cooldown, optional audio. Не требовать P1-контент для MVP.

## 18. Debug Mode и диагностические команды

F1 открывает панель только в Editor или Development Build при разрешённом prototype debug flag. В обычной release-сборке скрыть и отключить force-команды. Debug не должен незаметно менять стандартные результаты.

Набор команд:

| Команда | Поведение и ограничения |
|---|---|
| Start Round 1/2/3 | Построить валидный состав 6/4/2 на команду, контекст и чистые станции |
| Timer 30/60/240 | Изменить оставшееся время, корректно обработать пересечения порогов |
| Player Team A/B | До выпуска или через новый run; не менять текущую историю молча |
| Spawn Ingredient / Fill Basket | Через те же правила вместимости, без дублирования instance ID |
| Complete/Burn Selected Food | Установить валидное состояние, опубликовать фактическое изменение |
| Submit Sample Dish | Сгенерировать явно помеченный test snapshot выбранного качества |
| Skip Cooking / Go To Judging | Закрыть раунд обычным end pathway; no_dish допустим |
| Force Player Score 0–100 | Override для теста; пометка FORCED в UI/логах, категории согласованы |
| Win/Lose Current Round | Валидный override ранжирования, без нарушения числа выбывающих |
| Force Final Number Mapping | Только до judging финала, с явной пометкой |
| Trigger Reaction | Проверить очередь, фильтры, субтитры и отсутствие audio |
| Simulation Speed | Изменить GameClock целиком, а не только UI-таймер |
| Retry Round / Restart Show | Через штатный snapshot/reset |

Панель показывает текущую фазу, run/round seed, состав, назначенных судей, таймер, данные выбранной порции, score breakdown, очередь реакций и mapping финала. Force-override сбрасывается при следующем новом run и явно сохраняется в test report текущей проверки.

Локальный структурированный лог с ID событий полезен для расследования повторного выбывания и рассинхронизации. Длинный поток per-frame событий не писать. Пользовательские тестовые отчёты сохранять вне `Assets`, чтобы Unity не импортировал их как контент.

## 19. Поэтапная реализация и критерии выхода

Этапы — ориентир зависимостей, а не календарное обещание. Каждый завершается запускаемым состоянием без compile errors.

### Этап 0. Дизайн и подготовка

Прочитать документы/проект, провести короткий разбор, создать канонический индекс, decisions, architecture и карточку F-001. Подтвердить ближайшие предположения.

**Готово:** зафиксирован MVP, известны версия Unity/пакеты, путь индекса и первая фича. Gameplay ещё не обязан существовать.

### Этап 1. Foundation, арена, управление — F-001/F-002/F-003 и начало F-013

Собрать конфиги, bootstrap, clock, bus, input и controller. Builder создаёт примитивную арену, станции, pantry и персонажей-плейсхолдеров. HUD показывает состояние и пробный таймер; debug умеет restart/pause/короткую длительность.

**Готово:** можно ходить, смотреть, выбирать интерактивную цель, ставить паузу; сцена генерируется повторно без дубликатов и сохраняется; рабочая копия редактируется отдельно.

### Этап 2. Продукты и базовая готовка — F-004/F-005, часть F-006/F-007

Добавить корзину, лоток, одну порцию, перенос, доску, сковороду, кастрюлю для гарнира, snap-подачу. Первое блюдо — стейк с овощами. Связать реальные события, начать F-008 с нескольких реплик и субтитров.

**Готово:** игрок физически проходит сбор → подготовку → нагрев → тарелку; можно недоготовить и сжечь, данные не клонируются, пауза останавливает приборы.

### Этап 3. Первый полный раунд — F-009/F-010, часть F-008/F-013/F-014

Включить 12 участников, NPC-результаты, briefing, таймаут/автоподачу, scoring, короткую дегустацию и bottom 2 отдельно для каждой команды. Реализовать GameOver, Retry Round и Restart Show.

**Готово:** без debug можно закончить раунд 1, получить объяснимую оценку и 4+4 survivors или выбыть. Force win/lose помогает проверке, но реальный результат уже работает. Это первый вертикальный срез для автора.

### Этап 4. Полный набор кухни и Reactive Chefs — завершение F-006/F-007/F-008

Добавить смешивание, духовку, тарт и омлет, дозы, вытирание, очистку. Довести очередь реплик, cooldown, TTL, praise и перепалки. Подготовить базовый объём контента.

**Готово:** каждое из трёх блюд готовится руками; неверная смесь и сгоревший компонент имеют реальные последствия; шефы реагируют уместно и не накладывают речь.

### Этап 5. Смена шефов и раунд 2 — F-011

Назначить Team A десерт кондитера, Team B горячее блюдо второго шефа. Сохранить состав/станции, сменить судей и набор реплик, применить 4→2 в каждой команде.

**Готово:** игрок обеих команд может дойти до финала; неправильный рецепт получает объяснимую оценку; остаётся строго четыре финалиста.

### Этап 6. Финал — F-012

Свободная готовка, номера, клоши, скрытый mapping, два judging результата на блюдо, обсуждение, выбор номера и раскрытие. Victory и FinalLoss с фактическим местом.

**Готово:** в одном run финальный номер стабилен; player/NPC могут выиграть; пропуск presentation не меняет победителя; авторство скрыто до reveal.

### Этап 7. Приёмка полного выпуска — завершение F-013/F-014

Проверить все исходы, reset и паузу, полную сборку для Windows, работу без editor-only API, режим debug и управляемые параметры. Провести внешние плейтесты, записать результаты по GDD. Исправить выявленные blockers.

**Готово:** полный обычный выпуск запускается без вмешательства разработчика, укладывается в целевой ритм и соответствует acceptance criteria GDD. После этого автор решает об арт-проходе и расширении.

## 20. Проверки, тесты и отчёт о готовности

Не писать тесты, которые лишь повторяют порядок строк реализации. Нужны проверки рисков и инвариантов.

EditMode, когда системы готовы:

- Корзина/перенос: capacity, отказ без расходования, сохранение количества, отсутствие двух владельцев.
- Рецепт/еда: сохранение состава через смешивание, различие preparation/heat, caps, experimental fallback.
- Scoring: эталон хорошего блюда, сырое/сгоревшее, пустая тарелка, неприменимые критерии, точные ничьи.
- Elimination: ровно 2 выбывающих отдельно в A/B, корректный переход состава.
- NPC: одинаковый seed даёт одинаковый результат; генерация реплик его не меняет.
- Финал: shuffle является перестановкой 1–4, mapping сохраняется, ранг фактический.
- Реакции: фильтры, cooldown, TTL, приоритет, отмена старого round context.

PlayMode/ручные сценарии:

- Запуск bootstrap и Builder validation, одна camera/listener и EventSystem.
- Pause во время нагрева/смешивания/реплики, продолжение без рассинхронизации.
- Действие на границе 00:00 и повторная подача.
- Skip judging дважды: итог не пересчитывается, выбывание применяется один раз.
- Retry Round после проигрыша возвращает исходный состав текущего раунда.
- Restart после victory/finalloss: нет старых объектов, callbacks и подписок.
- Round 2 за обе команды; финал без маркеров авторства.
- Никаких missing references и errors в Console; кириллица читается.

Проверить отдельную Windows-сборку: отсутствие runtime-ссылок на UnityEditor, доступность сцены в активном Build Profile, наличие UI/input, корректное отключение debug в обычной сборке. Сцену выбирать явно; не затирать список других сцен проекта.

Если нет возможности запускать Unity в окружении агента, написать «проверено статически; Play Mode/сборка не проверены», оставить статус `implemented` и дать конкретные шаги проверки. Не объявлять `verified` на основании просмотра C# или наличия `.unity`-файла.

Для командного запуска тестов использовать реальный путь установленного Editor и текущего проекта, поддерживаемые им параметры и отдельный путь отчёта. Не запускать вторую Unity-процессию поверх открытого проекта, не обходить licensing и не закрывать окно автора ради теста.

Итог каждого этапа: что теперь можно сделать в игре, как открыть сцену и проверить, какие проверки выполнены, какие ограничения остаются, какая следующая фича по индексу. Не загружать автора длинным перечнем внутренних классов.

## 21. Стартовые запросы автору для копирования

### 21.1. Первый запрос: дизайн-разбор

```text
Это корень моего Unity 6 URP проекта. Прочитай GAME_CONCEPT.md,
CODEX_PROJECT_INSTRUCTIONS.md, применимые AGENTS.md и существующие docs.
Проверь фактическую версию Unity и пакеты. Сначала проведи короткий
дизайн-разбор концепта с доступным GDS/GDD-скиллом: задай 3–5 вопросов,
которые влияют на ближайший этап. После моих ответов зафиксируй решения,
создай или обнови единственный Feature Index и предложи первый вертикальный
срез. Пока не начинай gameplay-код; подготовь документы для реализации.
```

### 21.2. Запрос после фиксации решений

```text
Начинай этап 1 по CODEX_PROJECT_INSTRUCTIONS.md и утверждённому Feature Index.
Сохрани мои существующие изменения. Реализуй foundation, Editor Scene Builder,
арену из примитивов, first-person управление, базовую интеракцию, HUD, паузу
и debug restart. Генерируемая сцена должна быть отделена от рабочей копии.
Проверь компиляцию и запуск доступным способом, обнови индекс и лог.
Покажи, какую сцену открыть и как проверить результат в Unity.
```

### 21.3. Запрос на первый полный раунд

```text
Прочитай актуальный GDD, Feature Index и завершённые этапы. Реализуй следующий
сквозной срез: продукты → корзина → лоток → нарезка → стейк с гарниром →
подача → реакция шефа → scoring → judging → выбывание 6→4 в каждой команде.
Не включай будущую метапрогрессию и сложную физику. Добавь штатный повтор
раунда и выпуска. Проверяй этапы по мере реализации, фиксируй результаты,
не выдавай непроверенный код за готовый Play Mode.
```

### 21.4. Запрос на продолжение одной фичи

```text
Продолжи следующую ready-фичу по каноническому Feature Index. Сначала прочитай
её спецификацию, зависимости и последние принятые решения. Реализуй один
законченный игровой результат, проверь acceptance criteria и обнови документы.
Если нужен новый дизайн-выбор, задай только относящийся к нему вопрос.
Не меняй уже утверждённые правила и ручные правки сцены без основания.
```

### 21.5. Запрос на приёмку

```text
Проверь полный выпуск по GDD: обе команды, все три раунда, проигрыши,
смену судей, анонимные номера, reveal, паузу, Retry и Restart.
Проверь Builder и сохранность рабочей сцены, затем Windows-сборку,
если Unity доступна. Исправь найденные blockers и составь короткий отчёт:
что проверено фактически, что осталось непроверенным и какие шаги мне
выполнить в редакторе. Обнови Feature Index без завышения статуса готовности.
```

## 22. Правила, которые нельзя потерять при расширении

1. Игрок — один из 12 участников, поэтому NPC ровно 11.
2. Отборочный тур не входит в MVP.
3. Смена шефов не меняет команды и не переносит участника на чужую станцию.
4. В раундах 1/2 исключать по два внутри каждой команды; финалистов ровно четыре.
5. Таймер около четырёх минут включает сбор продуктов; пауза синхронна всей симуляции.
6. Реакции основаны на фактах; скрытых штрафов за реплику/чужую команду нет.
7. Состав и история еды сохраняются через все операции; визуал не определяет score.
8. Результат вычисляется и применяется один раз, независимо от анимаций и Skip.
9. Анонимный mapping создаётся один раз, раскрывается в правильной фазе.
10. Runtime-попытка не портит исходные ScriptableObjects.
11. Builder создаёт воспроизводимый серый прототип и сохраняет ручную рабочую сцену.
12. Готовность подтверждается проверкой; все непроверенные части названы явно.

При замене примитивов арт-ассетами сохранять эти контракты. Новый контент добавляется через definitions, prefabs и карточки фич, а не копированием всей игровой логики для каждого рецепта.

## Уточнение автора 9 октября 2026 — D-028/D-029

При реализации F-007 поддержать много порций на одной тарелке, включая повтор одного продукта и неправильные сочетания; четыре serving roles не являются пределом количества. Выкладка добавляет продукт к существующему содержимому и показывает горку с сохранением реальных ID/количеств/пищевых состояний. До Submit/00:00 доступны малые дозы соли/масла на собранном блюде. Тип распределения дозы и предел переполнения уточняются Q-018/Q-014; точные штрафы F-010 не выводить из геометрии. Эти правила пока описаны, не установлены в Unity. Актуальные решения/критерии — docs/features/F-007-recipes-serving.md, docs/decisions.md, T-036.

## Актуальный установленный срез D-030…D-032 — 9 октября 2026

Прямой запрос автора обновляет старые описания ввода: ЛКМ — еда слева/ящик/дозы/ручки, ПКМ — инструмент справа/6 нарезок/открытие упаковки в лотке/смешивание. E не подбирает; F фокус, Tab корзина, Backspace возврат еды. Мини-рука и русское действие над прицелом. Реализованы многокомпонентная тарелка/горка/общие дозы/Submit/00:00, миска с реальными входами и отдельно стоящая духовка у стены за участником. Все объекты/ссылки сохранены в Edit Mode. Итоговые Edit36/36 и Play32/32 PASS, proof/границы — docs/implementation-log.md. Полный scope F-006/F-007 ещё включает профили рецептов/тушение/посуду/справочник/очистку. Следующая сессия использует docs/2026-10-09-handoff.md и first-playable.md; старые pending не отменяют этот итог.

## Актуальный checkpoint D-033/D-034 — 10 октября 2026

**EditMode 41/41 и полная Play Mode 34/34 PASS** (10 октября 2026). Итоговый Edit: 09:37:04–09:37:05 UTC, TestResults/dishware-final-editmode.xml; Play: 09:32:43–09:35:07 UTC, job `2dc7315077ea46ffafc6e57d38a0ad22`, TestResults/dishware-full-playmode.xml. Адресный физический прогон 4/4 также пройден. Проверены все пять вариантов, смена наполненной тарелки с сохранением мяса/яйца/доз, занятость левой руки, чужая команда, пауза, Restart, неизменяемый снимок и мягкое переполнение; реальная смесь проходит увеличенную духовку, все четыре обхода сохранены.

Обе сохранённые сцены Validate PASS, dirty=False/hidden=0, EditorOptions=0/AutoPause=True. На каждой 9771 GameObject, 60 DishwareTarget, 5 профилей, 36 приборов/12 тарелок/12 мисок/48 инструментов/101 прежняя InventoryInteractable. Все 8705 исходных объектов и сериализованных записей сохранены, прежние .meta/GUID не менялись. Из исходных поз изменены только Floor/4 стены/12 Oven/12 Plate/Status; камера working2.20/generated1.65, участники, рабочие столы, щели и ручное освещение сохранены. Доказательства: TestResults/dishware-final-scene-proof.txt и dishware-preservation.json. После просьбы Ctrl+S получено подтверждение автора; перед установкой обе сцены были чистыми, сохранены резервные копии в TestResults/dishware-compact-before-*.

ЛКМ берёт посуду слева/ставит на блюдо/заменяет наполненную без потерь; ЛКМ на источнике или Backspace возвращает. Старт small_flat6, варианты large_flat12/deep_plate8/bowl10/kosushka4; заполнение суммаQuantity, мягкая горка. Current/Held/DishSnapshot используют неизменяемый профиль, Inspector Config.Types, изменения после Restart. Духовки масштаб2/x±12.86, комната28×38; два длинных стола Arena/Dishware Table A/B x±11.65. Камера2.20/ручное освещение сохранены. Не восстанавливать D-032 x±28.7 и прежние стены60×44, не запускать rebuild/Foundation.

F-004/F-005 остаются verified; F-002/F-003/F-006/F-007 остаются in_progress в полном scope. Выбор посуды завершён в пределах D-034. Далее — профили 12 рецептов/распознавание и справочник F-007; реальное тушение требует отдельного процесса F-006, текущий Pot не считать тушением. Очки за посуду/количество/вкус — F-010, реакции — F-008. Полный игровой проход за B, Windows build и субъективный UX этим этапом не подтверждены. Этап сохраняется локально в main; push без нового прямого запроса не выполнять.

## D-035/D-036 — действующая компоновка и подача, 10 октября 2026

Зазор за поварскими столами до боковых стен увеличен с 4,7 до 9,4 м. Стены x±18.7; торцевые зазоры также удвоены: передний 10,8→21,6 м, задний 4,3→8,6 м. Комната 37,4×53,1 м, центр z−3.25, торцы z−29.8/+23.3. Духовки масштаб2 остаются у новых стен x±17.56. Два стола посуды, рабочие столы, участники, ящики, зоны, щели и камера2.20 сохраняют позиции.

Красная физическая кнопка слева от тарелки подаёт блюдо по ЛКМ. Красный свет/эмиссия горят до Restart; еда, посуда и дозы зоны блюда заблокированы. На 00:00 действует прежняя автоподача только фактического содержимого. Остальная кухня после ручной подачи продолжает работать до конца таймера.

**Это заменяет D-034:** ЛКМ по реальной порции снимает еду, ЛКМ по свободной поверхности установленной посуды снимает саму посуду влево. При снятии или прямой замене посуды оставшаяся еда исчезает: её записи архивируются как Trash с операцией discarded_with_dishware, ID и история сохраняются. Общие PlateSaltDoses/PlateOilDoses/загрязнение очищаются. Чтобы сохранить продукты, сначала перенесите их на доску или в лоток. Продукты вне зоны блюда не затрагиваются.

Снятие или замена установленной посуды немедленно увеличивает PresentationPenalty на1, включая пустую посуду. Подбор на общем столе и возврат из руки не штрафуются. Установка на пустое место не даёт второй штраф. Вычет показан в HUD/ServingStation и сохраняется в неизменяемом DishSnapshot для F-010; полного судейства пока нет. CurrentDishware/DishSnapshot.Dishware могут быть null, FillRatio=0; без посуды еду класть нельзя. Restart восстанавливает small_flat, штраф0 и выключает красный свет.

**Проверки: EditMode 43/43 и полная Play Mode 35/35 PASS.** Edit job `82321804248f4cee82a7ce6281d28b43`, TestResults/submission-editmode.xml; Play job `759fd1749f02488fb82dafe428b5f186`, TestResults/submission-full-playmode.xml. Проверен физический цикл мясо→тарелка→доска→снять/вернуть тарелку→миска→еда→подать→запрет снятия/соли→Restart. Проверены удаление оставшихся порций, пять видов посуды, чужая команда, пауза/таймаут, нагрев большой духовки и четыре обхода. Старые сценарии переноса/фокуса теперь наводятся на ServingTarget еды: WorkSurface намеренно снимает посуду. Проверки ID, истории, вместимости, шести кликов, паузы и камеры сохранены; адресный повтор4/4 PASS (TestResults/submission-regression.xml). Foundation rebuild не запускали.

Обе сцены Validate PASS, dirty=False/hidden=0, AutoPause=True/EditorOptions=0. Сохранены все9771 исходных GameObjects и serialized records; теперь9783 объекта за счёт12 сохранённых Submission Red Light. Прежние .meta/GUID сохранены. В working изменены30 Transform/RectTransform: Floor, четыре стены,12 Oven,12 Submit Dish и Status. В generated ещё12 Work Surface подняты с0.53 до0.93м: старая зона блюда была ниже столешницы. Теперь тарелка и кнопка над столом, координаты кнопок совпадают с working. Камеры working2.20/generated1.65 и ручное освещение сохранены. Доказательства: TestResults/submission-preservation.json и submission-final-scene-proof.txt; просмотрен submission-red-locked.png. Объекты: Station_*/Inventory/Work Surface/Plate Contents/Submit Dish; ссылки ServingStation.SubmitButton/SubmitLight и Layout.ArenaCenterZ/Width/Depth видны в Inspector.

F-002/F-003/F-006/F-007 остаются in_progress в полном scope, F-004/F-005 verified. Полный игровой B, Windows build, субъективный UX и judging F-010 вне проверки. Не восстанавливать сохранение еды при смене D-034, комнату28×38/стеныx±14/духовки±12.86 и зелёную кнопку. Перед работой нужен свежий MCP preflight; не выполнять rebuild. Сохранять этап локально в main, push только по новому прямому запросу.

После Play35/35 исправлено сохранение URP-эмиссии: базовый _EmissionColor ненулевой и BakedEmissive сохраняют _EMISSION, runtime доSubmit переопределяет эмиссию0. Адресный физический повтор1/1 PASS (job `8dbceb5bf7524becae62b477e13b8f44`, TestResults/submission-emission-playmode.xml). Модель/рабочая геометрия после полного прогона не менялись. После выравнивания generated обе сцены повторно прошли native proof с проверкой высоты/keyword/цвета/света/ссылок; полный Play относится к рабочей сцене.

Актуальный checkpoint **D-037…D-039 / T-042/T-043, 2026-10-10** заменяет прежний pending. Полки, синяя отмена и книга I установлены через MCP в working/generated; **Edit48/48, полная Play39/39 PASS** (layout-recipe-shelves-* XML). Все12 кадров страниц и полки просмотрены. У12 духовок стоят12 полок с5 сохранёнными видами посуды; лавки перепрофилированы. Синяя Reset Submission снимает блок/красный свет только до00:00, сохраняя еду/посуду/дозы/штраф/таймер/прежний immutable snapshot; foreign/pause/timeup/disabled guards. Переключатель — Systems/ServingController.EnableSubmissionReset. Книга I:12 иллюстрированных страниц, зелёные стрелки, жёлтая «Выбрать», белая рамка списка справа сверху; список сохраняется/заменяется, задача не меняется. Modal блокирует камеру/ходьбу/обе руки; таймер/нагрев продолжаются; закрытие ждёт отпускания мыши. RecipeDefinition/Catalog в Data/Recipes доступны в Inspector, snapshot при Restart; количества — предложения. Настоящее тушение и перемешивание Pan не реализованы и отмечены в книге.9959 объектов, все9783 прежних GameObjects/records/GUID сохранены; прежние позы менялись только внутри лавок, неожиданных изменений0. Validate/dirtyFalse/hidden0, камера working2.20/generated1.65, AutoPauseTrue/EditorOptions0, настройки прежние. F-007 in_progress до распознавания/очистки, judging F-010 позже; Q-019 выдачи задач не решён выбором страницы. Не повторять install/rebuild/Foundation для регрессии; implement-shelves.py уже применён. Свежий preflight перед мутациями. Локальный main, push только по прямому запросу. Продолжение docs/2026-10-10-handoff.md/first-playable.md; полный B-проход/Windows build/субъективный UX вне проверки.
