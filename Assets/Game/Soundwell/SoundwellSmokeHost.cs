#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    // Runs only inside the isolated CI Play Mode session.
    public sealed class SoundwellSmokeHost : MonoBehaviour
    {
        private const string SceneName = "SoundwellTrial";
        private string runtimeError;
        private readonly List<float> frameTimeMs = new List<float>(512);
        private int startGc0;
        private int startGc1;
        private int startGc2;
        private long startHeapBytes;
        private long peakHeapBytes;

        [Serializable]
        private sealed class PlayModePerformance
        {
            public int schema_version = 1;
            public string scenario = "soundwell-ring-wrap-retreat-reload-reenter";
            public string sample_source = "UnityEngine.Time.unscaledDeltaTime";
            public string environment = "Unity Editor Play Mode (GameCI Linux container; not player hardware)";
            public string unity_version;
            public int frame_samples;
            public float frame_time_ms_p50;
            public float frame_time_ms_p95;
            public float frame_time_ms_max;
            public int gc_gen0_collections_delta;
            public int gc_gen1_collections_delta;
            public int gc_gen2_collections_delta;
            public long managed_heap_start_bytes;
            public long managed_heap_end_bytes;
            public long managed_heap_peak_bytes;
        }

        public int? ExitCode { get; private set; }

        public void Run()
        {
            startGc0 = GC.CollectionCount(0);
            startGc1 = GC.CollectionCount(1);
            startGc2 = GC.CollectionCount(2);
            startHeapBytes = GC.GetTotalMemory(false);
            peakHeapBytes = startHeapBytes;
            frameTimeMs.Clear();
            SoundwellMemoryProbe.Reset();
            Application.logMessageReceived += RecordRuntimeError;
            StartCoroutine(Drive(VerifyTrial()));
        }

        // Record real Play Mode frame intervals, including trial transitions and scene reload.
        // These headless Editor measurements support comparisons only; they are not FPS targets.
        private void Update()
        {
            if (ExitCode.HasValue)
                return;

            float milliseconds = Time.unscaledDeltaTime * 1000f;
            if (milliseconds > 0f && !float.IsNaN(milliseconds) && !float.IsInfinity(milliseconds))
                frameTimeMs.Add(milliseconds);

            // Heap collection itself has overhead; sample it sparsely rather than each frame.
            if (frameTimeMs.Count > 0 && frameTimeMs.Count % 30 == 0)
                peakHeapBytes = Math.Max(peakHeapBytes, GC.GetTotalMemory(false));
        }

        private void SavePerformanceEvidence()
        {
            if (frameTimeMs.Count < 30)
                throw new InvalidOperationException("Soundwell performance capture has fewer than 30 Play Mode frames.");

            var sorted = frameTimeMs.ToArray();
            Array.Sort(sorted);
            long endHeapBytes = GC.GetTotalMemory(false);
            var evidence = new PlayModePerformance
            {
                unity_version = Application.unityVersion,
                frame_samples = sorted.Length,
                frame_time_ms_p50 = sorted[(int)Math.Ceiling(sorted.Length * 0.50) - 1],
                frame_time_ms_p95 = sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1],
                frame_time_ms_max = sorted[sorted.Length - 1],
                gc_gen0_collections_delta = GC.CollectionCount(0) - startGc0,
                gc_gen1_collections_delta = GC.CollectionCount(1) - startGc1,
                gc_gen2_collections_delta = GC.CollectionCount(2) - startGc2,
                managed_heap_start_bytes = startHeapBytes,
                managed_heap_end_bytes = endHeapBytes,
                managed_heap_peak_bytes = Math.Max(peakHeapBytes, endHeapBytes)
            };
            const string file = "artifacts/p0/performance/soundwell-playmode.json";
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, JsonUtility.ToJson(evidence, true) + "\n");
            SoundwellMemoryProbe.Save();
        }

        private IEnumerator Drive(IEnumerator steps)
        {
            int status = 1;
            while (true)
            {
                bool more;
                try
                {
                    if (runtimeError != null)
                        throw new InvalidOperationException(runtimeError);
                    more = steps.MoveNext();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    break;
                }

                if (!more)
                {
                    if (runtimeError == null)
                    {
                        try
                        {
                            // Only successful end-to-end input paths produce performance evidence.
                            SavePerformanceEvidence();
                            Debug.Log("RPG_SOUNDWELL_SIGNAL_VALIDATED");
                            status = 0;
                        }
                        catch (Exception error)
                        {
                            Debug.LogException(error);
                        }
                    }
                    break;
                }
                yield return steps.Current;
            }

            Application.logMessageReceived -= RecordRuntimeError;
            ExitCode = status;
        }

        private void RecordRuntimeError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                runtimeError ??= message;
        }

        private void OnDestroy() => Application.logMessageReceived -= RecordRuntimeError;

        private static IEnumerator VerifyTrial()
        {
            yield return null;
            Require(SceneManager.GetActiveScene().name == "Bootstrap",
                "Unexpected initial scene: " + SceneManager.GetActiveScene().path);
            SoundwellMemoryProbe.Capture("bootstrap_before_entry");
            var entrance = FindRootComponent<SoundwellEntrance>();
            Require(entrance != null, "The saved Bootstrap scene must expose the trial.");
            Require(ShowsPrompt(entrance, "[F2 / Pad A] Enter the trial"), "The entry screen must show its entry control.");

            // Route a pointer event through the actual UGUI Button + GraphicRaycaster.
            var entryButton = entrance.GetComponentInChildren<Button>();
            Require(entryButton != null && entryButton.interactable, "The entry must be a live UGUI button.");
            RequireInputSystemEventSystem("Bootstrap");
            var eventSystem = EventSystem.current;
            Require(eventSystem != null && eventSystem.currentSelectedGameObject == entryButton.gameObject,
                "The entry button must receive keyboard / controller selection.");
            var raycaster = entrance.GetComponentInChildren<GraphicRaycaster>();
            Require(raycaster != null && raycaster.enabled, "The entry must accept pointer raycasts.");

            // Validate compact title/menu layout as well as the standard 16:9 path.
            // The viewport ratio is injected; physical monitor aspect remains a
            // manual test, while these raycasts exercise real UGUI components.
            entrance.LayoutForViewportAspect(0.6f);
            Canvas.ForceUpdateCanvases();
            Require(Mathf.Approximately(entryButton.GetComponent<RectTransform>().sizeDelta.x, 310f),
                "Portrait entry must use a compact button size.");
            var compactText = entryButton.GetComponentInChildren<Text>();
            Require(compactText != null && compactText.rectTransform.sizeDelta.x <
                entryButton.GetComponent<RectTransform>().sizeDelta.x,
                "The entry label must stay inside its portrait button.");
            var compactPointer = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, entryButton.transform.position)
            };
            var compactHits = new List<RaycastResult>();
            raycaster.Raycast(compactPointer, compactHits);
            Require(compactHits.Exists(hit => hit.gameObject == entryButton.gameObject),
                "The portrait-layout entry must remain raycastable.");

            entrance.LayoutForViewportAspect(16f / 9f);
            Canvas.ForceUpdateCanvases();
            Require(Mathf.Approximately(entryButton.GetComponent<RectTransform>().sizeDelta.x, 460f),
                "The entry must restore its landscape button size.");
            Require(eventSystem.currentSelectedGameObject == entryButton.gameObject,
                "Switching entry layout must not discard keyboard/gamepad focus.");
            var pointer = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, entryButton.transform.position)
            };
            var hits = new List<RaycastResult>();
            raycaster.Raycast(pointer, hits);
            Require(hits.Exists(hit => hit.gameObject == entryButton.gameObject),
                "A pointer on the entry button must hit its UGUI graphic.");
            ExecuteEvents.Execute<IPointerClickHandler>(entryButton.gameObject, pointer,
                ExecuteEvents.pointerClickHandler);
            Require(!entryButton.interactable, "Entry must disable on click to block double activation.");
            var load = entrance.OpenTrial();
            Require(load != null && ReferenceEquals(load, entrance.OpenTrial()),
                "A second entry request during loading must reuse the same operation.");
            yield return load;
            Require(SceneManager.GetActiveScene().name == SceneName, "The Bootstrap entrance must load Soundwell.");
            var trial = FindRootComponent<SoundwellTrial>();
            var party = FindRootComponent<SoundwellOrkParty>();
            Require(trial != null, "The saved trial scene must include the trial component.");
            Require(party != null, "The saved trial scene must include its ORK party.");
            trial.ReadInput = false;
            for (var wait = Until(() => trial.IsReady && party.IsReady,
                "initialize Soundwell and register the ORK party"); wait.MoveNext();)
                yield return null;
            SoundwellInputAdapter.EnsureEventSystem(trial.transform);
            yield return null;
            SoundwellMemoryProbe.Capture("soundwell_entered");
            RequireInputSystemEventSystem("SoundwellTrial");
            var camera = trial.TrialCamera;
            camera.aspect = 1f;
            yield return null;
            yield return null;
            float left = camera.WorldToViewportPoint(new Vector3(-8.1f, 0f, 0f)).x;
            float right = camera.WorldToViewportPoint(new Vector3(8.1f, 0f, 0f)).x;
            Require(left > 0f && right < 1f, "A square viewport must retain both ends of the trial.");

            // A square/narrow viewport must reflow the actual screen-space UGUI
            // actions to two bottom rows, with active pointer hit targets.
            var compactRing = TrialActionButton(trial, "Ring");
            var compactWrap = TrialActionButton(trial, "Wrap");
            var compactRetry = TrialActionButton(trial, "Retry");
            var compactBack = TrialActionButton(trial, "Back");
            Require(((RectTransform)compactRing.transform).anchorMin == new Vector2(0.5f, 0f) &&
                ((RectTransform)compactBack.transform).anchorMin == new Vector2(0.5f, 0f),
                "Square viewports must use a bottom-anchored compact HUD.");
            Require(((RectTransform)compactRing.transform).anchoredPosition.y ==
                ((RectTransform)compactWrap.transform).anchoredPosition.y &&
                ((RectTransform)compactRetry.transform).anchoredPosition.y ==
                ((RectTransform)compactBack.transform).anchoredPosition.y,
                "Compact actions must form two reachable horizontal rows.");
            AssertHudHit(trial, "Ring");
            AssertHudHit(trial, "Wrap");
            AssertHudHit(trial, "Retry");
            AssertHudHit(trial, "Back");
            Capture(trial, "00-m8-square-pixel-perfect", 640, 640);

            camera.aspect = 0.6f;
            yield return null;
            yield return null;
            Require(((RectTransform)compactRing.transform).anchorMin == new Vector2(0.5f, 0f),
                "Portrait viewports must preserve the compact button layout.");
            AssertHudHit(trial, "Retry");
            Capture(trial, "00-m8-portrait-pixel-perfect", 480, 800);
            camera.ResetAspect();
            yield return null;
            yield return null;
            Require(((RectTransform)compactRing.transform).anchorMin == Vector2.one &&
                ((RectTransform)compactBack.transform).anchorMin == Vector2.one,
                "Landscape viewports must restore the original top-right HUD stack.");
            AssertHudHit(trial, "Back");
            Capture(trial, "00-m8-widescreen-pixel-perfect", 960, 540);
            Require(ShowsPrompt(trial, "Walk to the copper bell"), "The initial view must show where to go.");
            Require(ShowsPrompt(trial, "[Esc / Pad B] Back to entry"), "The exit control must be visible at the entrance.");
            Require(!TrialActionButton(trial, "Ring").interactable &&
                !TrialActionButton(trial, "Wrap").interactable,
                "Out-of-reach bell actions must not be clickable.");
            Require(TrialActionButton(trial, "Retry").interactable &&
                TrialActionButton(trial, "Back").interactable,
                "Reset and return must be available at the entrance.");

            var bellAudio = FindRootComponent<AudioSource>();
            Require(bellAudio != null, "The copper bell must expose its audio source.");
            var searchTrack = trial.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(item => item.gameObject.name ==
                    "PLACEHOLDER beast search timer track");
            var searchFill = trial.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(item => item.gameObject.name ==
                    "PLACEHOLDER beast search timer fill");
            Require(searchTrack != null && searchFill != null &&
                !searchTrack.enabled && !searchFill.enabled &&
                !trial.IsSearching && trial.SearchRemainingFraction == 0f,
                "An unused bell cannot expose a ghost search timer.");
            // The beast is a scene-level actor, not necessarily a child of
            // the trial controller. Search all actual scene roots just as
            // FindRootComponent does, while keeping the exact marker name.
            var beastContactRing = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LineRenderer>(true))
                .SingleOrDefault(line => line.gameObject.name == "PLACEHOLDER beast contact radius");
            Require(beastContactRing != null && !beastContactRing.enabled &&
                beastContactRing.loop && !beastContactRing.useWorldSpace &&
                beastContactRing.positionCount == 40,
                "Locked-gate players must see no phantom beast-contact hazard.");
            for (int i = 0; i < beastContactRing.positionCount; i++)
                Require(Mathf.Abs(beastContactRing.GetPosition(i).magnitude -
                    SoundwellTrial.BeastContactRadius) < 0.003f,
                    "Every hazard-ring vertex must match the real knockback radius.");
            Require(Vector2.Distance(beastContactRing.transform.position,
                trial.BeastPosition) < 0.005f,
                "The danger ring must belong to the moving beast, not a simulated position.");
            var falseTrailMarker = trial.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(item => item.gameObject.name == "PLACEHOLDER false trail marker");
            Require(falseTrailMarker != null && falseTrailMarker.enabled &&
                !trial.FalseMarkerCleared,
                "The wrong-way sign must visibly exist until the surveyor removes it.");
            var ropeEvidence = trial.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(item => item.gameObject.name == "PLACEHOLDER severed bell rope");
            var clawEvidence = trial.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(item => item.gameObject.name == "PLACEHOLDER claw marks");
            Require(ropeEvidence != null && clawEvidence != null &&
                Vector2.Distance(ropeEvidence.transform.position,
                    SoundwellTrial.RopeEvidencePosition) < 0.01f &&
                Vector2.Distance(clawEvidence.transform.position,
                    SoundwellTrial.ClawEvidencePosition) < 0.01f &&
                !trial.RopeObserved && !trial.ClawsObserved,
                "Broken cord and claw marks must be real inspectable world evidence.");
            Color ropeUnobservedColor = ropeEvidence.color;
            Color clawsUnobservedColor = clawEvidence.color;
            var revealedClawTrail = trial.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(item => item.gameObject.name.StartsWith("PLACEHOLDER revealed claw trail "))
                .OrderBy(item => item.gameObject.name).ToArray();
            Require(revealedClawTrail.Length == 3 && revealedClawTrail.All(marker => !marker.enabled),
                "Unexamined claw tracks must not expose a world-space route.");
            for (int i = 0; i < revealedClawTrail.Length; i++)
            {
                Vector2 expected = Vector2.Lerp(SoundwellTrial.ClawEvidencePosition,
                    SoundwellTrial.SideBellStand, (i + 1f) / (revealedClawTrail.Length + 1f));
                Require(Vector2.Distance((Vector2)revealedClawTrail[i].transform.position,
                    expected) < 0.015f,
                    "Revealed claw trail must physically connect the observed marks to the side stand.");
            }
            var cartEvidence = trial.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(item => item.gameObject.name == "PLACEHOLDER abandoned mule cart");
            Require(cartEvidence != null && !trial.CartObserved &&
                Vector2.Distance((Vector2)cartEvidence.transform.position,
                    SoundwellTrial.CartEvidencePosition) < 0.01f,
                "The abandoned cart must exist as an inspectable world clue, not just a caption.");
            Color cartUnobservedColor = cartEvidence.color;
            var cartMapKit = trial.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(item => item.gameObject.name == "PLACEHOLDER cart map kit");
            var safeTrailMarker = trial.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(item => item.gameObject.name ==
                    "PLACEHOLDER verified return route marker");
            Require(cartMapKit != null && cartMapKit.enabled && !trial.CartKitReclaimed &&
                safeTrailMarker != null && !safeTrailMarker.enabled && !trial.SafeRouteMarked,
                "Initial scene must hold the map kit but no corrected safe-route sign.");
            var escapeWarning = trial.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(item => item.gameObject.name == "PLACEHOLDER escape-lane risk");
            Require(escapeWarning != null && !escapeWarning.enabled &&
                !trial.BeastBlocksEscape && ShowsPrompt(trial, "ESCAPE LANE  /  CLEAR"),
                "Escape risk must start clear and have a real world-space warning marker.");
            var gateCollider = trial.GetComponentsInChildren<BoxCollider2D>(true)
                .SingleOrDefault(body => body.gameObject.name == "PLACEHOLDER soundwell gate");
            Require(gateCollider != null && gateCollider.enabled && !trial.GateOpen,
                "The inner gate must physically block passage before any bell experiment.");
            Require(!TrialActionButton(trial, "Gate").interactable,
                "The gate button must be unavailable without a real distraction.");
            Require(!trial.AtSurveyor && !trial.HasSurveyorClue &&
                !trial.PlankSecured && !trial.CanSecurePlank &&
                !trial.SurveyorEscorting && !trial.RescueComplete &&
                !trial.BeastContacted && trial.BeastContacts == 0,
                "No rescue or beast-contact penalty can be claimed from the entry zone.");
            var sideStand = trial.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(renderer => renderer.gameObject.name == "PLACEHOLDER side bell stand");
            Require(sideStand != null &&
                Vector2.Distance((Vector2)sideStand.transform.position, SoundwellTrial.SideBellStand) < 0.05f,
                "The side bell stand must visibly mark the authoritative second sound source.");
            var upperBoard = trial.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(renderer => renderer.gameObject.name ==
                    "PLACEHOLDER upper load-bearing plank");
            var lowerBoard = trial.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(renderer => renderer.gameObject.name ==
                    "PLACEHOLDER unsafe lower plank");
            var surveyor = trial.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(renderer => renderer.gameObject.name == "PLACEHOLDER trapped surveyor");
            Require(upperBoard != null && lowerBoard != null &&
                !trial.LowerPlankBroken && surveyor != null &&
                Vector2.Distance((Vector2)lowerBoard.transform.position,
                    SoundwellTrial.LowerBoardPosition) < 0.01f &&
                Vector2.Distance((Vector2)surveyor.transform.position,
                    SoundwellTrial.SurveyorPosition) < 0.01f,
                "Both physical plank options and the trapped surveyor must exist in the inner room.");
            Color initialBoard = upperBoard.color;
            Color initialLowerBoard = lowerBoard.color;
            var intentMarker = trial.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(renderer => renderer.gameObject.name == "PLACEHOLDER beast intent marker");
            Require(intentMarker != null && !intentMarker.enabled,
                "The beast intent marker must exist but remain hidden before any sound.");
            Vector2 home = trial.BeastPosition;
            Vector2 retreat = trial.PlayerPosition;
            Vector2 bellStart = trial.BellPosition;
            Require(!trial.IsCarrying && !TrialActionButton(trial, "Carry").interactable,
                "Bell pickup must require reaching the actual bell.");
            Press(trial, ring: true);
            Require(!trial.HasBellTarget, "Interaction must require proximity.");
            Press(trial, gate: true);
            Require(!trial.RopeObserved && !trial.ClawsObserved && !trial.CartObserved && !trial.GateOpen,
                "Inspecting from RETREAT cannot remotely claim the environment evidence.");

            // Explore two optional details without being told the bell solution.
            Press(trial, Vector2.up);
            for (var wait = Until(() => trial.PlayerPosition.y >= 1.45f,
                "reach the optional broken-cord clue lane"); wait.MoveNext();)
                yield return null;
            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= -4.60f,
                "approach the torn rope"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            Require(trial.CanInspectRope && !trial.CanInspectClaws &&
                TrialActionButton(trial, "Gate").interactable &&
                TrialActionButton(trial, "Gate").GetComponentInChildren<Text>().text
                    .Contains("INSPECT ROPE", StringComparison.Ordinal),
                "A nearby torn rope must enable a real contextual UGUI inspection.");
            ClickTrialAction(trial, "Gate");
            yield return null;
            Require(trial.RopeObserved && !trial.RopeRecovered && !trial.ClawsObserved &&
                ropeEvidence.color != ropeUnobservedColor &&
                ShowsPrompt(trial, "bell cord was cut") &&
                !trial.HasBellTarget && !trial.GateOpen,
                "Inspecting the severed cord must reveal evidence without solving the puzzle.");
            Require(trial.CanRecoverRope && TrialActionButton(trial, "Gate").interactable &&
                TrialActionButton(trial, "Gate").GetComponentInChildren<Text>().text
                    .Contains("RECOVER CORD", StringComparison.Ordinal),
                "Observing the cord must expose a separate, real recovery action.");
            ClickTrialAction(trial, "Gate");
            yield return null;
            Require(trial.RopeRecovered && trial.RopeObserved && !ropeEvidence.enabled &&
                !trial.ClawsObserved && !trial.HasBellTarget,
                "Recovered rope must visibly leave the entrance, with no invented bell effect.");
            Press(trial, gate: true);
            Require(trial.RopeRecovered && !trial.CanRecoverRope,
                "Repeated recovery must not duplicate the usable rope.");

            // The cart is a separate optional clue. It requires real
            // proximity and never opens the gate or reveals the lure trick.
            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= -3.40f,
                "walk toward the abandoned cart"); wait.MoveNext();)
                yield return null;
            Press(trial, Vector2.up);
            for (var wait = Until(() => trial.PlayerPosition.y >= 2.26f,
                "approach the abandoned cart"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            Require(trial.CanInspectCart && !trial.CanInspectRope && !trial.CanInspectClaws &&
                TrialActionButton(trial, "Gate").interactable &&
                TrialActionButton(trial, "Gate").GetComponentInChildren<Text>().text
                    .Contains("INSPECT CART", StringComparison.Ordinal) &&
                ShowsPrompt(trial, "[G / RB] Inspect the abandoned mule cart"),
                "Only physical proximity must enable inspection on the real UGUI button.");
            ClickTrialAction(trial, "Gate");
            yield return null;
            Require(trial.CartObserved && cartEvidence.color != cartUnobservedColor &&
                ShowsPrompt(trial, "tracks lead inside") &&
                !trial.GateOpen && !trial.HasBellTarget && !trial.SurveyorEscorting,
                "Cart inspection must reveal a rescue clue, not solve the bell or gate.");
            Press(trial, gate: true);
            Require(trial.CartObserved && !trial.CanInspectCart,
                "Repeated inspection must not grant an extra effect.");
            Capture(trial, "00-abandoned-cart-observed");
            Press(trial, Vector2.down);
            for (var wait = Until(() => trial.PlayerPosition.y <= 1.48f,
                "return from the cart to the evidence lane"); wait.MoveNext();)
                yield return null;
            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= -1.90f,
                "follow the claw-mark trail"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            Require(trial.CanInspectClaws && !trial.CanInspectRope &&
                TrialActionButton(trial, "Gate").interactable &&
                TrialActionButton(trial, "Gate").GetComponentInChildren<Text>().text
                    .Contains("INSPECT CLAWS", StringComparison.Ordinal),
                "The beast marks must only be inspectable up close.");
            ClickTrialAction(trial, "Gate");
            yield return null;
            Require(trial.RopeObserved && trial.ClawsObserved && trial.CartObserved &&
                clawEvidence.color != clawsUnobservedColor &&
                revealedClawTrail.All(marker => marker.enabled) &&
                ShowsPrompt(trial, "Claw marks turn toward the bell stand") &&
                ShowsPrompt(trial, "revealed claw trail runs toward the SIDE BELL STAND") &&
                !trial.HasBellTarget && !trial.GateOpen,
                "Claw inspection must reveal its real-world trail without sounding a bell or opening the gate.");
            Press(trial, gate: true);
            Require(trial.ClawsObserved && revealedClawTrail.All(marker => marker.enabled) &&
                !trial.HasBellTarget,
                "Repeating the completed inspection must not modify the bell or duplicate the trail.");
            Capture(trial, "00-optional-entrance-evidence");

            Press(trial, Vector2.left);
            for (var wait = Until(() => trial.PlayerPosition.x <= retreat.x + 0.06f,
                "return from the evidence lane toward RETREAT"); wait.MoveNext();)
                yield return null;
            Press(trial, Vector2.down);
            for (var wait = Until(() => trial.PlayerPosition.y <= retreat.y + 0.06f,
                "return from the clues to the marked safe point"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            Require(Vector2.Distance(trial.PlayerPosition, retreat) < 0.35f &&
                trial.RopeObserved && trial.ClawsObserved && trial.CartObserved,
                "Optional observations must persist as the player returns to safety.");

            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= -2.25f, "walk into bell range"); wait.MoveNext();)
                yield return null;
            yield return null;
            yield return null;
            Require(ShowsPrompt(trial, "[E / Pad A] Ring the bell"), "Bell actions must appear within reach.");
            Require(ShowsPrompt(trial, "[WASD / Arrows / Stick] Move"), "The movement control must stay visible at the bell.");
            Require(TrialActionButton(trial, "Ring").interactable &&
                TrialActionButton(trial, "Wrap").interactable &&
                TrialActionButton(trial, "Carry").interactable &&
                !TrialActionButton(trial, "Retry").interactable,
                "The HUD must enable bell actions and pickup but reject reset away from safety.");

            // Activating keyboard/pad navigation must stop motion and suppress the
            // simultaneous gameplay input from the shared stick and A/B/X buttons.
            trial.SetActionMenuOpen(true);
            Require(trial.ActionMenuOpen && EventSystem.current.sendNavigationEvents &&
                SoundwellInputAdapter.IsUiContextActive,
                "Tab/Start action mode must transfer focus to UGUI and disable gameplay actions.");
            Require(EventSystem.current.currentSelectedGameObject == TrialActionButton(trial, "Ring").gameObject,
                "At the bell, focused UGUI navigation must initially select Ring.");
            Vector2 pausedPosition = trial.PlayerPosition;
            trial.ApplyControls(SoundwellControlFrame.FromButtons(Vector2.right,
                false, false, false, false, true, false, true, false));
            yield return new WaitForFixedUpdate();
            Require(Vector2.Distance(trial.PlayerPosition, pausedPosition) < 0.05f && !trial.HasBellTarget,
                "The same stick and A/X inputs must neither move nor ring while the HUD has focus.");
            // The evidence image must show actual UGUI focus, not only the world
            // camera's SpriteRenderers and world-space text.
            Capture(trial, "00-focused-actions");
            var focusedAction = EventSystem.current.currentSelectedGameObject;
            Require(focusedAction != null, "The focused action menu must have a selected control before Cancel.");
            ExecuteEvents.ExecuteHierarchy<ICancelHandler>(focusedAction,
                new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
            Require(!trial.ActionMenuOpen && !EventSystem.current.sendNavigationEvents &&
                EventSystem.current.currentSelectedGameObject == null &&
                SoundwellInputAdapter.IsGameplayContextActive,
                "Cancel must close UGUI focus, restore gameplay actions, and release the selection.");
            ClickTrialAction(trial, "Ring");
            Require(trial.HasBellTarget, "The UGUI Ring button must trigger the real Soundwell signal.");
            yield return null;
            Require(intentMarker.enabled && intentMarker.color.r > intentMarker.color.b,
                "The visible gold intent marker must represent the beast tracking clear sound.");
            Require(Vector2.Distance((Vector2)intentMarker.transform.position,
                new Vector2(1.15f, 0f)) < 0.05f,
                "The marker must use the same hearing target as real beast movement.");
            Require(bellAudio.loop && bellAudio.clip != null && bellAudio.clip.name == "Copper bell",
                "The clear sound must repeat while the beast tracks it.");
            for (var wait = Until(() => trial.BeastPosition.x < home.x - 0.7f,
                "the beast turns and advances toward the bell"); wait.MoveNext();)
                yield return null;
            Require(trial.BeastPosition.y < home.y, "The beast must approach the bell's lane.");
            for (var wait = Until(() => trial.BeastBlocksEscape,
                "clear bell draws the beast across the real escape lane"); wait.MoveNext();)
                yield return null;
            yield return null;
            Require(escapeWarning.enabled && ShowsPrompt(trial, "ESCAPE LANE  /  BLOCKED") &&
                Vector2.Distance(trial.BeastPosition, SoundwellTrial.EscapeLanePosition) <
                    SoundwellTrial.EscapeLaneDangerRadius,
                "The danger cue must be driven by the beast physically crossing the same lane.");
            Vector2 pursued = trial.BeastPosition;
            var clear = Capture(trial, "01-ringing");

            ClickTrialAction(trial, "Wrap");
            Require(trial.IsWrapped, "The UGUI Wrap button must update the real Soundwell state.");
            Require(!trial.HasBellTarget, "trial.HasBellTarget must be false.");
            Require(trial.IsSearching, "trial.IsSearching must be true.");
            Require(!bellAudio.loop && bellAudio.clip != null && bellAudio.clip.name == "Wrapped bell",
                "Wrapping must stop the clear loop and switch to a muffled sound.");
            Press(trial, ring: true);
            Require(!trial.HasBellTarget, "A wrapped bell must not attract the beast.");
            yield return null;
            yield return null;
            Require(ShowsPrompt(trial, "[Q / Pad X] Remove the cloth"), "Wrapped bells must show the available actions.");
            Require(intentMarker.enabled && intentMarker.color.b > intentMarker.color.r,
                "Stopping the clear sound must change the beast intent marker to blue searching.");
            Require(Vector2.Distance((Vector2)intentMarker.transform.position,
                new Vector2(1.15f, 0f)) < 1.45f,
                "Searching marker must follow the actual last-heard destination, not the player.");
            var muffled = Capture(trial, "02-wrapped-searching");
            Require(!muffled.SequenceEqual(clear), "The two signal phases must render differently.");
            for (var wait = Until(() => Vector2.Distance(trial.BeastPosition, pursued) > 0.35f,
                "the beast continues searching after losing the bell"); wait.MoveNext();)
                yield return null;

            // Removing the cloth cancels muffled playback and clears the pursuit
            // marker; wrapping again must not generate a clear sound.
            Press(trial, wrap: true);
            Require(!trial.IsWrapped && !trial.IsSearching && !trial.HasBellTarget,
                "Unwrapping must return to the quiet ready signal.");
            Require(!bellAudio.isPlaying && bellAudio.clip == null,
                "Unwrapping must stop lingering muffled audio, not merely hide the signal.");
            yield return null;
            Require(!intentMarker.enabled, "No audible signal must hide the beast's intent marker.");
            Press(trial, wrap: true);
            Require(trial.IsWrapped && !trial.IsSearching && !bellAudio.isPlaying,
                "Wrapping before a ring must not accidentally attract the beast.");

            Press(trial, retry: true);
            Require(trial.IsWrapped, "Retry requires a retreat to safety.");
            Press(trial, Vector2.right);
            float collisionCheck = Time.time + 0.85f;
            for (var wait = Until(() => Time.time >= collisionCheck, "hold movement against the grating"); wait.MoveNext();)
                yield return null;
            Require(trial.PlayerPosition.x < -0.10f, "The safety grating must block the player.");

            Press(trial, Vector2.left);
            for (var wait = Until(() => Vector2.Distance(trial.PlayerPosition, retreat) < 0.25f,
                "return to the safety mark"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            yield return null;
            Require(ShowsPrompt(trial, "[R / Pad Y] Reset the experiment"), "Retreat must reveal the reset control.");
            Require(TrialActionButton(trial, "Retry").interactable,
                "Returning to safety must enable the UGUI Reset button.");
            ClickTrialAction(trial, "Retry");
            Require(!trial.IsWrapped, "The UGUI Reset button must restore the real signal state.");
            Require(!trial.IsSearching, "trial.IsSearching must be false.");
            Require(trial.BeastPosition == home, "Reset must return the beast to its initial position.");
            Require(!bellAudio.loop && bellAudio.clip == null, "Reset must clear the bell's playback state.");
            Require(!trial.IsCarrying && trial.BellPosition == bellStart,
                "Reset must put the portable bell back at its original stand.");
            Require(!trial.RopeObserved && !trial.ClawsObserved && !trial.CartObserved &&
                !trial.RopeRecovered && !trial.SurveyorLineTied &&
                !trial.BeastContacted && trial.BeastContacts == 0 &&
                !beastContactRing.enabled && !searchTrack.enabled && !searchFill.enabled &&
                trial.SearchRemainingFraction == 0f &&
                revealedClawTrail.All(marker => !marker.enabled),
                "Retreat reset must clear clues, revealed claw trail, and recovered guide rope.");
            yield return null;
            Require(ropeEvidence.enabled && cartEvidence.color == cartUnobservedColor &&
                cartMapKit.enabled && !trial.CartKitReclaimed &&
                !safeTrailMarker.enabled && !trial.SafeRouteMarked,
                "Reset must restore cord, cart clue, map kit and absent safe route.");
            Require(!intentMarker.enabled, "Retreat/reset must clear the monster intent indicator.");
            Capture(trial, "03-retreat-reset");

            // This rescue begins after a legitimate reset: the cord recovered
            // in the first experiment no longer exists in the player's supplies.
            // Collect it again from the real world before entering the room.
            Press(trial, Vector2.up);
            for (var wait = Until(() => trial.PlayerPosition.y >= 1.45f,
                "return to the recovered rope's original location"); wait.MoveNext();)
                yield return null;
            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= -4.60f,
                "reach the restored broken bell cord"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            Require(trial.CanInspectRope && !trial.RopeRecovered,
                "A fresh trial must require inspecting the restored rope again.");
            ClickTrialAction(trial, "Gate");
            yield return null;
            Require(trial.CanRecoverRope && !trial.RopeRecovered,
                "Inspection must make the physical recovered cord available.");
            ClickTrialAction(trial, "Gate");
            yield return null;
            Require(trial.RopeRecovered && !ropeEvidence.enabled && !trial.SurveyorLineTied,
                "The second experiment must acquire its own real escort material.");

            // Retreat resets all optional observations. The earlier cart visit
            // cannot silently unlock a post-rescue scene in this new trial.
            // Inspect the cart again, physically, before asking the survivor
            // to return for his own map kit.
            Require(!trial.CartObserved && !trial.CartKitReclaimed && cartMapKit.enabled,
                "Reset must require discovering the cart again on the rescue attempt.");
            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= -3.40f,
                "return to the cart after the reset"); wait.MoveNext();)
                yield return null;
            Press(trial, Vector2.up);
            for (var wait = Until(() => trial.PlayerPosition.y >= 2.26f,
                "reach the cart on the rescue attempt"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            Require(trial.CanInspectCart && TrialActionButton(trial, "Gate").interactable,
                "Cart inspection must require the second attempt's real proximity.");
            ClickTrialAction(trial, "Gate");
            yield return null;
            Require(trial.CartObserved && !trial.CartKitReclaimed && cartMapKit.enabled &&
                ShowsPrompt(trial, "tracks lead inside"),
                "The rescue attempt must own the cart observation, without claiming its kit.");
            Capture(trial, "03-cart-rediscovered-after-reset");

            Press(trial, Vector2.left);
            for (var wait = Until(() => trial.PlayerPosition.x <= retreat.x + 0.06f,
                "return from rope recovery toward the entrance"); wait.MoveNext();)
                yield return null;
            Press(trial, Vector2.down);
            for (var wait = Until(() => trial.PlayerPosition.y <= retreat.y + 0.06f,
                "return to the entrance after recovering cord"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            Require(Vector2.Distance(trial.PlayerPosition, retreat) < 0.35f &&
                trial.RopeRecovered, "The recovered cord must follow the player into the rescue run.");

            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= -2.25f, "reach the bell on retry"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            ClickTrialAction(trial, "Carry");
            Require(trial.IsCarrying && !trial.HasBellTarget && !bellAudio.isPlaying,
                "UGUI Lift must carry the real bell and silence any decoy.");
            Require(trial.CanPlaceBell, "The entrance stand must accept the bell near its saved position.");
            yield return null;
            Require(!TrialActionButton(trial, "Ring").interactable &&
                !TrialActionButton(trial, "Wrap").interactable &&
                TrialActionButton(trial, "Carry").interactable,
                "Carrying must disable ringing and wrapping until the bell is put down.");
            Press(trial, ring: true);
            Require(!trial.HasBellTarget, "A carried bell may not create an infinite moving lure.");

            Press(trial, Vector2.down);
            for (var wait = Until(() => trial.PlayerPosition.y <= -0.96f,
                "carry the bell between the two marked stands"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            Require(trial.IsCarrying && !trial.CanPlaceBell &&
                !TrialActionButton(trial, "Carry").interactable,
                "The unmarked gap must forbid placing an arbitrary third sound source.");
            Press(trial, carry: true);
            Require(trial.IsCarrying,
                "The input boundary must reject an invalid drop even if called without the UGUI.");

            Press(trial, Vector2.down);
            for (var wait = Until(() => trial.PlayerPosition.y <= -1.8f,
                "carry the bell into the side lane"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            Require(trial.IsCarrying && trial.CanPlaceBell &&
                trial.BellPosition.y < bellStart.y - 1.5f,
                "The carried bell must reach the second stand, enabling a legal drop.");
            yield return null;
            Require(TrialActionButton(trial, "Carry").interactable,
                "The real UGUI drop action must re-enable at the named side stand.");
            ClickTrialAction(trial, "Carry");
            Require(!trial.IsCarrying &&
                Vector2.Distance(trial.BellPosition, SoundwellTrial.SideBellStand) < 0.001f,
                "A legal drop must snap to the side stand, not invent a third position.");
            yield return null;
            Press(trial, ring: true);
            Require(trial.HasBellTarget, "The relocated bell must still ring using the real signal state.");
            yield return null;
            Require(intentMarker.enabled &&
                Vector2.Distance((Vector2)intentMarker.transform.position,
                    new Vector2(1.15f, trial.BellPosition.y)) < 0.06f,
                "Relocating the bell must redirect the real beast intent toward the new lane.");
            Capture(trial, "04-relocated-bell");

            // The saved gate requires the actual side-stand signal, nearby
            // player, and beast physically outside the escape lane.
            Press(trial, gate: true);
            Require(!trial.GateOpen && !TrialActionButton(trial, "Gate").interactable,
                "A remote G/RB action cannot open the gate, even during tracking.");
            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= -0.6f,
                "approach the west-side gate panel"); wait.MoveNext();)
                yield return null;
            Press(trial, Vector2.up);
            for (var wait = Until(() => trial.PlayerPosition.y >= -0.15f,
                "stand at the gate control"); wait.MoveNext();)
                yield return null;
            Press(trial);
            for (var wait = Until(() => trial.CanOpenGate,
                "side bell draws beast far enough from the gate"); wait.MoveNext();)
                yield return null;
            yield return null;
            Require(!trial.GateOpen && TrialActionButton(trial, "Gate").interactable &&
                ShowsPrompt(trial, "[G / RB] Open the gate"),
                "The UGUI gate action requires a readable, real sound window.");
            ClickTrialAction(trial, "Gate");
            Require(trial.GateOpen, "The gate must open in the authoritative Makinom state.");
            for (var wait = Until(() => !trial.BeastBlocksEscape,
                "side bell draws the beast clear of the escape lane"); wait.MoveNext();)
                yield return null;
            yield return null;
            Require(!escapeWarning.enabled && ShowsPrompt(trial, "ESCAPE LANE  /  CLEAR") &&
                Vector2.Distance(trial.BeastPosition, SoundwellTrial.EscapeLanePosition) >=
                    SoundwellTrial.EscapeLaneDangerRadius,
                "The escape lane can only become clear when the real beast moves away.");
            Require(!gateCollider.enabled && ShowsPrompt(trial, "GATE  /  OPEN"),
                "The actual collider and world label must agree that the gate is open.");
            Require(beastContactRing.enabled &&
                Vector2.Distance(beastContactRing.transform.position, trial.BeastPosition) < 0.02f,
                "Opening the actual gate must reveal the beast's authoritative live contact radius.");
            Capture(trial, "06-inner-gate-open");
            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= 0.9f,
                "walk through the now-open inner gate"); wait.MoveNext();)
                yield return null;
            Require(trial.PlayerPosition.x > 0.25f,
                "Player must cross the previously blocked doorway without teleporting.");
            Require(!trial.HasSurveyorClue,
                "Crossing the threshold alone must not auto-reveal the surveyor's clue.");

            // Walk the actual player into the live beast while the side bell
            // is audible. Contact has a visible positional consequence but
            // does not revoke the open gate or complete the rescue.
            Require(!trial.BeastContacted && trial.HasBellTarget &&
                trial.BeastPosition.y < -0.95f,
                "Only a genuinely active low-lane beast can test contact.");
            Press(trial, Vector2.down);
            for (var wait = Until(() => trial.BeastContacted,
                "player physically meets the live beast in the side bell lane"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            Require(Vector2.Distance(trial.PlayerPosition, retreat) < 0.12f &&
                trial.GateOpen && trial.HasBellTarget && !trial.RescueComplete &&
                !trial.SurveyorEscorting && !trial.CartClueShared &&
                trial.BeastContacts == 1 && beastContactRing.enabled &&
                Vector2.Distance(beastContactRing.transform.position, trial.BeastPosition) < 0.02f &&
                ShowsPrompt(trial, "BEAST CONTACT: Pushed back to RETREAT"),
                "Beast contact must push the player to safety without hiding a reset or inventing rescue.");
            Capture(trial, "06-beast-contact-retreat");
            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= 0.9f,
                "reenter the already open gate after beast contact"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            Require(trial.BeastContacted && trial.GateOpen &&
                !trial.RescueComplete && !trial.HasSurveyorClue,
                "Beast encounter must allow the player to continue the original rescue.");

            // M5: solid walls are saved as one TilemapCollider2D, not the old
            // runtime BoxCollider2D GameObjects. Verify the persisted wall
            // cells and non-trigger physics, then try walking into them below.
            var wallCollider = UnityEngine.Object.FindObjectsByType<TilemapCollider2D>(
                    FindObjectsSortMode.None)
                .SingleOrDefault(body => body.gameObject.name == "Solid walls");
            Require(wallCollider != null && wallCollider.enabled && !wallCollider.isTrigger,
                "The saved eastern enclosure must have one active solid TilemapCollider2D.");
            var wallTiles = wallCollider.GetComponent<Tilemap>();
            Require(wallTiles != null &&
                wallTiles.HasTile(new Vector3Int(35, 0, 0)) &&
                wallTiles.HasTile(new Vector3Int(18, 16, 0)) &&
                wallTiles.HasTile(new Vector3Int(18, -16, 0)),
                "The saved east, north inner and south inner walls must all have solid tiles.");
            Require(!trial.GetComponentsInChildren<BoxCollider2D>(true).Any(body =>
                    body.gameObject.name == "East wall" ||
                    body.gameObject.name == "North inner wall" ||
                    body.gameObject.name == "South inner wall"),
                "M5 must not keep parallel old wall colliders.");
            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= 6.10f,
                "explore the inner room toward its far wall"); wait.MoveNext();)
                yield return null;
            for (int frame = 0; frame < 42; frame++)
                yield return null;
            Press(trial);
            Require(trial.PlayerPosition.x < 6.75f,
                "The east wall must physically stop walking outside the inner room.");
            Press(trial, Vector2.up);
            for (var wait = Until(() => trial.PlayerPosition.y >= 2.35f,
                "test the north side of the inner room"); wait.MoveNext();)
                yield return null;
            for (int frame = 0; frame < 42; frame++)
                yield return null;
            Press(trial);
            Require(trial.PlayerPosition.y < 3.0f && trial.PlayerPosition.y > 2.3f,
                "The north inner wall must physically block leaving through the top.");
            Capture(trial, "06-inner-room-physics-bounds");
            Press(trial, Vector2.down);
            for (var wait = Until(() => trial.PlayerPosition.y <= 0.0f,
                "return from the north boundary to the inner route"); wait.MoveNext();)
                yield return null;
            Press(trial, Vector2.left);
            for (var wait = Until(() => trial.PlayerPosition.x <= 0.95f,
                "return from the east boundary to the surveyor route"); wait.MoveNext();)
                yield return null;
            Press(trial);
            Require(!trial.HasSurveyorClue && trial.GateOpen,
                "Exploring closed room boundaries must not reveal or complete the rescue.");

            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= 2.75f,
                "reach the trapped surveyor in the inner room"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            // Testing the wrong plank causes a visible collapse. The player
            // recovers near the surveyor instead of being silently rewarded.
            Press(trial, Vector2.down);
            for (var wait = Until(() => trial.LowerPlankBroken,
                "unsafe lower plank gives way under the moving player"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            Require(trial.PlayerPosition.y > -0.75f && trial.GateOpen &&
                !trial.HasSurveyorClue && lowerBoard.color != initialLowerBoard &&
                ShowsPrompt(trial, "lower plank snapped"),
                "Stepping on the weak lower plank must collapse it and recover the player.");
            Capture(trial, "07-lower-plank-collapses");
            // The weakened route must remain unsafe on another attempt.
            Press(trial, Vector2.down);
            for (var wait = Until(() => trial.PlayerPosition.y < -0.9f,
                "approach the collapsed lower plank"); wait.MoveNext();)
                yield return null;
            for (int frame = 0; frame < 22; frame++)
                yield return null;
            Press(trial);
            Require(trial.LowerPlankBroken && trial.PlayerPosition.y > -1.25f &&
                lowerBoard.color != initialLowerBoard,
                "A collapsed lower plank cannot be crossed by repeating the same movement.");
            // Regain talking range without a teleport, then choose the safe route.
            Press(trial, Vector2.up);
            for (var wait = Until(() => trial.PlayerPosition.y >= -0.10f,
                "return to the surveyor after a failed lower route"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            Require(trial.AtSurveyor && TrialActionButton(trial, "Gate").interactable &&
                ShowsPrompt(trial, "[G / RB] Ask the surveyor"),
                "The shared G/RB UGUI button must become a real Talk action near the survivor.");
            Require(TrialActionButton(trial, "Gate").GetComponentInChildren<Text>().text
                .Contains("ASK SURVEYOR", StringComparison.Ordinal),
                "The actual action caption must change from opening the gate to talking.");
            ClickTrialAction(trial, "Gate");
            Require(trial.HasSurveyorClue && trial.GateOpen,
                "Dialogue must record the surveyor's route knowledge without closing the gate.");
            yield return null;
            Require(upperBoard.color != initialBoard &&
                upperBoard.color.g > upperBoard.color.r &&
                ShowsPrompt(trial, "That upper board holds two") &&
                ShowsPrompt(trial, "The surveyor is still trapped"),
                "A real spoken warning must mark the safe upper plank without falsely completing rescue.");
            Press(trial, gate: true);
            Require(trial.HasSurveyorClue && !trial.PlankSecured,
                "Repeated talk must not brace the plank from the surveyor's position.");
            Capture(trial, "07-surveyor-plank-warning");

            // The marked plank is interactive rather than an automatic clue
            // reward. Reaching and bracing it lets the surveyor move of his
            // own accord, but does not silently finish the rescue.
            Press(trial, Vector2.up);
            for (var wait = Until(() => trial.PlayerPosition.y >= 1.65f,
                "reach the marked upper board"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            Require(trial.CanSecurePlank && TrialActionButton(trial, "Gate").interactable &&
                TrialActionButton(trial, "Gate").GetComponentInChildren<Text>().text
                    .Contains("BRACE PLANK", StringComparison.Ordinal) &&
                ShowsPrompt(trial, "[G / RB] Brace the upper plank"),
                "The marked plank must expose an actual contextual bracing action.");
            ClickTrialAction(trial, "Gate");
            Require(trial.PlankSecured && !trial.CanSecurePlank,
                "Bracing must update its single authoritative Makinom status.");
            for (var wait = Until(() => Vector2.Distance(
                    (Vector2)surveyor.transform.position, SoundwellTrial.UpperBoardPosition) < 0.1f,
                "surveyor independently takes the braced upper route"); wait.MoveNext();)
                yield return null;
            yield return null;
            Require(upperBoard.color.g > upperBoard.color.r &&
                upperBoard.color.g > initialBoard.g &&
                ShowsPrompt(trial, "I can cross now") &&
                ShowsPrompt(trial, "Rescue not complete"),
                "The board and surveyor must visibly respond while rescue remains unfinished.");
            // A second G/RB interaction now legitimately begins the next
            // contextual action (escort). First verify that bracing alone
            // did not auto-start it; the following UGUI click must do that.
            Require(trial.PlankSecured && trial.GateOpen &&
                !trial.SurveyorEscorting && !trial.RescueComplete,
                "Bracing must prepare the route without automatically starting escort.");
            Capture(trial, "08-braced-plank-surveyor");
            Require(trial.CanTieEscortLine && trial.CanEscortSurveyor &&
                !trial.SurveyorLineTied && !trial.SurveyorEscorting &&
                TrialActionButton(trial, "Gate").interactable &&
                TrialActionButton(trial, "Gate").GetComponentInChildren<Text>().text
                    .Contains("TIE GUIDE LINE", StringComparison.Ordinal),
                "Recovered cord must be usable at the braced plank, never an automatic bonus.");
            ClickTrialAction(trial, "Gate");
            yield return null;
            Require(trial.SurveyorLineTied && !trial.SurveyorEscorting &&
                !trial.CanTieEscortLine && trial.CanEscortSurveyor &&
                ShowsPrompt(trial, "LEAD SURVEYOR"),
                "Fastening the guide line cannot silently start or complete rescue.");
            var guideLine = trial.GetComponentsInChildren<LineRenderer>(true)
                .SingleOrDefault(line => line.gameObject.name == "PLACEHOLDER surveyor guide rope");
            Require(guideLine != null && !guideLine.enabled,
                "The guide line should appear only once the actual escort starts.");
            Capture(trial, "08-guide-line-fastened");
            ClickTrialAction(trial, "Gate");
            Require(trial.SurveyorEscorting && !trial.RescueComplete &&
                !trial.CanEscortSurveyor,
                "The Makinom escort flag must start without claiming an early rescue.");
            Press(trial, gate: true);
            Require(trial.SurveyorEscorting && !trial.RescueComplete,
                "Repeated context inputs must not duplicate or complete the escort.");
            Require(falseTrailMarker.enabled && !trial.FalseMarkerCleared,
                "Starting escort alone must not remove the incorrect sign.");
            yield return null;
            Require(trial.SurveyorLineTied && guideLine.enabled &&
                guideLine.positionCount == 2 &&
                Vector2.Distance(guideLine.GetPosition(0), trial.PlayerPosition) < 0.06f &&
                Vector2.Distance(guideLine.GetPosition(1), trial.SurveyorWorldPosition) < 0.06f,
                "The recovered cord must physically connect player and surveyor in the world.");
            // The survivor will not magically leave while the player stays
            // behind. Waiting is visible and costs no new hidden timer.
            for (int frame = 0; frame < 18; frame++)
                yield return null;
            Require(Vector2.Distance((Vector2)surveyor.transform.position,
                    SoundwellTrial.UpperBoardPosition) < 0.05f &&
                ShowsPrompt(trial, "Show me the way through the gate") &&
                !trial.RescueComplete,
                "The survivor must wait on the safe plank until the player leads through the gate.");
            Capture(trial, "09-surveyor-waits-for-leader");

            Press(trial, Vector2.down);
            for (var wait = Until(() => trial.PlayerPosition.y <= 0.05f,
                "leave the secured upper plank"); wait.MoveNext();)
                yield return null;
            Press(trial, Vector2.left);
            for (var wait = Until(() => trial.PlayerPosition.x <= -0.6f,
                "return through the open gate"); wait.MoveNext();)
                yield return null;
            Press(trial);
            for (var wait = Until(() => Vector2.Distance(
                    (Vector2)surveyor.transform.position,
                    SoundwellTrial.UpperBoardPosition) > 0.25f,
                "surveyor leaves the plank only after the player opens the way"); wait.MoveNext();)
                yield return null;
            Require(trial.SurveyorEscorting && !trial.RescueComplete &&
                !trial.BeastBlocksEscape && ShowsPrompt(trial, "Keep the gate open"),
                "A safe bell distraction and player leading must permit the real survivor to cross.");
            Capture(trial, "09-surveyor-follows-through-gate");

            // Sprint well ahead to test that the NPC is not an autopilot:
            // he must reach the gate, stop, and ask for his guide to return.
            Press(trial, Vector2.left);
            for (var wait = Until(() => trial.PlayerPosition.x <= retreat.x + 0.12f,
                "player runs ahead and leaves the surveyor behind"); wait.MoveNext();)
                yield return null;
            Press(trial);
            for (var wait = Until(() => trial.SurveyorWaitingForLeader,
                "surveyor stops after losing sight of the guide"); wait.MoveNext();)
                yield return null;
            Vector2 strandedPosition = trial.SurveyorWorldPosition;
            for (int frame = 0; frame < 20; frame++)
                yield return null;
            Require(Vector2.Distance(strandedPosition, trial.SurveyorWorldPosition) < 0.03f &&
                !trial.RescueComplete && ShowsPrompt(trial, "ESCORT PAUSED") &&
                ShowsPrompt(trial, "Too far ahead") &&
                Vector2.Distance(trial.SurveyorWorldPosition, retreat) > 1.1f,
                "Leaving the surveyor behind must halt him and cannot complete rescue.");
            Capture(trial, "09-escort-paused");

            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= -2.45f,
                "player returns to collect the surveyor"); wait.MoveNext();)
                yield return null;
            Press(trial);
            for (var wait = Until(() => !trial.SurveyorWaitingForLeader,
                "surveyor resumes following once the guide returns"); wait.MoveNext();)
                yield return null;
            Vector2 resumedPosition = trial.SurveyorWorldPosition;
            for (var wait = Until(() => Vector2.Distance(
                    resumedPosition, trial.SurveyorWorldPosition) > 0.18f,
                "surveyor makes physical progress after the player returns"); wait.MoveNext();)
                yield return null;
            Require(trial.SurveyorEscorting && !trial.RescueComplete &&
                !ShowsPrompt(trial, "ESCORT PAUSED"),
                "Returning for the surveyor must resume escort without awarding victory.");
            Capture(trial, "09-escort-resumed");
            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= -0.67f,
                "guide returns to the open gate approach"); wait.MoveNext();)
                yield return null;
            Press(trial);
            Press(trial, Vector2.down);
            for (var wait = Until(() => trial.PlayerPosition.y <= -1.8f,
                "return toward the side bell"); wait.MoveNext();)
                yield return null;
            Press(trial, Vector2.left);
            for (var wait = Until(() => trial.PlayerPosition.x <= -1.85f,
                "return to the relocated bell"); wait.MoveNext();)
                yield return null;
            Press(trial);
            yield return null;
            Require(trial.GateOpen && !gateCollider.enabled,
                "The gate must stay physically open for the rescue approach.");

            // Stopping a relocated bell starts a bounded search. Once the beast
            // loses the trail, the same Makinom signal keeps the cloth wrapped,
            // the intent marker vanishes, and the beast walks home visibly.
            Press(trial, wrap: true);
            Require(trial.IsSearching && trial.IsWrapped,
                "Wrapping the side-stand bell must begin its search phase.");
            yield return null;
            Require(searchTrack.enabled && searchFill.enabled &&
                trial.SearchRemainingFraction > 0.85f &&
                Mathf.Abs(searchFill.transform.localScale.x -
                    1.12f * trial.SearchRemainingFraction) < 0.02f &&
                Vector2.Distance(searchTrack.transform.position,
                    (Vector2)beastContactRing.transform.position +
                    new Vector2(0f, 0.92f)) < 0.02f,
                "The real search must display its current time over the moving beast.");
            Capture(trial, "05-search-timer-start");
            float fullSearchWidth = searchFill.transform.localScale.x;
            var searchDeadline = Time.time + SoundwellTrial.SearchDurationSeconds * 0.5f;
            for (var wait = Until(() => Time.time >= searchDeadline,
                "search persists for a meaningful half-window"); wait.MoveNext();)
                yield return null;
            Require(trial.IsSearching && searchTrack.enabled && searchFill.enabled &&
                trial.SearchRemainingFraction > 0f &&
                searchFill.transform.localScale.x < fullSearchWidth * 0.7f &&
                searchFill.transform.localScale.x > fullSearchWidth * 0.2f &&
                Mathf.Abs(searchFill.transform.localScale.x -
                    1.12f * trial.SearchRemainingFraction) < 0.03f,
                "The search bar must deplete in step with the real AI clock.");
            Capture(trial, "05-search-timer-half");
            for (var wait = Until(() => !trial.IsSearching,
                "beast search expires without another input"); wait.MoveNext();)
                yield return null;
            Require(trial.IsWrapped && !trial.IsSearching && !trial.HasBellTarget,
                "Search expiry must stop pursuit while leaving the wet cloth wrapped.");
            yield return null;
            Require(!intentMarker.enabled && ShowsPrompt(trial, "BEAST  /  RETURNING") &&
                !searchTrack.enabled && !searchFill.enabled &&
                trial.SearchRemainingFraction == 0f,
                "Search expiry must hide the target and timer while the beast goes home.");
            Require(!bellAudio.loop && !bellAudio.isPlaying,
                "The muffled bell must be silent while the beast returns.");
            Capture(trial, "05-beast-returning");
            for (var wait = Until(() => Vector2.Distance(trial.BeastPosition, home) < 0.10f,
                "beast returns to its original watch point"); wait.MoveNext();)
                yield return null;
            yield return null;
            Require(ShowsPrompt(trial, "BEAST  /  LISTENING"),
                "Once home, the beast must visibly resume listening instead of searching forever.");
            // A quiet wrapped bell does not reactivate pursuit until the player
            // intentionally unwraps and rings it again.
            Press(trial, ring: true);
            Require(!trial.HasBellTarget && !trial.IsSearching,
                "A wrapped bell must not restart tracking after the search expires.");
            // Returning the PLAYER alone does not count as saving the trapped
            // surveyor: wait until his on-screen figure reaches the same refuge.
            Press(trial, Vector2.left);
            for (var wait = Until(() => trial.PlayerPosition.x <= retreat.x + 0.12f,
                "lead the surveyor back toward the retreat entrance"); wait.MoveNext();)
                yield return null;
            Press(trial, Vector2.up);
            for (var wait = Until(() => trial.PlayerPosition.y >= retreat.y - 0.10f,
                "return to the safe retreat marker"); wait.MoveNext();)
                yield return null;
            Press(trial);
            Require(Vector2.Distance(trial.PlayerPosition, retreat) < 0.5f &&
                trial.SurveyorEscorting, "The player and escort must reach the same safety zone.");
            for (var wait = Until(() => trial.RescueComplete,
                "wait for the real surveyor to reach RETREAT"); wait.MoveNext();)
                yield return null;
            yield return null;
            Require(Vector2.Distance((Vector2)surveyor.transform.position, retreat) <= 0.98f &&
                trial.RescueComplete && !trial.CanEscortSurveyor && !beastContactRing.enabled &&
                ShowsPrompt(trial, "RESCUED: The surveyor reached RETREAT") &&
                ShowsPrompt(trial, "My marker was wrong"),
                "Rescue must require the actual NPC reaching the safe point and his own response.");
            Capture(trial, "10-surveyor-rescued");
            // Rescue success does not merely flip a dialogue: the actual NPC
            // walks to his misleading route sign and physically removes it.
            for (var wait = Until(() => trial.FalseMarkerCleared,
                "surveyor walks to remove the false trail sign"); wait.MoveNext();)
                yield return null;
            yield return null;
            Require(!falseTrailMarker.enabled && trial.RescueComplete &&
                ShowsPrompt(trial, "ROUTE CORRECTED") &&
                ShowsPrompt(trial, "I've taken down that false trail sign"),
                "The actual sign must disappear only after the surveyor reaches it.");
            Capture(trial, "11-route-marker-corrected");
            // The clue is optional: seeing the cart alone must not cause
            // a scripted automatic retrieval. The player chooses whether
            // to share it with the nearby rescued surveyor using real UGUI.
            Require(trial.CartObserved && !trial.CartClueShared && !trial.CartKitReclaimed &&
                cartMapKit.enabled && trial.CanShareCartClue &&
                TrialActionButton(trial, "Gate").interactable &&
                TrialActionButton(trial, "Gate").GetComponentInChildren<Text>().text
                    .Contains("TELL ABOUT CART", StringComparison.Ordinal) &&
                ShowsPrompt(trial, "Tell the surveyor about his cart"),
                "The surveyor needs the player's proximity and decision before retrieving his kit.");
            Vector2 undisclosedPosition = trial.SurveyorWorldPosition;
            for (int frame = 0; frame < 18; frame++)
                yield return null;
            Require(!trial.CartClueShared && !trial.CartKitReclaimed && cartMapKit.enabled &&
                Vector2.Distance(trial.SurveyorWorldPosition,
                    SoundwellTrial.CartEvidencePosition) > 1.5f,
                "Waiting without choosing to share the clue must not secretly retrieve the cart kit.");
            Capture(trial, "11-cart-clue-optional");
            ClickTrialAction(trial, "Gate");
            yield return null;
            Require(trial.CartClueShared && !trial.CanShareCartClue && !trial.CartKitReclaimed &&
                cartMapKit.enabled && ShowsPrompt(trial, "returning to the cart for his map kit"),
                "UGUI choice must share the actual clue, but the kit still needs physical retrieval.");
            Capture(trial, "11-cart-clue-shared");
            for (var wait = Until(() => trial.CartKitReclaimed,
                "surveyor actually reaches the cart to reclaim his map kit"); wait.MoveNext();)
                yield return null;
            yield return null;
            Require(trial.RescueComplete && trial.FalseMarkerCleared &&
                Vector2.Distance(trial.SurveyorWorldPosition,
                    SoundwellTrial.CartEvidencePosition) < 0.3f &&
                !cartMapKit.enabled && !trial.SafeRouteMarked && !safeTrailMarker.enabled &&
                ShowsPrompt(trial, "MAP KIT RECOVERED") &&
                ShowsPrompt(trial, "My map kit is safe"),
                "The map kit must disappear when the survivor reaches it, before a safe route exists.");
            Capture(trial, "12-surveyor-recovers-map-kit");
            for (var wait = Until(() => trial.SafeRouteMarked,
                "surveyor physically revisits the old sign to post a verified route"); wait.MoveNext();)
                yield return null;
            yield return null;
            Require(trial.RescueComplete && trial.FalseMarkerCleared &&
                trial.CartKitReclaimed && trial.SafeRouteMarked &&
                !falseTrailMarker.enabled && safeTrailMarker.enabled &&
                Vector2.Distance(trial.SurveyorWorldPosition,
                    (Vector2)falseTrailMarker.transform.position) < 0.28f &&
                ShowsPrompt(trial, "SAFE ROUTE POSTED") &&
                ShowsPrompt(trial, "verified route is posted"),
                "Verified route sign requires the map kit and real arrival at the old marker.");
            Capture(trial, "13-surveyor-posts-safe-route");
            SoundwellMemoryProbe.Capture("rescue_completed");

            // Returning after a real rescue must visibly carry its outcome
            // into Bootstrap. This is not a save or an invented ORK reward.
            int earnedContacts = trial.BeastContacts;
            Require(earnedContacts >= 1,
                "The actual encounter must remain recorded through the full rescue.");
            var finishedReturn = trial.ReturnToBootstrap();
            Require(finishedReturn != null && ReferenceEquals(finishedReturn,
                trial.ReturnToBootstrap()), "Completed return must be idempotent.");
            yield return finishedReturn;
            Require(SceneManager.GetActiveScene().name == SoundwellEntrance.BootstrapSceneName,
                "Completed rescue must return to the actual entry scene.");
            var victoryEntrance = FindRootComponent<SoundwellEntrance>();
            Require(victoryEntrance != null, "Completed return must show the actual entry UI.");
            for (var wait = Until(() => victoryEntrance.LastReturnOutcome == 2 &&
                victoryEntrance.LastReturnCartKitRecovered &&
                victoryEntrance.LastReturnSafeRouteMarked &&
                victoryEntrance.LastReturnBeastContacts == earnedContacts,
                "entry HUD receives rescue, recovered kit, safe route and true contact count"); wait.MoveNext();)
                yield return null;
            Require(ShowsPrompt(victoryEntrance, "SURVEYOR SAFE") &&
                ShowsPrompt(victoryEntrance, "FALSE TRAIL MARKER REMOVED") &&
                ShowsPrompt(victoryEntrance, "MAP KIT RECOVERED") &&
                ShowsPrompt(victoryEntrance, "SAFE ROUTE POSTED") &&
                ShowsPrompt(victoryEntrance, "BEAST CONTACTS: " + earnedContacts),
                "Completed return must report physical route and contact penalties, not only victory.");

            yield return victoryEntrance.OpenTrial();
            var trialAfterVictory = FindRootComponent<SoundwellTrial>();
            Require(trialAfterVictory != null && !ReferenceEquals(trialAfterVictory, trial),
                "The next attempt must create a new trial after victory.");
            for (var wait = Until(() => trialAfterVictory.IsReady,
                "initialise the replay after a successful return"); wait.MoveNext();)
                yield return null;
            Require(!trialAfterVictory.SurveyorEscorting &&
                !trialAfterVictory.RescueComplete && !trialAfterVictory.FalseMarkerCleared &&
                !trialAfterVictory.SafeRouteMarked &&
                !trialAfterVictory.BeastContacted && trialAfterVictory.BeastContacts == 0 &&
                !trialAfterVictory.RopeRecovered && !trialAfterVictory.SurveyorLineTied &&
                !trialAfterVictory.CartObserved && !trialAfterVictory.CartKitReclaimed &&
                !trialAfterVictory.CartClueShared &&
                !trialAfterVictory.LowerPlankBroken &&
                !trialAfterVictory.PlankSecured && !trialAfterVictory.GateOpen &&
                trialAfterVictory.PlayerPosition == retreat,
                "Replay after victory must start fresh without copying the previous rescue.");

            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            yield return SceneManager.LoadSceneAsync(SceneName);
            var fresh = FindRootComponent<SoundwellTrial>();
            Require(fresh != null && !ReferenceEquals(fresh, trial), "Reload must create a fresh trial.");
            for (var wait = Until(() => fresh.IsReady, "initialize the reloaded trial"); wait.MoveNext();)
                yield return null;
            SoundwellMemoryProbe.Capture("soundwell_reloaded");
            Require(SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese &&
                ShowsPrompt(fresh, "声井 / 信号试炼"),
                "Reload must retain the selected local HUD language.");
            // Capture the actual Chinese locale in the native GameView renderer,
            // not only English screenshots and string equality assertions.
            Capture(fresh, "14-localized-zh-Hans");
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            Require(ShowsPrompt(fresh, "SOUNDWELL  /  SIGNAL TRIAL"),
                "Reverting language must immediately update live UGUI after reload.");
            Require(!fresh.HasBellTarget, "fresh.HasBellTarget must be false.");
            Require(!fresh.IsWrapped, "fresh.IsWrapped must be false.");
            Require(!fresh.IsCarrying && fresh.BellPosition == bellStart && !fresh.CanPlaceBell,
                "Scene reload must restore the initial named bell stand and carrying state.");
            var freshGate = fresh.GetComponentsInChildren<BoxCollider2D>(true)
                .SingleOrDefault(body => body.gameObject.name == "PLACEHOLDER soundwell gate");
            var freshFalseMarker = fresh.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(item => item.gameObject.name == "PLACEHOLDER false trail marker");
            Require(fresh.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(item => item.gameObject.name.StartsWith("PLACEHOLDER revealed claw trail "))
                .Count(item => !item.enabled) == 3,
                "Scene reload must hide all revealed claw evidence until reinspection.");
            Require(!fresh.GetComponentsInChildren<SpriteRenderer>(true)
                .Any(item => item.gameObject.name.StartsWith(
                    "PLACEHOLDER beast search timer ") && item.enabled) &&
                fresh.SearchRemainingFraction == 0f,
                "Scene reload cannot inherit the prior search countdown.");
            Require(!SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LineRenderer>(true))
                .Any(line => line.gameObject.name == "PLACEHOLDER beast contact radius" &&
                    line.enabled),
                "A new scene must not retain an open-gate beast warning from the previous run.");
            Require(!fresh.GateOpen && !fresh.SurveyorEscorting && !fresh.RescueComplete &&
                !fresh.BeastContacted && fresh.BeastContacts == 0 &&
                !fresh.FalseMarkerCleared && !fresh.SafeRouteMarked &&
                !fresh.RopeObserved && !fresh.ClawsObserved &&
                !fresh.CartObserved && !fresh.CartKitReclaimed && !fresh.CartClueShared &&
                !fresh.RopeRecovered && !fresh.SurveyorLineTied &&
                freshFalseMarker != null &&
                freshFalseMarker.enabled && freshGate != null && freshGate.enabled,
                "Reload must restore the gate, wrong-way sign and survivor objective.");
            var freshBoard = fresh.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(renderer => renderer.gameObject.name ==
                    "PLACEHOLDER upper load-bearing plank");
            var freshSurveyor = fresh.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(renderer => renderer.gameObject.name == "PLACEHOLDER trapped surveyor");
            var freshLowerBoard = fresh.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(renderer => renderer.gameObject.name == "PLACEHOLDER unsafe lower plank");
            Require(!fresh.HasSurveyorClue && !fresh.PlankSecured && !fresh.LowerPlankBroken &&
                freshLowerBoard != null && freshLowerBoard.color == initialLowerBoard &&
                freshBoard != null && freshBoard.color == initialBoard && freshSurveyor != null &&
                Vector2.Distance((Vector2)freshSurveyor.transform.position,
                    SoundwellTrial.SurveyorPosition) < 0.01f,
                "Reload must clear secured-plank state and return the surveyor to his inner-room start.");
            Require(fresh.PlayerPosition == retreat, "Reload must restore the entrance position.");
            yield return null;
            var freshMarker = fresh.GetComponentsInChildren<SpriteRenderer>(true)
                .SingleOrDefault(renderer => renderer.gameObject.name == "PLACEHOLDER beast intent marker");
            Require(freshMarker != null && !freshMarker.enabled,
                "Reload must clear the visible beast target from the previous experiment.");
            // ORK can rename actors to their combatant asset name ("Default").
            // Check the real serialized player actor and the unique physics
            // controller in the current scene, not a presentation name.
            var reloadedBodies = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Rigidbody2D>(true))
                .ToArray();
            Require(reloadedBodies.Length == 1 &&
                reloadedBodies[0].transform == fresh.PlayerActor &&
                Vector2.Distance(reloadedBodies[0].position, fresh.PlayerPosition) < 0.01f,
                "Reload must have exactly one live scene physics player linked to the trial " +
                "(found " + reloadedBodies.Length + " Rigidbody2D controllers).");

            var back = fresh.ReturnToBootstrap();
            Require(back != null && ReferenceEquals(back, fresh.ReturnToBootstrap()),
                "Repeated exit requests must reuse the same load.");
            yield return back;
            Require(SceneManager.GetActiveScene().name == SoundwellEntrance.BootstrapSceneName,
                "Returning from the trial must restore the entry scene.");
            var returnedEntrance = FindRootComponent<SoundwellEntrance>();
            RequireInputSystemEventSystem("Bootstrap after trial return");
            Require(returnedEntrance != null && ShowsPrompt(returnedEntrance, "[F2 / Pad A] Enter the trial"),
                "The entry screen must be usable after returning from the trial.");
            yield return null;
            Require(returnedEntrance.LastReturnOutcome == 0 &&
                !returnedEntrance.LastReturnCartKitRecovered &&
                !returnedEntrance.LastReturnSafeRouteMarked &&
                returnedEntrance.LastReturnBeastContacts == 0 &&
                !ShowsPrompt(returnedEntrance, "BEAST CONTACTS:") &&
                !ShowsPrompt(returnedEntrance, "SURVEYOR SAFE") &&
                !ShowsPrompt(returnedEntrance, "MAP KIT RECOVERED") &&
                !ShowsPrompt(returnedEntrance, "SAFE ROUTE POSTED"),
                "A later incomplete return must clear rescue, map kit and safe route outcomes.");
            var submitButton = returnedEntrance.GetComponentInChildren<Button>();
            Require(submitButton != null && submitButton.interactable,
                "Returning must create a fresh actionable UGUI entry.");
            Require(EventSystem.current != null, "Returning must restore UI navigation.");
            EventSystem.current.SetSelectedGameObject(submitButton.gameObject);
            ExecuteEvents.Execute<ISubmitHandler>(submitButton.gameObject,
                new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Require(!submitButton.interactable,
                "Keyboard / controller Submit must activate and disable the entry button.");
            yield return returnedEntrance.OpenTrial();
            var restarted = FindRootComponent<SoundwellTrial>();
            Require(restarted != null, "The trial must be playable after returning to the entry screen.");
            for (var wait = Until(() => restarted.IsReady, "initialize the reentered trial"); wait.MoveNext();)
                yield return null;
            Require(!restarted.HasBellTarget && !restarted.IsWrapped && restarted.PlayerPosition == retreat,
                "Reentering from Bootstrap must start a fresh experiment.");

            // Simulated controller button values exercise the exact production mapping and
            // scene action boundary; a physical USB/Bluetooth gamepad remains untested.
            restarted.ReadInput = false;
            Require(SoundwellControlFrame.EnterRequested(false, true),
                "Controller south must be able to enter from the Bootstrap prompt.");
            Require(!SoundwellControlFrame.EnterRequested(false, false),
                "Unpressed entry controls must not trigger a scene change.");

            var idle = SoundwellControlFrame.FromButtons(Vector2.zero,
                false, false, false, false, false, false, false, false);
            Require(!idle.Ring && !idle.Wrap && !idle.Retry && !idle.Back &&
                !idle.Carry && !idle.Gate,
                "A neutral pad must not trigger an action.");
            var diagonal = SoundwellControlFrame.FromButtons(new Vector2(1f, 1f),
                false, false, false, false, false, false, false, false);
            Require(diagonal.Movement.magnitude <= 1.001f,
                "Analog diagonal movement must be normalized to prevent faster travel.");

            // Hold the stick across real physics frames until the saved player reaches the bell.
            for (var wait = Until(() => restarted.PlayerPosition.x >= -2.25f,
                "controller stick reaches the bell"); wait.MoveNext();)
            {
                restarted.ApplyControls(SoundwellControlFrame.FromButtons(Vector2.right,
                    false, false, false, false, false, false, false, false));
                yield return null;
            }
            restarted.ApplyControls(idle);
            yield return null;
            var shoulder = SoundwellControlFrame.FromButtons(Vector2.zero,
                false, false, false, false, false, false, false, false,
                false, true);
            Require(shoulder.Carry && !shoulder.Ring && !shoulder.Wrap &&
                !shoulder.Retry && !shoulder.Gate,
                "Controller left shoulder must map only to the portable bell action.");
            var rightShoulder = SoundwellControlFrame.FromButtons(Vector2.zero,
                false, false, false, false, false, false, false, false,
                false, false, false, true);
            Require(rightShoulder.Gate && !rightShoulder.Carry &&
                !rightShoulder.Ring && !rightShoulder.Wrap,
                "Controller right shoulder must map only to the gate interaction.");
            restarted.ApplyControls(shoulder);
            Require(restarted.IsCarrying, "Controller LB must actually lift the bell.");
            restarted.ApplyControls(shoulder);
            Require(!restarted.IsCarrying, "Controller LB must drop the same bell.");
            var south = SoundwellControlFrame.FromButtons(Vector2.zero,
                false, false, false, false, true, false, false, false);
            Require(south.Ring && !south.Wrap && !south.Retry && !south.Back,
                "Controller south must map only to ringing.");
            restarted.ApplyControls(south);
            Require(restarted.HasBellTarget, "Controller south must ring the saved scene bell.");

            var west = SoundwellControlFrame.FromButtons(Vector2.zero,
                false, false, false, false, false, false, true, false);
            Require(west.Wrap && !west.Ring && !west.Retry && !west.Back,
                "Controller west must map only to wrapping.");
            restarted.ApplyControls(west);
            Require(restarted.IsWrapped && restarted.IsSearching && !restarted.HasBellTarget,
                "Controller west must stop the clear bell and make the beast search.");

            for (var wait = Until(() => Vector2.Distance(restarted.PlayerPosition, retreat) < 0.25f,
                "controller stick returns to safety"); wait.MoveNext();)
            {
                restarted.ApplyControls(SoundwellControlFrame.FromButtons(Vector2.left,
                    false, false, false, false, false, false, false, false));
                yield return null;
            }
            restarted.ApplyControls(idle);
            yield return null;
            var north = SoundwellControlFrame.FromButtons(Vector2.zero,
                false, false, false, false, false, false, false, true);
            Require(north.Retry && !north.Ring && !north.Wrap && !north.Back,
                "Controller north must map only to resetting.");
            restarted.ApplyControls(north);
            Require(!restarted.IsWrapped && !restarted.IsSearching && !restarted.HasBellTarget,
                "Controller north must fully reset the soundwell trial.");

            var east = SoundwellControlFrame.FromButtons(Vector2.zero,
                false, false, false, false, false, true, false, false);
            Require(east.Back && !east.Ring && !east.Wrap && !east.Retry,
                "Controller east must map only to exit.");
            restarted.ApplyControls(east);
            yield return null;
            for (var wait = Until(() => SceneManager.GetActiveScene().name == SoundwellEntrance.BootstrapSceneName,
                "controller exit returns to the entry scene"); wait.MoveNext();)
                yield return null;
            Require(ShowsPrompt(FindRootComponent<SoundwellEntrance>(), "[F2 / Pad A] Enter the trial"),
                "The entry prompt must remain usable after the controller exit.");

            // Re-enter and click the actual HUD Back button, not the input adapter.
            yield return FindRootComponent<SoundwellEntrance>().OpenTrial();
            var mouseTrial = FindRootComponent<SoundwellTrial>();
            for (var wait = Until(() => mouseTrial.IsReady, "initialize mouse HUD trial"); wait.MoveNext();)
                yield return null;
            mouseTrial.ReadInput = false;
            yield return null;
            mouseTrial.SetActionMenuOpen(true);
            Require(mouseTrial.ActionMenuOpen && EventSystem.current.sendNavigationEvents,
                "The new scene must support action focus.");
            var first = EventSystem.current.currentSelectedGameObject;
            Require(first == TrialActionButton(mouseTrial, "Retry").gameObject,
                "At safety, focus must start at the enabled Reset button, not a disabled bell action.");
            var nav = new AxisEventData(EventSystem.current)
            {
                moveDir = MoveDirection.Down,
                moveVector = Vector2.down
            };
            ExecuteEvents.Execute<IMoveHandler>(first, nav, ExecuteEvents.moveHandler);
            var backSelection = TrialActionButton(mouseTrial, "Back");
            Require(EventSystem.current.currentSelectedGameObject == backSelection.gameObject,
                "Down/left-stick navigation must reach the Back action.");
            ExecuteEvents.Execute<ISubmitHandler>(backSelection.gameObject,
                new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            for (var wait = Until(() => SceneManager.GetActiveScene().name == SoundwellEntrance.BootstrapSceneName,
                "HUD Back returns to the entry scene"); wait.MoveNext();)
                yield return null;
            Require(FindRootComponent<SoundwellEntrance>() != null,
                "Focused UGUI Back Submit must return to a live entry scene.");
            RequireInputSystemEventSystem("Bootstrap after HUD exit");
            Require(EventSystem.current != null && EventSystem.current.sendNavigationEvents,
                "Returning to Bootstrap must restore its keyboard/gamepad UGUI navigation.");
            SoundwellMemoryProbe.Capture("bootstrap_after_return");
        }

        private static Button TrialActionButton(SoundwellTrial trial, string action)
        {
            var button = trial.GetComponentsInChildren<Button>(true)
                .SingleOrDefault(item => item.gameObject.name == "Soundwell " + action + " button");
            Require(button != null, "Missing real UGUI action button: " + action);
            return button;
        }

        private static void AssertHudHit(SoundwellTrial trial, string action)
        {
            var button = TrialActionButton(trial, action);
            var canvas = button.GetComponentInParent<Canvas>();
            var raycaster = canvas != null ? canvas.GetComponent<GraphicRaycaster>() : null;
            var eventSystem = EventSystem.current;
            Require(eventSystem != null && raycaster != null,
                "The action HUD must have an active EventSystem and GraphicRaycaster.");
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position)
            };
            var hits = new List<RaycastResult>();
            raycaster.Raycast(pointer, hits);
            Require(hits.Any(hit => hit.gameObject == button.gameObject),
                "The " + action + " button must be reached by a real UI raycast.");
        }

        private static void ClickTrialAction(SoundwellTrial trial, string action)
        {
            var button = TrialActionButton(trial, action);
            Require(button.interactable, "Disabled action cannot be clicked: " + action);
            AssertHudHit(trial, action);
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position)
            };
            ExecuteEvents.Execute<IPointerClickHandler>(button.gameObject, pointer,
                ExecuteEvents.pointerClickHandler);
        }

        private static bool ShowsPrompt(Component owner, string prompt) =>
            owner.GetComponentsInChildren<Text>().Any(label => label.text.Contains(prompt, StringComparison.Ordinal));

        private static T FindRootComponent<T>() where T : Component =>
            SceneManager.GetActiveScene().GetRootGameObjects()
                .Select(root => root.GetComponent<T>())
                .SingleOrDefault(component => component != null);

        private static void Press(SoundwellTrial trial, Vector2 direction = default, bool ring = false,
            bool wrap = false, bool retry = false, bool carry = false, bool gate = false) =>
            trial.ApplyInput(direction, ring, wrap, retry, carry, gate);

        private static IEnumerator Until(Func<bool> condition, string action)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Require(condition(), "Timed out: " + action);
        }

        private static void RequireInputSystemEventSystem(string context)
        {
            var systems = UnityEngine.Object.FindObjectsByType<EventSystem>();
            Require(systems.Length == 1, context + " must have exactly one active EventSystem; found " +
                systems.Length + ": " + string.Join(", ", systems.Select(system => system.gameObject.name)) + ".");
            var modules = systems[0].GetComponents<BaseInputModule>().Where(module => module != null && module.enabled).ToArray();
            Require(modules.Length == 1 && modules[0] is InputSystemUIInputModule,
                context + " must use exactly one active InputSystemUIInputModule and no legacy module.");
            var ui = (InputSystemUIInputModule)modules[0];
            Require(ui.actionsAsset != null && ui.move != null && ui.move.action != null &&
                ui.submit != null && ui.submit.action != null && ui.cancel != null && ui.cancel.action != null,
                context + " must have valid New Input System UI navigation, Submit and Cancel actions.");
            // The package's DefaultInputActions intentionally uses semantic usage
            // bindings (*/{Submit} and */{Cancel}), not literal button paths.
            // Resolve those bindings against a virtual pad so this checks the actual
            // controls that UGUI will receive rather than one serialized path spelling.
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                Require(ui.submit.action.controls.Any(control => control == gamepad.buttonSouth),
                    context + " must resolve gamepad South through UGUI Submit.");
                Require(ui.cancel.action.controls.Any(control => control == gamepad.buttonEast),
                    context + " must resolve gamepad East through UGUI Cancel.");
            }
            finally
            {
                InputSystem.RemoveDevice(gamepad);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static byte[] Capture(SoundwellTrial trial, string stage,
            int width = 960, int height = 540)
        {
            var camera = trial.TrialCamera;
            var actionHud = trial.GetComponentsInChildren<Canvas>(true).SingleOrDefault(
                canvas => canvas.gameObject.name == "Soundwell actions HUD");
            Require(actionHud != null && actionHud.renderMode == RenderMode.ScreenSpaceOverlay,
                "The live action HUD must use Screen Space Overlay before evidence capture.");

            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var previousMode = actionHud.renderMode;
            var previousHudCamera = actionHud.worldCamera;
            float previousPlaneDistance = actionHud.planeDistance;
            var target = RenderTexture.GetTemporary(width, height, 24);
            var frame = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;

                // Camera.Render alone omits Screen Space Overlay widgets, making CI
                // screenshots misleading. Capture an identical world-only frame
                // first, then render the real scene-local UGUI Canvas through the
                // same camera and assert that the final pixels actually changed.
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                frame.Apply();
                var worldPixels = frame.GetPixels32();

                // Temporary evidence-only switch; gameplay stays ScreenSpaceOverlay.
                // No synthetic UI, screenshot mock, or substitute scene is created.
                actionHud.worldCamera = camera;
                actionHud.planeDistance = 1f;
                actionHud.renderMode = RenderMode.ScreenSpaceCamera;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                frame.Apply();
                var imagePixels = frame.GetPixels32();
                if (stage == "14-localized-zh-Hans")
                {
                    // UI pixels from the Chinese title, excluding the ASCII
                    // slash. Prior screenshots had ZERO visible Chinese title
                    // pixels despite Font.HasCharacter returning true.
                    int glyphPixels = 0;
                    for (int y = 446; y < 477; y++)
                        for (int x = 399; x < 592; x++)
                        {
                            if (x >= 465 && x <= 503)
                                continue;
                            Color32 pixel = imagePixels[y * width + x];
                            if (pixel.r > 115 && pixel.g > 115 && pixel.b > 115)
                                glyphPixels++;
                        }
                    Require(glyphPixels >= 45,
                        "Chinese title lacks actual GameView glyph pixels: " + glyphPixels);
                }
                int changedSamples = 0;
                for (int i = 0; i < imagePixels.Length; i += 3)
                {
                    Color32 a = worldPixels[i];
                    Color32 b = imagePixels[i];
                    if (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) +
                        Math.Abs(a.b - b.b) > 45)
                        changedSamples++;
                }

                Require(changedSamples > 150,
                    $"Camera screenshot omitted the real UGUI action HUD: {stage} (changed {changedSamples} samples)");
                int colors = imagePixels.Where((_, index) => index % 173 == 0).Distinct().Count();
                Require(colors > 6, $"The captured scene is blank or incomplete: {stage}");
                byte[] png = frame.EncodeToPNG();
                const string directory = "artifacts/p0/soundwell";
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, stage + ".png"), png);
                return png;
            }
            finally
            {
                actionHud.renderMode = previousMode;
                actionHud.worldCamera = previousHudCamera;
                actionHud.planeDistance = previousPlaneDistance;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Canvas.ForceUpdateCanvases();
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(frame);
            }
        }
    }
}
#endif
