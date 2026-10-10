using System;
using System.Linq;
using ChefShow.Contestants;
using ChefShow.Core;
using ChefShow.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChefShow.Editor
{
    public static class PrototypeValidator
    {
        [MenuItem("Tools/Chef Show/Validate Prototype")]
        public static void ValidateActiveScene() => ValidateScene(SceneManager.GetActiveScene());

        public static void ValidateScene(Scene scene)
        {
            var objects = scene.GetRootGameObjects();
            T[] All<T>() where T : Component => objects.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            var bootstraps = All<GameBootstrap>();
            Require(bootstraps.Length == 1, "Нужен один GameBootstrap.");
            var bootstrap = bootstraps[0];
            Require(bootstrap.Config != null && bootstrap.InputDefinition != null && bootstrap.Player != null
                && bootstrap.Hud != null && bootstrap.UiInput != null, "Не заполнены обязательные ссылки.");
            Require(bootstrap.Config.Validate() == null, bootstrap.Config.Validate());
            Require(All<Camera>().Length == 1 && All<AudioListener>().Length == 1, "Нужна одна camera/listener.");
            Require(All<EventSystem>().Length == 1 && All<InputSystemUIInputModule>().Length == 1
                && All<StandaloneInputModule>().Length == 0, "Нужен один EventSystem/InputSystemUIInputModule.");
            Require(All<FirstPersonRig>().Length == 1, "Нужен один first-person player.");
            var actors = All<PrototypeActor>();
            var contestants = actors.Where(a => a.Kind != PrototypeActorKind.Chef).ToArray();
            Require(contestants.Length == 12 && contestants.Count(a => a.Kind == PrototypeActorKind.Player) == 1
                && contestants.Count(a => a.Kind == PrototypeActorKind.Npc) == 11, "Состав должен быть игрок + 11 NPC.");
            Require(contestants.Count(a => a.Team == Data.TeamId.A) == 6 && contestants.Count(a => a.Team == Data.TeamId.B) == 6,
                "Нужно по шесть в A/B.");
            Require(actors.Select(a => a.StableId).Distinct().Count() == 14 && actors.All(a => !string.IsNullOrWhiteSpace(a.StableId)),
                "IDs должны быть уникальными и заполненными.");
            Require(actors.Count(a => a.Kind == PrototypeActorKind.Chef) == 2, "Нужны два шефа.");
            Require(All<PrototypeInteractable>().Count(a => a.IsPlayerStation) == 1, "Нужна одна станция игрока.");
            var stations = All<PrototypeInteractable>().Where(a => a.name.StartsWith("Station_")).ToArray();
            Require(stations.Length == 12 && stations.Select(s => s.name).Distinct().Count() == 12, "Нужны 12 уникальных станций.");
            Require(contestants.Single(a => a.Kind == PrototypeActorKind.Player).Team == bootstrap.Config.PlayerTeam
                && stations.Single(s => s.IsPlayerStation).Team == bootstrap.Config.PlayerTeam,
                "Команда игрока/станции отличается от конфига: перегенерируйте сцену.");
            var transforms = All<Transform>();
            Require(transforms.Single(t => t.name == "TeamDishSlots").childCount == 12
                && transforms.Single(t => t.name == "FinalDishSlots").childCount == 4, "Нужны 12 обычных и 4 финальных места подачи.");
            foreach (string name in new[] { "Gameplay", "Station", "UI", "Debug" })
                Require(bootstrap.InputDefinition.FindActionMap(name) != null, "Нет action map " + name);
            if(bootstrap.Judging!=null)Require(bootstrap.Judging.Validate()==null,bootstrap.Judging.Validate());
            if(bootstrap.Recipes!=null)Require(bootstrap.Recipes.Validate()==null,bootstrap.Recipes.Validate());
            if(bootstrap.RecipeBook!=null)
            {
                Require(bootstrap.RecipeBook.Validate()==null,bootstrap.RecipeBook.Validate());
                Require(bootstrap.InputDefinition.FindAction("UI/RecipeBook")!=null && bootstrap.InputDefinition.FindAction("UI/RecipeRightHeld")!=null,"Нет ввода книги.");
            }
            if (bootstrap.Inventory != null)
            {
                Require(bootstrap.Inventory.Validate() == null, bootstrap.Inventory.Validate());
                Require(bootstrap.Inventory.PlayerStationId == contestants.Single(a => a.Kind == PrototypeActorKind.Player).StableId,
                    "Станция инвентаря отличается от участника игрока.");
                foreach (string map in new[] { "Gameplay", "Station" })
                    foreach (string action in new[] { "Basket", "DropBasket", "Primary", "Interact", "Cancel", "Task" })
                        Require(bootstrap.InputDefinition.FindAction(map + "/" + action) != null, "Нет inventory action " + map + "/" + action);
            }
            if (bootstrap.Tools != null)
            {
                Require(bootstrap.Tools.Validate() == null, bootstrap.Tools.Validate());
                Require(bootstrap.Tools.Drawers.Select(d => d.StationId).Distinct().Count() == 12, "Нужны 12 уникальных ящиков.");
            }
            if (bootstrap.Preparation != null)
                Require(bootstrap.Preparation.Validate(bootstrap.Inventory) == null, bootstrap.Preparation.Validate(bootstrap.Inventory));
            if (bootstrap.Cooking != null)
                Require(bootstrap.Cooking.Validate(bootstrap.Inventory) == null, bootstrap.Cooking.Validate(bootstrap.Inventory));
            if(bootstrap.Serving!=null) Require(bootstrap.Serving.Validate()==null,bootstrap.Serving.Validate());
            if(bootstrap.Dishware!=null)Require(bootstrap.Dishware.Validate()==null,bootstrap.Dishware.Validate());
            if(bootstrap.Mixing!=null) Require(bootstrap.Mixing.Validate()==null,bootstrap.Mixing.Validate());
            Require(All<Text>().All(t => t.font != null), "Отсутствует шрифт UI.");
            foreach (var text in All<Text>()) PrototypeSceneBuilder.RequireCyrillic(text.font);
            Require(Shader.Find("Universal Render Pipeline/Lit") != null, "URP shader не найден.");
            Debug.Log("Chef Show validator: PASS — " + (bootstrap.Cooking != null ? "foundation + inventory + pan/pot" : bootstrap.Inventory == null ? "foundation" : "foundation + inventory") + ". Scoring и полный выпуск ещё не реализованы.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Chef Show validator: " + message);
        }
    }
}
