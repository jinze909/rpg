using System;
using System.Collections;
using System.Collections.Generic;
using GamingIsLove.ORKFramework;
using GamingIsLove.Makinom;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rpg.Soundwell
{
    /// <summary>Player-facing rescue encounter. All actions and health belong to ORK.</summary>
    public sealed class OrkRoleEncounter : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterSceneEntry()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SoundwellEntrance.SceneName || !SoundwellEntrance.ConsumeRoleEncounterRequest())
                return;
#if UNITY_EDITOR
            if (IsLegacySoundwellSmokeActive())
                return;
#endif
            var party = UnityEngine.Object.FindFirstObjectByType<SoundwellOrkParty>();
            if (party != null && party.GetComponent<OrkRoleEncounter>() == null)
                party.gameObject.AddComponent<OrkRoleEncounter>();
        }

#if UNITY_EDITOR
        private static bool IsLegacySoundwellSmokeActive()
        {
            // The legacy SoundwellTrial smoke test (SoundwellSmokeHost, in the
            // EditorSmoke assembly) clicks the visible Bootstrap entry button to
            // exercise the original SoundwellTrial movement/ring/wrap/retreat room.
            // Do not hijack its scene load with OrkRoleEncounter.
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (behaviour != null && behaviour.GetType().Name == "SoundwellSmokeHost")
                    return true;
            }
            return false;
        }
