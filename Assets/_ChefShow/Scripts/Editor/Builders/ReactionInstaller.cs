using System;
using System.Linq;
using ChefShow.Chefs;
using ChefShow.Contestants;
using ChefShow.Core;
using ChefShow.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChefShow.Editor
{
    public static class ReactionInstaller
    {
        private const string Folder = "Assets/_ChefShow/Data/Reactions";

        [MenuItem("Tools/Chef Show/Install Reactive Chefs")]
        public static void Install() => HandServingInstaller.UpdateScenes(AddToScene, "chef-reactions");

        public static void AddToScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var bootstrap = roots.SelectMany(root => root.GetComponentsInChildren<GameBootstrap>(true)).Single();
            var config = CreateConfig();
            var controller = bootstrap.Reactions;
            if (controller == null)
            {
                controller = bootstrap.GetComponent<ReactionController>();
                if (controller == null) controller = Undo.AddComponent<ReactionController>(bootstrap.gameObject);
            }
            Undo.RecordObject(controller, "Saved reaction references");
            if (controller.Config == null) controller.Config = config;
            var actors = roots.SelectMany(root => root.GetComponentsInChildren<PrototypeActor>(true)).ToArray();
            controller.Bindings = new[]
            {
                Binding(controller, actors, ChefSpeaker.Savory, "CHEF_SAVORY", new Color(1, .60f, .18f)),
                Binding(controller, actors, ChefSpeaker.Pastry, "CHEF_PASTRY", new Color(.76f, .46f, 1))
            };
            if (controller.Panel == null)
            {
                var existing = bootstrap.Hud.transform.Find("Chef Reaction");
                if (existing != null)
                {
                    controller.Panel = existing.gameObject;
                    if (controller.SpeakerText == null) controller.SpeakerText = FindText(existing, "Speaker");
                    if (controller.SubtitleText == null) controller.SubtitleText = FindText(existing, "Subtitle");
                }
                else
                {
                    // Convert the approved 1920x1080 design coordinates to the existing 1280x720 canvas.
                    // Its x~50..770 area leaves the right-hand judging card and bottom controls clear.
                    var panel = Rect("Chef Reaction", bootstrap.Hud.transform, new Vector2(.32f, 0),
                        new Vector2(.5f, 0), new Vector2(0, 136.6667f), new Vector2(720, 72));
                    var image = Undo.AddComponent<Image>(panel.gameObject);
                    image.color = new Color(.035f, .045f, .06f, .80f);
                    image.raycastTarget = false;
                    controller.Panel = panel.gameObject;
                    controller.SpeakerText = Text("Speaker", panel, new Vector2(12, -3), new Vector2(696, 18),
                        15, new Color(1, .60f, .18f), controller.Config.SavoryName, bootstrap);
                    controller.SubtitleText = Text("Subtitle", panel, new Vector2(12, -23), new Vector2(696, 48),
                        20, new Color(.97f, .98f, 1), "", bootstrap);
                    var book = bootstrap.Hud.transform.Find("Recipe Book");
                    if (book != null) panel.SetSiblingIndex(book.GetSiblingIndex());
                    controller.Panel.SetActive(false);
                    EditorUtility.SetDirty(image);
                }
            }
            var error = controller.Validate();
            if (error != null) throw new InvalidOperationException(error);
            Undo.RecordObject(bootstrap, "Chef reaction controller");
            bootstrap.Reactions = controller;
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(bootstrap);
        }

        private static ChefReactionBinding Binding(ReactionController controller, PrototypeActor[] actors,
            ChefSpeaker speaker, string stableId, Color color)
        {
            var actor = actors.Single(candidate => candidate.Kind == PrototypeActorKind.Chef && candidate.StableId == stableId);
            var binding = controller.Bindings == null ? default(ChefReactionBinding)
                : controller.Bindings.FirstOrDefault(candidate => !ReferenceEquals(candidate, null) && candidate.Speaker == speaker);
            if (ReferenceEquals(binding, null)) binding = new ChefReactionBinding();
            binding.Speaker = speaker;
            if (binding.Actor == null) binding.Actor = actor.transform;
            else if (binding.Actor != actor.transform)
                throw new InvalidOperationException("Сохранённая привязка шефа отличается от его StableId: " + stableId);
            if (binding.SpeakingMarker == null)
            {
                var marker = actor.transform.Find("Speaking Marker");
                if (marker == null)
                {
                    var body = actor.GetComponent<Renderer>();
                    var point = body.bounds.center;
                    point.y = body.bounds.max.y + .16f;
                    marker = HandServingInstaller.Shape("Speaking Marker", actor.transform, PrimitiveType.Sphere,
                        point, Vector3.one * .18f, HandServingInstaller.Material("Speaking Marker " + stableId, color)).transform;
                    marker.GetComponent<Renderer>().enabled = false;
                }
                binding.SpeakingMarker = marker.GetComponent<Renderer>();
                if (binding.SpeakingMarker == null)
                    throw new InvalidOperationException("Speaking Marker без Renderer: " + stableId);
            }
            if (binding.Voice == null)
            {
                binding.Voice = actor.GetComponent<AudioSource>();
                if (binding.Voice == null)
                {
                    binding.Voice = Undo.AddComponent<AudioSource>(actor.gameObject);
                    binding.Voice.playOnAwake = false;
                    binding.Voice.loop = false;
                    binding.Voice.spatialBlend = .25f;
                    binding.Voice.dopplerLevel = 0;
                    binding.Voice.minDistance = 2;
                    binding.Voice.maxDistance = 30;
                    EditorUtility.SetDirty(binding.Voice);
                }
            }
            return binding;
        }

        private static ReactionConfig CreateConfig()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_ChefShow/Data", "Reactions");
            var path = Folder + "/ReactionConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<ReactionConfig>(path);
            if (config != null)
            {
                var existingError = config.Validate();
                if (existingError != null) throw new InvalidOperationException(existingError);
                return config;
            }
            config = ScriptableObject.CreateInstance<ReactionConfig>();
            config.Definitions = new[]
            {
                Line("ready_savory_a", ReactionCue.FoodReady, ReactionPriority.Normal, ChefSpeaker.Savory,
                    "Порог готовности: {food}. Снимайте, пока огонь не увлёкся."),
                Line("ready_savory_b", ReactionCue.FoodReady, ReactionPriority.Normal, ChefSpeaker.Savory,
                    "Готовность достигнута. Дальше нагрев уже не работает на пользу порции."),
                Line("ready_pastry_a", ReactionCue.FoodReady, ReactionPriority.Normal, ChefSpeaker.Pastry,
                    "Порция готова: {food}. Следующий шаг — вовремя снять её."),
                Line("ready_pastry_b", ReactionCue.FoodReady, ReactionPriority.Normal, ChefSpeaker.Pastry,
                    "Порог готовности пройден. У передержки теперь слишком удобный вход."),
                Line("burn_savory_a", ReactionCue.FoodBurned, ReactionPriority.High, ChefSpeaker.Savory,
                    "{food}: порция сгорела. Огонь взял лишнюю смену."),
                Line("burn_savory_b", ReactionCue.FoodBurned, ReactionPriority.High, ChefSpeaker.Savory,
                    "Сгорание подтверждено. У этой порции уже слишком дымная биография."),
                Line("burn_pastry_a", ReactionCue.FoodBurned, ReactionPriority.High, ChefSpeaker.Pastry,
                    "{food}: граница сгорания пройдена. Румянец закончился раньше."),
                Line("burn_pastry_b", ReactionCue.FoodBurned, ReactionPriority.High, ChefSpeaker.Pastry,
                    "Порция сгорела. Это уже декорация к пожару, а не к ужину."),
                Line("submit_savory", ReactionCue.DishSubmitted, ReactionPriority.Normal, ChefSpeaker.Savory,
                    "Блюдо выставлено. Теперь говорит то, что на тарелке."),
                Line("submit_pastry", ReactionCue.DishSubmitted, ReactionPriority.Normal, ChefSpeaker.Pastry,
                    "Подача зафиксирована. Оценим именно то, что подано."),
                Line("raw_savory_a", ReactionCue.JudgingRaw, ReactionPriority.System, ChefSpeaker.Savory,
                    "Обязательный белковый продукт остался сырым. {detail}"),
                Line("raw_savory_b", ReactionCue.JudgingRaw, ReactionPriority.System, ChefSpeaker.Savory,
                    "Белковый продукт не дошёл до готовности. Подача этого не исправляет. {detail}"),
                Line("raw_pastry_a", ReactionCue.JudgingRaw, ReactionPriority.System, ChefSpeaker.Pastry,
                    "Вид блюда не заменяет готовность: обязательный белковый продукт сырой. {detail}"),
                Line("raw_pastry_b", ReactionCue.JudgingRaw, ReactionPriority.System, ChefSpeaker.Pastry,
                    "Сырой белковый компонент остался в подаче. Это уже вопрос готовности. {detail}"),
                Line("judged_burn_savory_a", ReactionCue.JudgingBurned, ReactionPriority.System, ChefSpeaker.Savory,
                    "Главный компонент сгорел. Тёмная корочка здесь уже не бонус. {detail}"),
                Line("judged_burn_savory_b", ReactionCue.JudgingBurned, ReactionPriority.System, ChefSpeaker.Savory,
                    "Сгоревший главный продукт попал в подачу. Дым не заменяет вкус. {detail}"),
                Line("judged_burn_pastry_a", ReactionCue.JudgingBurned, ReactionPriority.System, ChefSpeaker.Pastry,
                    "Главное в блюде сгорело. Подпись «румяное» уже не поможет. {detail}"),
                Line("judged_burn_pastry_b", ReactionCue.JudgingBurned, ReactionPriority.System, ChefSpeaker.Pastry,
                    "Сгоревший главный компонент остался на тарелке. Оценка это учитывает. {detail}"),
                Line("good_savory_a", ReactionCue.JudgingGood, ReactionPriority.System, ChefSpeaker.Savory,
                    "Вот блюдо, у которого готовка и подача договорились. {detail}"),
                Line("good_savory_b", ReactionCue.JudgingGood, ReactionPriority.System, ChefSpeaker.Savory,
                    "У этой подачи есть аргументы. Я бы начал с вилки. {detail}"),
                Line("good_pastry_a", ReactionCue.JudgingGood, ReactionPriority.System, ChefSpeaker.Pastry,
                    "Хорошая работа с блюдом. Результат уверенно держится на тарелке. {detail}"),
                Line("good_pastry_b", ReactionCue.JudgingGood, ReactionPriority.System, ChefSpeaker.Pastry,
                    "Подача получилась убедительной. Можно обойтись без оправдательного гарнира. {detail}"),
                Line("weak_savory", ReactionCue.JudgingWeak, ReactionPriority.System, ChefSpeaker.Savory,
                    "Эта подача пока не убедила. Блюду не хватило аргументов. {detail}"),
                Line("weak_pastry", ReactionCue.JudgingWeak, ReactionPriority.System, ChefSpeaker.Pastry,
                    "К этой подаче стоит вернуться после работы над ошибками. {detail}"),
                Line("experiment_savory", ReactionCue.JudgingExperimental, ReactionPriority.System, ChefSpeaker.Savory,
                    "Знакомое ядро рецепта здесь не сложилось. Эксперимент засчитан. {detail}"),
                Line("experiment_pastry", ReactionCue.JudgingExperimental, ReactionPriority.System, ChefSpeaker.Pastry,
                    "Получилось экспериментальное блюдо. Эталон пока смотрит издалека. {detail}"),
                Line("time60_savory", ReactionCue.Time60, ReactionPriority.System, ChefSpeaker.Savory,
                    "Осталось 60 секунд. Пора собирать подачу!"),
                Line("time60_pastry", ReactionCue.Time60, ReactionPriority.System, ChefSpeaker.Pastry,
                    "60 секунд. Проверьте готовность и начинайте подачу."),
                Line("time30_savory", ReactionCue.Time30, ReactionPriority.System, ChefSpeaker.Savory,
                    "30 секунд. На финиш идёт блюдо, а не список планов."),
                Line("time30_pastry", ReactionCue.Time30, ReactionPriority.System, ChefSpeaker.Pastry,
                    "Осталось 30 секунд. Проверьте, что реально лежит на тарелке."),
                Line("time10_savory", ReactionCue.Time10, ReactionPriority.System, ChefSpeaker.Savory,
                    "10 секунд. Финальный ход — подача!"),
                Line("time10_pastry", ReactionCue.Time10, ReactionPriority.System, ChefSpeaker.Pastry,
                    "Осталось 10 секунд. Завершайте подачу."),
                Line("timeup_savory", ReactionCue.TimeUp, ReactionPriority.System, ChefSpeaker.Savory,
                    "Время! Готовка закрыта. Оцениваем фактическое блюдо."),
                Line("timeup_pastry", ReactionCue.TimeUp, ReactionPriority.System, ChefSpeaker.Pastry,
                    "Время вышло. Новые действия закончены, результат остаётся на оценке."),
                Sequence("burn_bicker", ReactionCue.FoodBurned, ReactionPriority.High,
                    ChefSpeaker.Savory, "Эта порция теперь претендует на звание угля.",
                    ChefSpeaker.Pastry, "Титул ей уже дали. Для ужина придётся поискать другое."),
                Sequence("ready_bicker", ReactionCue.FoodReady, ReactionPriority.Normal,
                    ChefSpeaker.Pastry, "Порог готовности есть. Дальше нужен вовремя выключенный огонь.",
                    ChefSpeaker.Savory, "Согласен. Передержка тоже любит эффектный выход.")
            };
            var error = config.Validate();
            if (error != null) throw new InvalidOperationException(error);
            AssetDatabase.CreateAsset(config, path);
            return config;
        }

        private static ReactionDefinition Line(string id, ReactionCue cue, ReactionPriority priority, ChefSpeaker chef, string text)
        {
            return new ReactionDefinition
            {
                Id = id, Cue = cue, Priority = priority, PrimaryChef = chef,
                Lines = new[] { new ReactionLineDefinition { Id = id + "_line", Chef = chef, Text = text } }
            };
        }

        private static ReactionDefinition Sequence(string id, ReactionCue cue, ReactionPriority priority,
            ChefSpeaker firstChef, string firstText, ChefSpeaker secondChef, string secondText)
        {
            return new ReactionDefinition
            {
                Id = id, Cue = cue, Priority = priority, PrimaryChef = firstChef,
                Lines = new[]
                {
                    new ReactionLineDefinition { Id = id + "_first", Chef = firstChef, Text = firstText },
                    new ReactionLineDefinition { Id = id + "_reply", Chef = secondChef, Text = secondText }
                }
            };
        }

        private static Text FindText(Transform panel, string name)
        {
            var child = panel.Find(name);
            return child == null ? null : child.GetComponent<Text>();
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 point, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            Undo.RegisterCreatedObjectUndo(go, "Saved chef reaction UI");
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = point;
            rect.sizeDelta = size;
            return rect;
        }

        private static Text Text(string name, Transform parent, Vector2 point, Vector2 size, int fontSize,
            Color color, string value, GameBootstrap bootstrap)
        {
            var rect = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), point, size);
            var text = Undo.AddComponent<Text>(rect.gameObject);
            text.font = bootstrap.Hud.Status.font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.text = value;
            EditorUtility.SetDirty(text);
            return text;
        }
    }
}
