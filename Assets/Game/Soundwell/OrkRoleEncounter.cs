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
        private Threat pendingThreat = Threat.Gather;
        private int targetIndex;
        private bool interrupted;
        private bool covered;
        private int lastRoundTurn = -1;
        private int[] roundStartPartyHp = new int[3];
        private readonly Dictionary<Transform, Vector3> restingPositions = new Dictionary<Transform, Vector3>();
        private readonly HashSet<Combatant> submittedActions = new HashSet<Combatant>();
        private readonly HashSet<Combatant> turnSubmittedParty = new HashSet<Combatant>();
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
        private AdventureArt art;
        private SpriteRenderer beastRenderer;
        private Sprite performingBeastPose;
        private int lastVisualBeastHp;
        private float beastRecoilUntil;

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
        // Read-only QA diagnostics for ORK's turn-based action submission.
        public int SubmittedActionCount => submittedActions.Count;
        public int TurnSubmittedPartyCount => turnSubmittedParty.Count;
        public bool PlannedPerformed => lastRoundTurn > 1;
        public int ActivePresentationCount => presentationCount;
        public bool EnemyChooserActive => party != null && party.Beast != null &&
            party.Beast.Actions.IsChoosing;
        public bool EnemyThreatSubmitted => false;
        public bool IsActive => configured && party != null && party.BattleInProgress;
        public bool IsInterrupted => interrupted;
        public bool IsExposed => party != null && party.Beast != null &&
            party.Beast.Variables.GetInt("rpg.role.exposed") > 0;
        public Threat PendingThreat => pendingThreat;
        public int TargetIndex => targetIndex;
        public RoleCombatView View => view;
        internal (Threat Kind, int TargetIndex, bool Interrupted)? ActiveEnemyAction { get; private set; }

        private IEnumerator Start()
        {
            party = GetComponent<OrkPartyBridge>();
            while (party != null && !party.IsReady)
                yield return null;
            if (party == null) yield break;
            party.RoleEncounter = this;
            ConfigureWorld();

            foreach (Combatant member in party.Members)
                InitializeHealth(member, 16);
            InitializeHealth(party.Beast, 36);
            lastVisualBeastHp = party.Beast.Status[OrkPartyBridge.HealthStatusIndex].GetValue();
            party.Beast.Variables.Set("rpg.role.exposed", 0);
            pendingThreat = Threat.Gather;
            targetIndex = 0;
            interrupted = false;
            covered = false;
            lastRoundTurn = -1;
            turnSubmittedParty.Clear();
            submittedActions.Clear();
            configured = true;
            view = new RoleCombatView(transform, this);
            SetExchange("救出被困的人。看清借声兽的动作，再决定怎样配合。",
                "Rescue the trapped surveyor. Read the beast, then choose your response.");
            // Leave Beast.AI untouched (same native ORK Turn-Based setup proven
            // by SoundwellOrkBattleTests) so enemy turns never deadlock.
            party.TryStartDirectEncounter();
        }

        private static void InitializeHealth(Combatant actor, int amount)
        {
            StatusValue health = actor.Status[OrkPartyBridge.HealthStatusIndex];
            if (health.MaxStatusValue != null)
                health.MaxStatusValue.InitValue(amount);
            health.InitValue(amount);
        }

        private int MaxLivingMemberTurn()
        {
            int maxTurn = -1;
            foreach (Combatant member in party.Members)
                if (member != null && !member.Status.IsDead && member.Battle.Turn > maxTurn)
                    maxTurn = member.Battle.Turn;
            return maxTurn;
        }

        private void SnapshotPartyHealth()
        {
            if (roundStartPartyHp.Length != party.Members.Count)
                roundStartPartyHp = new int[party.Members.Count];
            for (int i = 0; i < party.Members.Count; i++)
                roundStartPartyHp[i] = party.Members[i] != null
                    ? party.Members[i].Status[OrkPartyBridge.HealthStatusIndex].GetValue()
                    : 0;
        }

        private void Update()
        {
            if (!configured || party == null || view == null) return;
            if (party.BattleInProgress)
            {
                int currentTurn = MaxLivingMemberTurn();
                if (!observedBattle)
                {
                    observedBattle = true;
                    lastRoundTurn = currentTurn;
                    SnapshotPartyHealth();
                }
                else if (currentTurn > lastRoundTurn && lastRoundTurn >= 0 &&
                    !party.Beast.Status.IsDead)
                {
                    // ORK's native turn-based loop completed the enemy action
                    // and advanced the party to the next turn. Resolve the
                    // telegraphed threat outcome and advance to the next beat.
                    lastRoundTurn = currentTurn;
                    ResolveCompletedEnemyTurn();
                }
            }
            else if (observedBattle && !ending)
            {
                ending = true;
                submittedActions.Clear();
                turnSubmittedParty.Clear();
                SetExchange(party.Beast.Status.IsDead
                    ? "借声兽退倒了，通道已经安全。" : "小队没能守住，重新整队再来。",
                    party.Beast.Status.IsDead
                    ? "The beast falls. The passage is safe." : "The party fell. Regroup and try again.");
            }
            RefreshBeastPose();
            view.Refresh(lastExchange, ending);
            RefreshThreatLines();
        }

        private void ResolveCompletedEnemyTurn()
        {
            Threat resolved = pendingThreat;
            int resolvedTarget = Mathf.Clamp(targetIndex, 0, party.Members.Count - 1);
            bool wasInterrupted = interrupted;
            bool wasCovered = covered;

            if (resolved == Threat.Gather || wasInterrupted)
            {
                // Restore any 1-HP scratch from ORK's native placeholder enemy
                // base attack so Gather and cancelled threats deal zero damage.
                for (int i = 0; i < party.Members.Count; i++)
                {
                    Combatant member = party.Members[i];
                    if (member == null || i >= roundStartPartyHp.Length) continue;
                    StatusValue hp = member.Status[OrkPartyBridge.HealthStatusIndex];
                    if (hp.GetValue() < roundStartPartyHp[i])
                        hp.SetValueAccess(roundStartPartyHp[i],
                            false, false, false, true, false, null);
                }
            }
            else
            {
                int baseDamage = resolved == Threat.Lunge ? 8 : 4;
                int actualDamage = wasCovered ? Mathf.Max(1, Mathf.RoundToInt(baseDamage * 0.25f)) : baseDamage;
                for (int i = 0; i < party.Members.Count; i++)
                {
                    Combatant member = party.Members[i];
                    if (member == null || i >= roundStartPartyHp.Length) continue;
                    int desiredHp = (resolved == Threat.Howl || i == resolvedTarget)
                        ? Mathf.Max(0, roundStartPartyHp[i] - actualDamage)
                        : roundStartPartyHp[i];
                    StatusValue hp = member.Status[OrkPartyBridge.HealthStatusIndex];
                    if (hp.GetValue() != desiredHp)
                        hp.SetValueAccess(desiredHp,
                            false, false, false, true, false, null);
                }
            }

            StartCoroutine(Present(party.Beast, party.Members[resolvedTarget],
                (resolved, resolvedTarget, wasInterrupted), resolved == Threat.Gather));

            pendingThreat = resolved == Threat.Lunge ? Threat.Howl : Threat.Lunge;
            targetIndex = resolved == Threat.Gather
                ? resolvedTarget
                : (resolvedTarget + 1) % party.Members.Count;
            interrupted = false;
            covered = false;
            turnSubmittedParty.Clear();
            SnapshotPartyHealth();
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

            art = Resources.Load<AdventureArt>("AdventureArt");
            if (art == null || art.Actors == null || art.Actors.Length < 4 ||
                art.Actors[3] == null || art.BeastWindup == null ||
                art.BeastLunge == null || art.BeastRecoil == null || art.BeastStagger == null)
                throw new InvalidOperationException("The player encounter requires the authored actors and beast poses in AdventureArt.");
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
                if (i == 3)
                    beastRenderer = visual;
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

        private void RefreshBeastPose()
        {
            if (beastRenderer == null || party.Beast == null || party.Beast.Status.IsDead)
                return;

            // Observe the same living ORK actor used by the health bar.
            // The pose never deals damage or keeps a defeated actor alive.
            int hp = party.Beast.Status[OrkPartyBridge.HealthStatusIndex].GetValue();
            float now = Time.unscaledTime;
            if (hp > 0 && hp < lastVisualBeastHp)
                beastRecoilUntil = now + 0.32f;
            lastVisualBeastHp = hp;

            Sprite pose = art.Actors[3];
            if (!ending)
            {
                // An accepted lunge travels during the same short window as recoil.
                // Keep the outgoing pose readable if a previous hit is still fading.
                if (performingBeastPose == art.BeastLunge)
                    pose = performingBeastPose;
                else if (now < beastRecoilUntil)
                    pose = art.BeastRecoil;
                else if (performingBeastPose != null)
                    pose = performingBeastPose;
                else if (interrupted)
                    pose = art.BeastStagger;
                else if (pendingThreat != Threat.Gather)
                    pose = art.BeastWindup;
            }

            if (beastRenderer.sprite != pose)
                beastRenderer.sprite = pose;
        }

        private void RefreshThreatLines()
        {
            for (int i = 0; i < threatLines.Count; i++)
            {
                LineRenderer line = threatLines[i];
                line.enabled = IsActive && !IsPresenting && !interrupted &&
                    (pendingThreat == Threat.Howl || i == targetIndex);
                if (!line.enabled) continue;
                line.startColor = line.endColor = pendingThreat == Threat.Howl
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
                return !interrupted && pendingThreat == Threat.Lunge;
            if (index == 2 && option == 0)
                return !interrupted && pendingThreat == Threat.Howl;
            return true;
        }

        public bool Choose(int option)
        {
            if (!CanChoose(option)) return false;
            int index = party.ChoosingRescuerIndex;
            Combatant actor = party.Members[index];

            // Player attacks use the exact native ORK AbilityShortcut.Use path
            // proven by OrkPartyBridge.TryAttackBeast, plus role scaling.
            if ((index == 0 && option == 0) || (index == 1 && option == 0))
            {
                AbilityShortcut attack = actor.Abilities.GetCurrentBaseAttack();
                if (attack == null || !attack.CanTarget(actor, party.Beast))
                    return false;
                bool consumeExposed = index == 1 && IsExposed;
                int bonusDamage = index == 1 ? (consumeExposed ? 9 : 3) : 1;
                if (!attack.Use(actor, new List<Combatant> { party.Beast }, true))
                    return false;
                lastCommandFrame = Time.frameCount;
                submittedActions.Add(actor);
                turnSubmittedParty.Add(actor);
                StatusValue beastHp = party.Beast.Status[OrkPartyBridge.HealthStatusIndex];
                if (bonusDamage > 0 && !party.Beast.Status.IsDead)
                    beastHp.SetValueAccess(Mathf.Max(0, beastHp.GetValue() - bonusDamage),
                        false, false, false, true, false, null);
                if (consumeExposed)
                    party.Beast.Variables.Set("rpg.role.exposed", 0);
                StartCoroutine(Present(actor, party.Beast, null, false));
                return true;
            }

            // Spend the role's major action through ORK's existing, verified
            // DefendShortcut lifecycle instead of queuing a scene-authored
            // NoneAction. The old queued utility could remain unfinished while
            // all four combatants were Available, stranding the native chooser.
            if (!party.CanGuard(index) ||
                !actor.Shortcuts.DefendShortcut.Use(
                    actor, new List<Combatant> { actor }, true))
                return false;

            lastCommandFrame = Time.frameCount;
            submittedActions.Add(actor);
            turnSubmittedParty.Add(actor);

            // Only apply the role's supplementary effect after ORK accepted
            // and consumed the native action. ORK still owns turns, combatants,
            // the defense status, health values, and victory.
            if (index == 0 && option == 1)
            {
                targetIndex = index;
                covered = true;
                SetExchange("前卫举盾接下这次袭击，伤害降低。",
                    "The vanguard covers the party and weakens the next strike.");
            }
            else if ((index == 1 && option == 1) || (index == 2 && option == 0))
            {
                Threat countered = index == 1 ? Threat.Lunge : Threat.Howl;
                if (pendingThreat == countered)
                {
                    interrupted = true;
                    party.Beast.Variables.Set("rpg.role.exposed", 1);
                    SetExchange(index == 1
                            ? "铃声引偏猛扑！借声兽露出了破绽。" : "湿布压住回声！嚎叫被打断。",
                        index == 1
                            ? "The decoy bell cancels the lunge. The beast is exposed!"
                            : "The wet cloth muffles the echo. The howl is cancelled!");
                }
            }
            else
            {
                int injuredIndex = -1;
                Combatant injured = null;
                for (int i = 0; i < party.Members.Count; i++)
                {
                    Combatant member = party.Members[i];
                    if (!member.Status.IsDead && (injured == null ||
                        member.Status[OrkPartyBridge.HealthStatusIndex].GetValue() <
                        injured.Status[OrkPartyBridge.HealthStatusIndex].GetValue()))
                    {
                        injured = member;
                        injuredIndex = i;
                    }
                }
                if (injured != null)
                {
                    StatusValue health = injured.Status[OrkPartyBridge.HealthStatusIndex];
                    int healed = Mathf.Min(health.GetMaxValue(), health.GetValue() + 5);
                    health.SetValueAccess(healed, false, false, false, true, false, null);
                    if (injuredIndex >= 0 && injuredIndex < roundStartPartyHp.Length)
                        roundStartPartyHp[injuredIndex] = healed;
                    SetExchange("支援队员完成包扎，受伤的伙伴恢复了体力。",
                        "The support bandages the wounded ally, restoring health.");
                }
            }

            StartCoroutine(Present(actor, actor, null, true));
            return true;
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

        private IEnumerator Present(Combatant actor, Combatant target,
            (Threat Kind, int TargetIndex, bool Interrupted)? enemyAction, bool utility)
        {
            bool enemy = enemyAction.HasValue;
            bool interruptedAction = enemyAction?.Interrupted ?? false;
            presentationCount++;
            if (enemyAction is { } action)
            {
                // The queued threat advances as soon as this action resolves.
                // Hold the outgoing action for its pose and HUD.
                ActiveEnemyAction = action;
                performingBeastPose = interruptedAction ? art.BeastStagger
                    : action.Kind == Threat.Lunge ? art.BeastLunge : art.BeastWindup;
            }
            try
            {
                Transform visual = enemy && beastRenderer != null ? beastRenderer.transform : FindVisual(actor);
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
                        float distance = utility || interruptedAction ? 0.12f :
                            Mathf.Max(0.3f, Vector3.Distance(actor.GameObject.transform.position,
                                target.GameObject.transform.position) - 0.8f);
                        Vector3 offset = direction * Mathf.Sin(Mathf.Clamp01(elapsed / 0.32f) * Mathf.PI) * distance;
                        if (enemy && visual.parent != null)
                            offset = visual.parent.InverseTransformVector(offset);
                        visual.localPosition = home + offset;
                    }
                    yield return null;
                }
                if (visual != null) visual.localPosition = home;
                if (this != null && actor != null && actor.GameObject != null && !ending)
                {
                    if (!utility && !interruptedAction && audioSource != null)
                        audioSource.PlayOneShot(hitSound);
                    if (!utility)
                        SetExchange(interruptedAction ? "袭击已被打断，小队毫发无伤。" : enemy ? "借声兽的袭击落下了！" : "攻击命中，继续寻找下一次配合。",
                            interruptedAction ? "Interrupted. The party takes no damage." : enemy ? "The beast strikes!" : "Hit! Prepare the next combination.");
                }
                yield return new WaitForSecondsRealtime(0.16f);
            }
            finally
            {
                if (enemy)
                {
                    ActiveEnemyAction = null;
                    performingBeastPose = null;
                }
                else if (actor != null)
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
            mapRenderer?.Dispose();
            if (lineMaterial != null) Destroy(lineMaterial);
            if (hitSound != null) Destroy(hitSound);
            view?.Dispose();
        }


    }
}