#endif

        public enum Threat { Gather, Lunge, Howl }

        private OrkPartyBridge party;
        private RoleCombatView view;
        private StrikeAction planned;
        private StrikeAction submittedEnemyThreat;
        private readonly Dictionary<Transform, Vector3> restingPositions = new Dictionary<Transform, Vector3>();
        private readonly HashSet<Combatant> submittedActions = new HashSet<Combatant>();
        private float previousDecisionTime;
        private bool configured;
        private bool observedBattle;
        private int presentationCount;
        private int lastCommandFrame = -1;
        private bool ending;
        private string lastExchange = string.Empty;
        private RpgLargeMapChunkRenderer mapRenderer;
        private readonly List<LineRenderer> threatLines = new List<LineRenderer>();
        private Material lineMaterial;
        private AudioSource audioSource;
        private AudioClip hitSound;

        // The trial camera may be retained by another scene/test. Never leak
        // role-combat framing or disabled follow components into exploration.
        private Camera encounterCamera;
        private Vector3 previousCameraPosition;
        private bool previousCameraOrthographic;
        private float previousCameraOrthographicSize;
        private Rect previousCameraRect;
        private Unity.Cinemachine.CinemachineBrain previousCameraBrain;
        private bool previousBrainEnabled;
        private SoundwellCameraRig previousCameraRig;
        private bool previousRigEnabled;
        private UnityEngine.Rendering.Universal.PixelPerfectCamera previousPixelCamera;
        private UnityEngine.Rendering.Universal.PixelPerfectCamera.CropFrame previousCropFrame;

        public OrkPartyBridge Party => party;
        public bool IsConfigured => configured;
        public bool IsPresenting => presentationCount > 0;
        // Read-only QA diagnostics for ORK's classic-turn action submission.
        public int SubmittedActionCount => submittedActions.Count;
        public int ActivePresentationCount => presentationCount;
        public bool EnemyChooserActive => party != null && party.Beast != null &&
            party.Beast.Actions.IsChoosing;
        public bool EnemyThreatSubmitted => submittedEnemyThreat != null;
        public bool IsActive => configured && party != null && party.BattleInProgress;
        public bool IsInterrupted => planned != null && planned.Interrupted;
        public bool IsExposed => party != null && party.Beast != null &&
            party.Beast.Variables.GetInt("rpg.role.exposed") > 0;
        public Threat PendingThreat => planned != null ? planned.Kind : Threat.Gather;
        public int TargetIndex => planned != null ? planned.TargetIndex : 0;
        public RoleCombatView View => view;

        private IEnumerator Start()
        {
            party = GetComponent<OrkPartyBridge>();
            while (party != null && !party.IsReady)
                yield return null;
            if (party == null) yield break;
            party.RoleEncounter = this;
            ConfigureWorld();

            previousDecisionTime = party.Beast.AI.DecisionTime;
            // ORK owns the chooser. Its decision window allows this encounter
            // to submit the already-advertised action before fallback AI runs.
            party.Beast.AI.DecisionTime = 120f;
            foreach (Combatant member in party.Members)
                InitializeHealth(member, 16);
            InitializeHealth(party.Beast, 36);
            party.Beast.Variables.Set("rpg.role.exposed", 0);
            planned = CreateThreat(Threat.Gather, 0);
            // The live battle chooses actions through ORK's Actions gate.
            // Do not also put this same mutable action in the AI queue:
            // AI may consume it before the scene sees IsChoosing, leaving
            // the opponent with no action until its long fallback timer.
            party.Beast.AI.ActionQueue.Clear();
            configured = true;
            view = new RoleCombatView(transform, this);
            SetExchange("救出被困的人。看清借声兽的动作，再决定怎样配合。",
                "Rescue the trapped surveyor. Read the beast, then choose your response.");
            party.TryStartDirectEncounter();
        }

        private static void InitializeHealth(Combatant actor, int amount)
        {
            StatusValue health = actor.Status[OrkPartyBridge.HealthStatusIndex];
            if (health.MaxStatusValue != null)
                health.MaxStatusValue.InitValue(amount);
            health.InitValue(amount);
        }

        private void Update()
        {
            if (!configured || party == null || view == null) return;
            if (party.BattleInProgress)
            {
                observedBattle = true;
                // Let ORK own the turn. Once the enemy becomes its live
                // chooser, submit the authored threat directly, exactly once.
                // The AI queue is not an authority for a scene-owned threat:
                // dequeuing it elsewhere can strand the classic turn.
                if (party.Beast.Actions.IsChoosing && !IsPresenting &&
                    planned != null && submittedEnemyThreat != planned &&
                    planned.CanUse())
                {
                    submittedEnemyThreat = planned;
                    party.Beast.Actions.AddAccess(planned, false);
                }
            }
            else if (observedBattle && !ending)
            {
                ending = true;
                submittedActions.Clear();
                submittedEnemyThreat = null;
                SetExchange(party.Beast.Status.IsDead
                    ? "借声兽退倒了，通道已经安全。" : "小队没能守住，重新整队再来。",
                    party.Beast.Status.IsDead
                    ? "The beast falls. The passage is safe." : "The party fell. Regroup and try again.");
            }
            view.Refresh(lastExchange, ending);
            RefreshThreatLines();
        }

        private void ConfigureWorld()
        {
            GetComponent<TrialFlowController>().enabled = false;
            foreach (Canvas legacy in GetComponentsInChildren<Canvas>(true))
                legacy.gameObject.SetActive(false);
            foreach (SpriteRenderer legacy in GetComponentsInChildren<SpriteRenderer>(true))
                legacy.enabled = false;
            foreach (UnityEngine.Tilemaps.TilemapRenderer legacy in GetComponentsInChildren<UnityEngine.Tilemaps.TilemapRenderer>(true))
                legacy.enabled = false;
            foreach (TextMesh legacy in GetComponentsInChildren<TextMesh>(true))
                legacy.gameObject.SetActive(false);

            AdventureArt art = Resources.Load<AdventureArt>("AdventureArt");
            if (art == null || art.Actors.Length < 4)
                throw new InvalidOperationException("The player encounter requires AdventureArt with its four actor sprites.");
            var layout = SoundwellLargeMapLayout.Build();
            mapRenderer = RpgLargeMapChunkRenderer.BuildForSoundwell(transform, layout, art.SoundwellTiles);
            Vector2[] positions = { new Vector2(39, 28), new Vector2(37, 29), new Vector2(37, 26), new Vector2(45, 28) };
            for (int i = 0; i < 4; i++)
            {
                Combatant actor = i == 3 ? party.Beast : party.Members[i];
                var body = actor.GameObject.GetComponent<Rigidbody2D>();
                if (body != null) { body.linearVelocity = Vector2.zero; body.position = positions[i]; }
                actor.GameObject.transform.position = positions[i];
                var visual = new GameObject("Rescue actor visual " + i).AddComponent<SpriteRenderer>();
                visual.transform.SetParent(actor.GameObject.transform, false);
                visual.sprite = art.Actors[i];
                visual.sortingOrder = 30;
                visual.flipX = i == 3;
            }
            Camera camera = Camera.main;
            if (camera != null)
            {
                encounterCamera = camera;
                previousCameraPosition = camera.transform.position;
                previousCameraOrthographic = camera.orthographic;
                previousCameraOrthographicSize = camera.orthographicSize;
                previousCameraRect = camera.rect;
                previousCameraBrain = camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
                if (previousCameraBrain != null)
                {
                    previousBrainEnabled = previousCameraBrain.enabled;
                    previousCameraBrain.enabled = false;
                }
                previousCameraRig = camera.GetComponent<SoundwellCameraRig>();
                if (previousCameraRig != null)
                {
                    previousRigEnabled = previousCameraRig.enabled;
                    previousCameraRig.enabled = false;
                }
                previousPixelCamera = camera.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
                if (previousPixelCamera != null)
                {
                    previousCropFrame = previousPixelCamera.cropFrame;
                    previousPixelCamera.cropFrame = UnityEngine.Rendering.Universal.PixelPerfectCamera.CropFrame.None;
                }
                camera.transform.position = new Vector3(41f, 28f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = 5.625f;
            }
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            for (int i = 0; i < 3; i++)
            {
                var line = new GameObject("Queued beast target " + i).AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.sharedMaterial = lineMaterial;
                line.positionCount = 2;
                line.startWidth = line.endWidth = 1f / 16f;
                line.sortingOrder = 29;
                line.enabled = false;
                threatLines.Add(line);
            }
            audioSource = gameObject.AddComponent<AudioSource>();
            SoundwellAudioBus.Route(audioSource);
            hitSound = AudioClip.Create("Rescue impact", 4410, 1, 44100, false);
            var samples = new float[4410];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = Mathf.Sin(i * 0.19f) * Mathf.Exp(-i / 520f) * 0.25f;
            hitSound.SetData(samples, 0);
            Physics2D.SyncTransforms();
        }

        private void RefreshThreatLines()
        {
            for (int i = 0; i < threatLines.Count; i++)
            {
                LineRenderer line = threatLines[i];
                line.enabled = IsActive && !IsPresenting && planned != null && !planned.Interrupted &&
                    (planned.Kind == Threat.Howl || i == planned.TargetIndex);
                if (!line.enabled) continue;
                line.startColor = line.endColor = planned.Kind == Threat.Howl
                    ? new Color(0.95f, 0.65f, 0.20f, 0.7f) : new Color(0.95f, 0.27f, 0.23f, 0.7f);
                line.SetPosition(0, party.Beast.GameObject.transform.position + Vector3.up * 0.5f);
                line.SetPosition(1, party.Members[i].GameObject.transform.position + Vector3.up * 0.5f);
            }
        }

        public bool CanChoose(int option)
        {
            if (!IsActive || IsPresenting || option < 0 || option > 1) return false;
            if (lastCommandFrame == Time.frameCount) return false;
            int index = party.ChoosingRescuerIndex;
            if (index < 0 || index >= party.Members.Count) return false;
            Combatant actor = party.Members[index];
            if (actor == null || !actor.Actions.IsChoosing || submittedActions.Contains(actor))
                return false;
            if (index == 1 && option == 1)
                return planned != null && !planned.Interrupted && planned.Kind == Threat.Lunge;
            if (index == 2 && option == 0)
                return planned != null && !planned.Interrupted && planned.Kind == Threat.Howl;
            return true;
        }

        public bool Choose(int option)
        {
            if (!CanChoose(option)) return false;
            int index = party.ChoosingRescuerIndex;
            Combatant actor = party.Members[index];
            BaseAction action;
            if (index == 0 && option == 1)
            {
                action = new UtilityAction(this, actor, () =>
                {
                    if (planned != null)
                    {
                        planned.SetTarget(actor);
                        planned.TargetIndex = index;
                        planned.Covered = true;
                    }
                    SetExchange("前卫举盾接下这次袭击，伤害降低。", "The vanguard covers the party and weakens the next strike.");
                }, true);
            }
            else if ((index == 1 && option == 1) || (index == 2 && option == 0))
            {
                Threat countered = index == 1 ? Threat.Lunge : Threat.Howl;
                action = new UtilityAction(this, actor, () =>
                {
                    if (planned != null && planned.Kind == countered)
                    {
                        // Keep the action eligible for ORK's lifecycle so its
                        // finishedCallback advances the threat queue while
                        // Calculate resolves with empty targets (zero damage).
                        planned.Interrupted = true;
                        party.Beast.Variables.Set("rpg.role.exposed", 1);
                        SetExchange(index == 1 ? "铃声引偏猛扑！借声兽露出了破绽。" : "湿布压住回声！嚎叫被打断。",
                            index == 1 ? "The decoy bell cancels the lunge. The beast is exposed!" : "The wet cloth muffles the echo. The howl is cancelled!");
                    }
                }, false);
            }
            else if (index == 2 && option == 1)
            {
                action = new UtilityAction(this, actor, () =>
                {
                    Combatant injured = null;
                    foreach (Combatant member in party.Members)
                        if (!member.Status.IsDead && (injured == null ||
                            member.Status[1].GetValue() < injured.Status[1].GetValue()))
                            injured = member;
                    if (injured == null) return;
                    StatusValue health = injured.Status[1];
                    health.SetValueAccess(Mathf.Min(health.GetMaxValue(), health.GetValue() + 5),
                        false, false, false, true, false, null);
                    SetExchange("支援队员完成包扎，受伤的伙伴恢复了体力。", "The support bandages the wounded ally, restoring health.");
                }, true);
            }
            else
            {
                float strength = index == 1 ? (IsExposed ? 10f : 4f) : 2f;
                action = new StrikeAction(this, actor, party.Beast, strength, false,
                    Threat.Gather, 0);
            }
            if (!action.CanUse()) return false;
            // In ORK Classic turn-based mode, each party member submits its
            // action before any PerformAction runs. Track per-actor submission
            // and same-frame debounce without blocking the next choosing ally.
            lastCommandFrame = Time.frameCount;
            submittedActions.Add(actor);
            action.finishedCallback += _ => submittedActions.Remove(actor);
            actor.Actions.AddAccess(action, false);
            return true;
        }

        private StrikeAction CreateThreat(Threat kind, int target)
        {
            var action = new StrikeAction(this, party.Beast, party.Members[target],
                kind == Threat.Lunge ? 8f : 4f, true, kind, target);
            if (kind == Threat.Howl)
                action.SetTargets(new List<Combatant>(party.Members));
            action.finishedCallback += _ =>
            {
                if (this == null || ending || party == null || party.Beast.Status.IsDead) return;
                Threat next = kind == Threat.Lunge ? Threat.Howl : Threat.Lunge;
                int nextTarget = kind == Threat.Gather ? target : (target + 1) % party.Members.Count;
                planned = CreateThreat(next, nextTarget);
                submittedEnemyThreat = null;
            };
            return action;
        }

        public void Leave()
        {
            if (party == null || IsPresenting) return;
            if (observedBattle && party.Beast.Status.IsDead && party.SettleRescue())
                Maki.Game.Variables.Set(SoundwellSignalKeys.RescueKey, 1);
            party.ResetEncounter();
            GetComponent<TrialFlowController>().ReturnToBootstrap();
        }

        private void SetExchange(string zh, string en) =>
            lastExchange = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese ? zh : en;

        private IEnumerator Present(Combatant actor, Combatant target, bool enemy, bool utility,
            bool interrupted)
        {
            presentationCount++;
            try
            {
                Transform visual = FindVisual(actor);
                Vector3 home = visual != null ? visual.localPosition : Vector3.zero;
                if (visual != null) restingPositions[visual] = home;
                float elapsed = 0f;
                while (elapsed < 0.32f && actor != null && actor.GameObject != null)
                {
                    elapsed += Time.unscaledDeltaTime;
                    if (visual != null)
                    {
                        Vector3 direction = target != null && target.GameObject != null
                            ? (target.GameObject.transform.position - actor.GameObject.transform.position).normalized : Vector3.right;
                        float distance = utility || interrupted ? 0.12f :
                            Mathf.Max(0.3f, Vector3.Distance(actor.GameObject.transform.position,
                                target.GameObject.transform.position) - 0.8f);
                        visual.localPosition = home + direction * Mathf.Sin(Mathf.Clamp01(elapsed / 0.32f) * Mathf.PI) * distance;
                    }
                    yield return null;
                }
                if (visual != null) visual.localPosition = home;
                if (this != null && actor != null && actor.GameObject != null && !ending)
                {
                    // ORK's base PerformAction has already run synchronously
                    // on the live battle action, never from this delayed
                    // presentation coroutine. This effect cannot own HP,
                    // callbacks or battle-turn advancement.
                    if (!utility && !interrupted && audioSource != null)
                        audioSource.PlayOneShot(hitSound);
                    if (!utility)
                        SetExchange(interrupted ? "袭击已被打断，小队毫发无伤。" : enemy ? "借声兽的袭击落下了！" : "攻击命中，继续寻找下一次配合。",
                            interrupted ? "Interrupted. The party takes no damage." : enemy ? "The beast strikes!" : "Hit! Prepare the next combination.");
                }
                yield return new WaitForSecondsRealtime(0.16f);
            }
            finally
            {
                if (!enemy && actor != null)
                    submittedActions.Remove(actor);
                presentationCount = Mathf.Max(0, presentationCount - 1);
            }
        }

        private static Transform FindVisual(Combatant actor)
        {
            if (actor == null || actor.GameObject == null) return null;
            foreach (SpriteRenderer renderer in actor.GameObject.GetComponentsInChildren<SpriteRenderer>())
                if (renderer.enabled && renderer.color.a > 0f) return renderer.transform;
            return null;
        }

        private void OnDestroy()
        {
            // Restore the owning camera before another scene uses its live
            // pixel-perfect framing; do not resurrect destroyed scene objects.
            if (encounterCamera != null)
            {
                encounterCamera.transform.position = previousCameraPosition;
                encounterCamera.orthographic = previousCameraOrthographic;
                encounterCamera.orthographicSize = previousCameraOrthographicSize;
                encounterCamera.rect = previousCameraRect;
            }
            if (previousPixelCamera != null)
                previousPixelCamera.cropFrame = previousCropFrame;
            if (previousCameraRig != null)
                previousCameraRig.enabled = previousRigEnabled;
            if (previousCameraBrain != null)
                previousCameraBrain.enabled = previousBrainEnabled;

            foreach (var item in restingPositions)
                if (item.Key != null) item.Key.localPosition = item.Value;
            if (configured && party != null && party.Beast != null)
                party.Beast.AI.DecisionTime = previousDecisionTime;
            mapRenderer?.Dispose();
            if (lineMaterial != null) Destroy(lineMaterial);
            if (hitSound != null) Destroy(hitSound);
            view?.Dispose();
        }

        private sealed class StrikeAction : AbilityAction
        {
            private readonly OrkRoleEncounter owner;
            private readonly float strength;
            private readonly bool enemy;
            public readonly Threat Kind;
            public int TargetIndex;
            public bool Interrupted;
            public bool Covered;

            public StrikeAction(OrkRoleEncounter owner, Combatant actor, Combatant target,
                float strength, bool enemy, Threat kind, int targetIndex)
                : base(actor, actor.Abilities.GetCurrentBaseAttack())
            {
                this.owner = owner;
                this.strength = strength;
                this.enemy = enemy;
                Kind = kind;
                TargetIndex = targetIndex;
                SetTarget(target);
            }

            protected override void ActionStartSetup() { }
            public override bool CanUse() => base.CanUse();
            public override void PerformAction()
            {
                // ORK owns current-action progression. Deferring its base
                // PerformAction into a Unity coroutine causes a classic-turn
                // deadlock after the first hit: queued allies remain suspended,
                // while ORK grants no next chooser. Start graphics first but
                // invoke the actual ORK ability on this same action call.
                owner.StartCoroutine(owner.Present(User,
                    enemy ? owner.party.Members[TargetIndex] : owner.party.Beast,
                    enemy, enemy && Kind == Threat.Gather, Interrupted));
                base.PerformAction();
            }

            public override void Calculate(List<Combatant> targets, float damageFactor,
                ActionCalculationOptions options, GameObject flyingTextTarget, ActionCalculationFinished notify)
            {
                if (enemy && (Kind == Threat.Gather || Interrupted))
                {
                    // Complete the real ORK action lifecycle and invoke notify
                    // with an empty target list so minimum-damage clamps or
                    // target status changes can never damage the party.
                    base.Calculate(new List<Combatant>(), 0f, options, flyingTextTarget, notify);
                    return;
                }
                bool consumeExposed = !enemy && User == owner.party.Members[1] && owner.IsExposed;
                base.Calculate(targets, damageFactor * strength * (Covered ? 0.25f : 1f),
                    options, flyingTextTarget, notify);
                if (consumeExposed && owner != null && owner.party != null && owner.party.Beast != null)
                    owner.party.Beast.Variables.Set("rpg.role.exposed", 0);
            }
        }

        private sealed class UtilityAction : NoneAction
        {
            private readonly OrkRoleEncounter owner;
            private readonly Action effect;
            private readonly bool supportive;

            public UtilityAction(OrkRoleEncounter owner, Combatant actor, Action effect, bool supportive)
                : base(actor, false)
            {
                this.owner = owner;
                this.effect = effect;
                this.supportive = supportive;
            }

            protected override void ActionStartSetup() { }
            public override void PerformAction()
            {
                owner.StartCoroutine(owner.Present(
                    User, supportive ? User : owner.party.Beast,
                    false, true, false));
                // Utility actions must obey the same synchronous ORK lifecycle
                // as attacks; only their presentation animates asynchronously.
                base.PerformAction();
            }

            public override void Calculate(List<Combatant> targets, float damageFactor,
                ActionCalculationOptions options, GameObject flyingTextTarget, ActionCalculationFinished notify)
            {
                effect();
                base.Calculate(targets, damageFactor, options, flyingTextTarget, notify);
            }
        }
    }
}
