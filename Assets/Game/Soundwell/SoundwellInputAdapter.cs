using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// The sole game-owned input adapter for the Soundwell sample.
    /// ORK's original Input Key data remains untouched; this adapter owns
    /// only the trial's movement, actions, entry prompt and focus handoff.
    /// </summary>
    public static class SoundwellInputAdapter
    {
        private const string ResourcePath = "Input/SoundwellControls";
        private const string BindingOverridesKey = "rpg.soundwell.input.binding-overrides.v1";
        private static InputActionAsset actions;
        private static InputActionMap entryMap;
        private static InputActionMap gameplayMap;
        private static InputAction interact, move, ring, wrap, retry, back, carry, gate, toggleMenu;
        private static bool initialized;

        public static void SetEntryContext()
        {
            EnsureLoaded();
            actions.Disable();
            entryMap.Enable();
        }

        public static void SetGameplayContext()
        {
            EnsureLoaded();
            actions.Disable();
            gameplayMap.Enable();
        }

        // UGUI owns navigation, Submit and Cancel while world actions are
        // disabled. Keep ONLY ToggleMenu enabled so Tab / Gamepad Start can
        // close the focus menu; otherwise it can open but cannot close.
        public static void SetUiContext()
        {
            EnsureLoaded();
            actions.Disable();
            toggleMenu.Enable();
        }

        public static bool InteractPressed() { EnsureLoaded(); return interact.WasPressedThisFrame(); }

        // F2 is the explicit entry shortcut. Gamepad South is also UGUI
        // Submit; identify the active binding so a focused settings button
        // cannot accidentally enter the trial when the player presses A.
        public static bool EntryKeyboardShortcutPressed()
        {
            EnsureLoaded();
            return interact.WasPressedThisFrame() &&
                Keyboard.current != null && interact.activeControl == Keyboard.current.f2Key;
        }

        public static bool ToggleMenuPressed() { EnsureLoaded(); return toggleMenu.WasPressedThisFrame(); }

        public static bool IsEntryContextActive
        {
            get { EnsureLoaded(); return entryMap.enabled && !gameplayMap.enabled; }
        }

        public static bool IsGameplayContextActive
        {
            get { EnsureLoaded(); return move.enabled && gameplayMap.enabled && !entryMap.enabled; }
        }

        public static bool IsUiContextActive
        {
            // Enabling a single action also marks its map enabled. Check the
            // world Move action, rather than the map, to distinguish focus mode.
            get { EnsureLoaded(); return !entryMap.enabled && !move.enabled && toggleMenu.enabled; }
        }

        public static SoundwellControlFrame ReadGameplay()
        {
            EnsureLoaded();
            return SoundwellControlFrame.FromActions(move.ReadValue<Vector2>(), ring.WasPressedThisFrame(),
                wrap.WasPressedThisFrame(), retry.WasPressedThisFrame(), back.WasPressedThisFrame(),
                carry.WasPressedThisFrame(), gate.WasPressedThisFrame());
        }

        public static void SaveBindingOverrides()
        {
            EnsureLoaded();
            PlayerPrefs.SetString(BindingOverridesKey, actions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public static void ResetBindingOverrides()
        {
            EnsureLoaded();
            actions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(BindingOverridesKey);
            PlayerPrefs.Save();
        }

        public static void ValidateActionAsset()
        {
            EnsureLoaded();
            if (actions.actionMaps.Count != 2)
                throw new InvalidOperationException("Soundwell must have exactly Entry and Gameplay action maps.");
            RequireBinding(interact, "<Keyboard>/f2");
            RequireBinding(interact, "<Gamepad>/buttonSouth");
            foreach (string path in new[] { "2DVector", "<Keyboard>/w", "<Keyboard>/a", "<Keyboard>/s",
                "<Keyboard>/d", "<Keyboard>/upArrow", "<Keyboard>/leftArrow", "<Keyboard>/downArrow",
                "<Keyboard>/rightArrow", "<Gamepad>/leftStick", "<Gamepad>/dpad" })
                RequireBinding(move, path);
            RequireBinding(ring, "<Keyboard>/e"); RequireBinding(ring, "<Gamepad>/buttonSouth");
            RequireBinding(wrap, "<Keyboard>/q"); RequireBinding(wrap, "<Gamepad>/buttonWest");
            RequireBinding(retry, "<Keyboard>/r"); RequireBinding(retry, "<Gamepad>/buttonNorth");
            RequireBinding(back, "<Keyboard>/escape"); RequireBinding(back, "<Gamepad>/buttonEast");
            RequireBinding(carry, "<Keyboard>/f"); RequireBinding(carry, "<Gamepad>/leftShoulder");
            RequireBinding(gate, "<Keyboard>/g"); RequireBinding(gate, "<Gamepad>/rightShoulder");
            RequireBinding(toggleMenu, "<Keyboard>/tab"); RequireBinding(toggleMenu, "<Gamepad>/start");
        }

        public static EventSystem EnsureEventSystem(Transform owner)
        {
            var systems = UnityEngine.Object.FindObjectsByType<EventSystem>();
            EventSystem current = EventSystem.current;
            // Keep the Soundwell-owned module when a later ORK/Makinom
            // EventSystem temporarily becomes EventSystem.current. Picking
            // current first would destroy our own UI input owner and could
            // lose UGUI button selection during an encounter transition.
            EventSystem eventSystem = systems.FirstOrDefault(system =>
                    system.name == "Soundwell EventSystem" &&
                    system.GetComponent<InputSystemUIInputModule>() != null) ??
                (current != null && current.GetComponent<InputSystemUIInputModule>() != null
                    ? current : null) ??
                systems.FirstOrDefault(system =>
                    system.GetComponent<InputSystemUIInputModule>() != null) ??
                current ?? systems.FirstOrDefault();

            if (eventSystem == null)
            {
                var instance = new GameObject("Soundwell EventSystem", typeof(EventSystem));
                if (owner != null) instance.transform.SetParent(owner, false);
                eventSystem = instance.GetComponent<EventSystem>();
            }

            // ORK may create its own EventSystem after the scene has loaded.
            // Keep the project-owned New Input System instance and retire any
            // duplicates instead of failing the whole trial or running two UIs.
            foreach (EventSystem duplicate in systems)
            {
                if (duplicate == null || duplicate == eventSystem)
                    continue;
                foreach (BaseInputModule duplicateModule in duplicate.GetComponents<BaseInputModule>())
                {
                    duplicateModule.enabled = false;
                    UnityEngine.Object.Destroy(duplicateModule);
                }
                duplicate.enabled = false;
                UnityEngine.Object.Destroy(duplicate);
            }

            if (!eventSystem.enabled)
                eventSystem.enabled = true;
            if (EventSystem.current != eventSystem)
            {
                eventSystem.enabled = false;
                eventSystem.enabled = true;
            }

            foreach (var module in eventSystem.GetComponents<BaseInputModule>())
            {
                if (module is InputSystemUIInputModule) continue;
                module.enabled = false;
                UnityEngine.Object.Destroy(module);
            }
            var ui = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (ui == null) ui = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            ui.enabled = true;
            if (ui.actionsAsset == null) ui.AssignDefaultActions();
            if (ui.actionsAsset == null) throw new InvalidOperationException("New Input System UI module has no action asset.");
            var active = eventSystem.GetComponents<BaseInputModule>().Where(m => m != null && m.enabled).ToArray();
            if (active.Length != 1 || active[0] != ui)
                throw new InvalidOperationException("EventSystem must have exactly one active New Input UI module.");
            return eventSystem;
        }

        private static void EnsureLoaded()
        {
            if (initialized) return;
            actions = Resources.Load<InputActionAsset>(ResourcePath);
            if (actions == null) throw new InvalidOperationException("Missing Resources/Input/SoundwellControls.inputactions asset.");
            entryMap = actions.FindActionMap("Entry", true);
            gameplayMap = actions.FindActionMap("Gameplay", true);
            interact = entryMap.FindAction("Interact", true);
            move = gameplayMap.FindAction("Move", true);
            ring = gameplayMap.FindAction("Ring", true);
            wrap = gameplayMap.FindAction("Wrap", true);
            retry = gameplayMap.FindAction("Retry", true);
            back = gameplayMap.FindAction("Back", true);
            carry = gameplayMap.FindAction("Carry", true);
            gate = gameplayMap.FindAction("Gate", true);
            toggleMenu = gameplayMap.FindAction("ToggleMenu", true);
            string overrides = PlayerPrefs.GetString(BindingOverridesKey, string.Empty);
            if (!string.IsNullOrEmpty(overrides)) actions.LoadBindingOverridesFromJson(overrides);
            initialized = true;
        }

        private static void RequireBinding(InputAction action, string path)
        {
            if (!action.bindings.Any(b => string.Equals(b.path, path, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Input action " + action.name + " is missing binding " + path + ".");
        }
    }
}
