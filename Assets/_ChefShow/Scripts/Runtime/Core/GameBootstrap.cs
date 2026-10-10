using System;
using System.Collections.Generic;
using ChefShow.Data;
using ChefShow.Contestants;
using ChefShow.Player;
using ChefShow.Inventory;
using ChefShow.Ingredients;
using ChefShow.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace ChefShow.Core
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        public PrototypeGameConfig Config;
        public InputActionAsset InputDefinition;
        public FirstPersonRig Player;
        public PrototypeHud Hud;
        public InventoryController Inventory;
        public ServingController Serving;
        public DishwareController Dishware;
        public KitchenLayoutConfig Layout;
        public ChefShow.Cooking.MixingController Mixing;
        public ToolDrawerController Tools;
        public PreparationController Preparation;
        public ChefShow.Cooking.CookingController Cooking;
        public InputSystemUIInputModule UiInput;
        private InputActionAsset input;
        private readonly List<InputActionReference> uiReferences = new List<InputActionReference>();
        private bool paused;
        private bool debug;
        private float sensitivity;
        public PrototypeRun Run { get; private set; }
        public bool IsPaused => paused;
        // Automation can drive an unfocused Editor; ordinary runs keep focus-loss pause enabled.
        public bool AutoPauseOnFocusLoss { get; set; } = true;
        public bool DebugAvailable => Config != null && Config.PrototypeDebugEnabled && (Application.isEditor || Debug.isDebugBuild);

        private void Awake()
        {
            if (Config == null || InputDefinition == null || Player == null || Hud == null || UiInput == null)
            {
                Debug.LogError("Chef Show: обязательные ссылки не заполнены; выполните Validate Prototype.", this);
                enabled = false;
                return;
            }
            var error = Config.Validate();
            if (error != null) { Debug.LogError("Chef Show: " + error, this); enabled = false; return; }
            var actor = Player.GetComponent<PrototypeActor>();
            if (actor == null || actor.Team != Config.PlayerTeam)
            {
                const string message = "Команда в конфиге отличается от сцены. Заново выполните Build Prototype Scene и создайте новую рабочую копию.";
                Debug.LogError("Chef Show: " + message, this);
                Hud.Status.text = message;
                enabled = false;
                return;
            }
            input = Instantiate(InputDefinition);
            input.name = "ChefShowInput_Runtime";
            // UI и игрок используют одну runtime-копию, исходный asset не меняется.
            UiInput.actionsAsset = input;
            UiInput.point = Reference("UI/Point");
            UiInput.move = Reference("UI/Navigate");
            UiInput.leftClick = Reference("UI/Click");
            UiInput.scrollWheel = Reference("UI/ScrollWheel");
            UiInput.submit = Reference("UI/Submit");
            UiInput.cancel = Reference("UI/Cancel");
            UiInput.rightClick = null;
            UiInput.middleClick = null;
            UiInput.trackedDevicePosition = null;
            UiInput.trackedDeviceOrientation = null;
            sensitivity = Config.LookSensitivity;
            Player.Initialize(Config, input);
            if (Inventory != null)
            {
                var inventoryError = Inventory.Validate();
                if (inventoryError != null) { Debug.LogError("Chef Show: " + inventoryError, this); enabled = false; return; }
                Inventory.Initialize(this, input);
                Player.CancelInteraction = Inventory.CancelHeld;
            }
            if (Tools != null)
            {
                var toolError = Tools.Validate();
                if (toolError != null || Inventory == null)
                { Debug.LogError("Chef Show: " + (toolError ?? "Ящику нужен инвентарь."), this); enabled = false; return; }
                Tools.Initialize(this, input);
                Player.CancelInteraction = Inventory.CancelHeld;
            }
            if (Preparation != null)
            {
                var preparationError = Inventory == null || Tools == null ? "Нарезке нужны инвентарь и инструменты." : Preparation.Validate(Inventory);
                if (preparationError != null) { Debug.LogError("Chef Show: " + preparationError, this); enabled = false; return; }
                Preparation.Initialize(this, input);
            }
            if (Cooking != null)
            {
                var cookingError = Cooking.Validate(Inventory);
                if (cookingError != null || Tools == null) { Debug.LogError("Chef Show: " + (cookingError ?? "Готовке нужны инструменты."), this); enabled = false; return; }
                Cooking.Initialize(this, input);
            }
            if(Mixing!=null)
            {var mixError=Mixing.Validate();if(mixError!=null){Debug.LogError(mixError,this);enabled=false;return;}Mixing.Initialize(this,input);}
            if(Dishware!=null){var wareError=Dishware.Validate();if(wareError!=null){Debug.LogError(wareError,this);enabled=false;return;}Dishware.Initialize(this,input);}
            if (Serving != null)
            { var servingError=Serving.Validate(); if(servingError!=null){Debug.LogError(servingError,this);enabled=false;return;} Serving.Initialize(this,input); }
            Hud.Resume.onClick.AddListener(() => SetPaused(false));
            Hud.Restart.onClick.AddListener(RestartShow);
            Hud.DebugRestart.onClick.AddListener(RestartShow);
            Hud.Timer30.onClick.AddListener(() => SetDebugTimer(30));
            Hud.Timer60.onClick.AddListener(() => SetDebugTimer(60));
            Hud.Timer240.onClick.AddListener(() => SetDebugTimer(240));
            Hud.Sensitivity.SetValueWithoutNotify(sensitivity);
            Hud.Sensitivity.onValueChanged.AddListener(value => { sensitivity = value; Player.SetSensitivity(value); });
            RestartShow();
        }

        private InputActionReference Reference(string path)
        {
            var reference = InputActionReference.Create(input.FindAction(path, true));
            uiReferences.Add(reference);
            return reference;
        }

        private void Update()
        {
            if (Run == null) return;
            if (input.FindAction("UI/Cancel", true).WasPressedThisFrame())
            {
                if (debug) { debug = false; SetPaused(false); }
                else SetPaused(!paused);
            }
            if (DebugAvailable && input.FindAction("Debug/Toggle", true).WasPressedThisFrame())
            {
                debug = !debug;
                SetPaused(debug);
            }
            Run.Tick(Time.unscaledDeltaTime);
            bool gameplay = !paused && Run.RemainingSeconds > 0;
            Player.Step(Run.Clock, gameplay);
            bool toolCommand = Tools != null && Tools.Step(gameplay);
            bool dishwareCommand=Dishware!=null && Dishware.Step(gameplay,toolCommand);
            bool cookCommand = Cooking != null && Cooking.Step(gameplay, toolCommand || dishwareCommand);
            bool mixCommand=Mixing!=null && Mixing.Step(gameplay,toolCommand || cookCommand || dishwareCommand);
            bool servingCommand = Serving != null && Serving.Step(gameplay, toolCommand || cookCommand || mixCommand || dishwareCommand);
            bool preparationCommand = Preparation != null && Preparation.Step(gameplay, toolCommand || cookCommand || servingCommand || mixCommand || dishwareCommand);
            if (Inventory != null) Inventory.Step(gameplay, toolCommand || cookCommand || preparationCommand || servingCommand || mixCommand || dishwareCommand);
            UpdateMaps();
            bool task = !paused && input.FindAction((Player.Focused ? "Station" : "Gameplay") + "/Task", true).IsPressed();
            Hud.Present(Run, Player, paused, debug, task, Inventory, Tools, Preparation, Cooking, Serving, Mixing, Dishware);
        }

        public void SetPaused(bool value)
        {
            if (Run == null) return;
            paused = value;
            if (!value) debug = false;
            Run.SetPaused(value);
            UpdateMaps();
            if (value && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(debug ? Hud.DebugRestart.gameObject : Hud.Resume.gameObject);
        }

        public void RestartShow()
        {
            Run?.Dispose();
            Run = new PrototypeRun(Config.RoundDurationSeconds, Config.RunSeed,
                (type, error) => Debug.LogError($"Chef Show event {type.Name}: {error}"), Config.BasketCapacity, Config.TrayCapacity, Config.PlayerTeam, Cooking == null ? null : Cooking.Config.Capture(),Dishware==null?null:Dishware.Config.Capture());
            paused = debug = false;
            Player.ResetRig();
            if (Inventory != null) Inventory.ResetPresentation();
            if (Tools != null) Tools.ResetPresentation();
            if (Preparation != null) Preparation.ResetPresentation();
            if (Cooking != null) Cooking.ResetPresentation();
            if (Serving != null) Serving.ResetPresentation();
            if(Mixing!=null)Mixing.ResetPresentation();
            if(Dishware!=null)Dishware.ResetPresentation();
            Player.SetSensitivity(sensitivity);
            UpdateMaps();
            Run.Events.Publish(new RunStarted(Run.RunId, Run.Seed));
            Debug.Log($"Chef Show: start run={Run.RunId} seed={Run.Seed}; {(Cooking != null ? "сковорода/кастрюля" : Inventory == null ? "арена" : "продукты и перенос")}; полный выпуск ещё не реализован.", this);
        }

        private void UpdateMaps()
        {
            SetMap("Gameplay", !paused && !Player.Focused && Run.RemainingSeconds > 0);
            SetMap("Station", !paused && Player.Focused && Run.RemainingSeconds > 0);
            SetMap("UI", true);
            SetMap("Debug", DebugAvailable);
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }

        private void SetMap(string name, bool active)
        {
            var map = input.FindActionMap(name, true);
            if (active && !map.enabled) map.Enable();
            else if (!active && map.enabled) map.Disable();
        }

        public void SetDebugTimer(float seconds)
        {
            if (!DebugAvailable || Run == null) return;
            Run.SetRemaining(seconds);
            Debug.Log($"Chef Show: DEBUG timer={seconds}; run={Run.RunId}", this);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && AutoPauseOnFocusLoss && Run != null) SetPaused(true);
        }

        private void OnDestroy()
        {
            Run?.Dispose();
            if (input != null) { input.Disable(); Destroy(input); }
            foreach (var reference in uiReferences) if (reference != null) Destroy(reference);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
