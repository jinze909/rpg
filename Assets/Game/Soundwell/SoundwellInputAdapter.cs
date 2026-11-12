using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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
        private static bool ignoreHeldToggleUntilRelease;

        public static void SetEntryContext()
        {
            EnsureLoaded();
            ignoreHeldToggleUntilRelease = false;
            actions.Disable();
            entryMap.Enable();
        }

        public static void SetGameplayContext()
        {
            EnsureLoaded();
            // Re-enabling an InputAction while Tab/Start is held may treat
            // its initial state as another press, reopening a just-closed menu.
            ignoreHeldToggleUntilRelease |= ToggleControlHeld();
            actions.Disable();
            gameplayMap.Enable();
        }

        // UGUI owns navigation, Submit and Cancel while world actions are
        // disabled. Keep ONLY ToggleMenu enabled so Tab / Gamepad Start can
        // close the focus menu; otherwise it can open but cannot close.
        public static void SetUiContext()
        {
            EnsureLoaded();
            // A held opening press must not immediately close the newly
            // focused menu when this action is disabled and enabled again.
            ignoreHeldToggleUntilRelease |= ToggleControlHeld();
            actions.Disable();
            toggleMenu.Enable();
        }

        public static bool InteractPressed() { EnsureLoaded(); return interact.WasPressedThisFrame(); }

        // The entry shortcut follows the player's saved keyboard remap.
        // Gamepad South is also UGUI Submit, so it must still respect a
        // focused settings button rather than entering the trial.
        public static bool EntryKeyboardShortcutPressed()
        {
            EnsureLoaded();
            return interact.WasPressedThisFrame() &&
                interact.activeControl != null && interact.activeControl.device is Keyboard;
        }

        // Both the action's button state and its physical bound controls are
        // checked so a held Start/Tab remains suppressed across re-enables.
        // Releasing it rearms the next intentional menu-toggle press.
        public static bool ToggleMenuPressed()
        {
            EnsureLoaded();
            if (ignoreHeldToggleUntilRelease)
            {
                if (!ToggleControlHeld())
                    ignoreHeldToggleUntilRelease = false;
                return false;
            }
            return toggleMenu.WasPressedThisFrame();
        }

        private static bool ToggleControlHeld()
        {
            foreach (InputControl control in toggleMenu.controls)
            {
                if (control is ButtonControl button && button.isPressed)
                    return true;
            }
            return toggleMenu.IsPressed();
        }

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
            // ORK/Makinom may park an inactive UI owner and reactivate it
            // later. Excluding inactive objects here leaves a duplicate
            // EventSystem able to steal focus after scene handoff.
            var systems = UnityEngine.Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            EventSystem current = EventSystem.current;
            // Prefer an ACTIVE Soundwell UI owner, never one on an inactive
            // GameObject. Enabling its component cannot activate the host.
            EventSystem eventSystem = systems.FirstOrDefault(system =>
                    system.gameObject.activeInHierarchy &&
                    system.name == "Soundwell EventSystem" &&
                    system.GetComponent<InputSystemUIInputModule>() != null) ??
                (current != null && current.gameObject.activeInHierarchy &&
                    current.GetComponent<InputSystemUIInputModule>() != null ? current : null) ??
                systems.FirstOrDefault(system =>
                    system.gameObject.activeInHierarchy &&
                    system.GetComponent<InputSystemUIInputModule>() != null) ??
                (current != null && current.gameObject.activeInHierarchy ? current : null) ??
                systems.FirstOrDefault(system => system.gameObject.activeInHierarchy);

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
            RestoreSavedBindingOverrides(actions);
            initialized = true;
        }

        // A malformed or obsolete local binding file must never prevent the
        // game from reaching its Entry/Gameplay action maps. Preserve valid
        // player rebinding data; only a proven invalid preference is cleared.
        private static void RestoreSavedBindingOverrides(InputActionAsset target)
        {
            string saved = PlayerPrefs.GetString(BindingOverridesKey, string.Empty);
            if (string.IsNullOrEmpty(saved))
                return;
            try
            {
                string trimmed = saved.Trim();
                if (trimmed.Length < 2 || trimmed[0] != '{' ||
                    trimmed[trimmed.Length - 1] != '}')
                    throw new FormatException("Saved input binding overrides are not JSON.");
                target.LoadBindingOverridesFromJson(saved);
                // Well-formed override JSON can silently disable every path
                // for a required action. In UI mode the game enables only
                // ToggleMenu, so losing all its bindings strands keyboard/pad
                // players in the focus menu. Preserve genuine remaps, but
                // reject a preference that removes every usable control.
                foreach (var required in new[]
                {
                    (Map: "Entry", Action: "Interact"),
                    (Map: "Gameplay", Action: "Move"),
                    (Map: "Gameplay", Action: "Back"),
                    (Map: "Gameplay", Action: "ToggleMenu"),
                    // Soundwell's existing bell/escort puzzle is controlled
                    // through these actual game-owned world input actions.
                    // A valid JSON override that unbinds one of them must
                    // not silently make the authored trial controls unusable.
                    (Map: "Gameplay", Action: "Ring"),
                    (Map: "Gameplay", Action: "Wrap"),
                    (Map: "Gameplay", Action: "Carry"),
                    (Map: "Gameplay", Action: "Gate"),
                    // Retry is the only input route back to a failed trial.
                    // A saved unbound override must not strand the player.
                    (Map: "Gameplay", Action: "Retry")
                })
                {
                    InputAction action = target.FindActionMap(required.Map, true)
                        .FindAction(required.Action, true);
                    if (!HasUsableBinding(action))
                        throw new InvalidOperationException(
                            "Saved input overrides disabled all bindings for " +
                            required.Map + "/" + required.Action + ".");
                }
            }
            catch (Exception error)
            {
                target.RemoveAllBindingOverrides();
                PlayerPrefs.DeleteKey(BindingOverridesKey);
                PlayerPrefs.Save();
                Debug.LogWarning("Soundwell binding overrides are invalid; restored default controls (" +
                    error.GetType().Name + ").");
            }
        }

        // Movement uses 2DVector composite roots followed by individual
        // direction parts. A saved override may disable the composite root
        // and both direct gamepad paths while leaving the orphaned W/A/S/D
        // parts with nonempty effectivePath. Those parts cannot move the player.
        // Require either a direct binding or an enabled composite with a
        // still-bound child; keep legitimate partial keyboard remaps usable.
        private static bool HasUsableBinding(InputAction action)
        {
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (string.IsNullOrWhiteSpace(binding.effectivePath))
                    continue;
                if (!binding.isComposite && !binding.isPartOfComposite)
                    return true;
                if (!binding.isComposite)
                    continue;
                for (int part = i + 1; part < bindings.Count &&
                    bindings[part].isPartOfComposite; part++)
                {
                    if (!string.IsNullOrWhiteSpace(bindings[part].effectivePath))
                        return true;
                }
            }
            return false;
        }

        private static void RequireBinding(InputAction action, string path)
        {
            if (!action.bindings.Any(b => string.Equals(b.path, path, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Input action " + action.name + " is missing binding " + path + ".");
        }
    }
}
