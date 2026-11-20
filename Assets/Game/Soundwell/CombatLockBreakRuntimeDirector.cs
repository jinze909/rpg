using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Live runtime director strictly subordinate to ORK 3 that automatically
    /// attaches CombatLockBreakHudView and OrkCombatLockBreakAdapter during
    /// active ORK battles (S00 Whitewater Crossing bridgehead defense and
    /// Soundwell acoustic encounters), then disposes them cleanly when battle
    /// ends. Owns zero HP, damage, turn, or defeat authority and never adds
    /// a GraphicRaycaster or intercepts UGUI input.
    /// </summary>
    public sealed class CombatLockBreakRuntimeDirector : MonoBehaviour
    {
        private static CombatLockBreakRuntimeDirector instance;

        private CombatLockBreakHudView hudView;
        private OrkCombatLockBreakAdapter adapter;
        private BattleContextMode activeMode;
        private bool battleAttached;
        private float lastAspect = -1f;

        public static CombatLockBreakRuntimeDirector Instance => instance;
        public bool IsBattleAttached => battleAttached;
        public BattleContextMode ActiveMode => activeMode;
        public CombatLockBreakHudView HudView => hudView;
        public OrkCombatLockBreakAdapter Adapter => adapter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDirector()
        {
            if (!Application.isPlaying || instance != null)
                return;
            var go = new GameObject("CombatLockBreakRuntimeDirector");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CombatLockBreakRuntimeDirector>();
        }

        public static BattleContextMode InferBattleModeFromSnapshots(
            OrkPartyBridge.BattleUnitSnapshot[] snapshots, string activeSceneName = null)
        {
            int allyCount = 0;
            bool enemyLooksLikeWhitewater = false;
            bool enemyLooksLikeBeast = false;

            if (snapshots != null)
            {
                for (int i = 0; i < snapshots.Length; i++)
                {
                    var unit = snapshots[i];
                    if (!unit.IsEnemy)
                    {
                        allyCount++;
                        continue;
                    }

                    string name = unit.Name ?? string.Empty;
                    if (name.IndexOf("beast", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("借声", StringComparison.Ordinal) >= 0)
                    {
                        enemyLooksLikeBeast = true;
                    }
                    else if (name.IndexOf("spear", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("sapper", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("矛", StringComparison.Ordinal) >= 0 ||
                        name.IndexOf("工兵", StringComparison.Ordinal) >= 0)
                    {
                        enemyLooksLikeWhitewater = true;
                    }
                }
            }

            bool sceneLooksLikeWhitewater = !string.IsNullOrEmpty(activeSceneName) &&
                (activeSceneName.IndexOf("whitewater", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 activeSceneName.IndexOf("s00", StringComparison.OrdinalIgnoreCase) >= 0);

            if (enemyLooksLikeBeast)
                return BattleContextMode.SoundwellParty;
            if (enemyLooksLikeWhitewater || sceneLooksLikeWhitewater)
                return BattleClashVfxDirector.DetectContext(whitewaterActive: true, allyCount: allyCount);

            return allyCount <= 1
                ? BattleContextMode.WhitewaterSolo
                : BattleContextMode.SoundwellParty;
        }

        public void AttachForBattle(BattleContextMode mode, int startingMomentum = 1)
        {
            DetachBattle();

            activeMode = mode;
            if (mode == BattleContextMode.SoundwellParty)
            {
                var tracker = new SoundwellAcousticBreakTracker(startingMomentum);
                hudView = new CombatLockBreakHudView(transform, tracker);
                adapter = new OrkCombatLockBreakAdapter(tracker, hudView);
            }
            else
            {
                var tracker = new S00BridgeheadCableTracker(startingMomentum);
                hudView = new CombatLockBreakHudView(transform, tracker);
                adapter = new OrkCombatLockBreakAdapter(tracker, hudView);
            }

            battleAttached = true;
            lastAspect = -1f;
            SyncViewportAspect();
        }

        public bool ObserveSnapshots(OrkPartyBridge.BattleUnitSnapshot[] snapshots)
        {
            if (!battleAttached || adapter == null)
                return false;
            return adapter.ObserveOrkSnapshots(snapshots);
        }

        public bool ObserveSoundwellAcousticAction(CombatLockKind action, int momentumCost = 0)
        {
            if (!battleAttached || adapter == null)
                return false;
            return adapter.ObserveSoundwellAcousticAction(action, momentumCost);
        }

        public void DetachBattle()
        {
            battleAttached = false;
            adapter = null;
            if (hudView != null)
            {
                hudView.Dispose();
                hudView = null;
            }
            lastAspect = -1f;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }
            instance = this;
        }

        private void LateUpdate()
        {
            OrkPartyBridge party = FindFirstObjectByType<OrkPartyBridge>();
            bool active = party != null && party.BattleInProgress && party.RoleEncounter == null;
            if (!active)
            {
                if (battleAttached)
                    DetachBattle();
                return;
            }

            OrkPartyBridge.BattleUnitSnapshot[] snapshots = party.CaptureBattleSnapshots();
            if (!battleAttached)
            {
                string sceneName = SceneManager.GetActiveScene().name;
                BattleContextMode mode = InferBattleModeFromSnapshots(snapshots, sceneName);
                AttachForBattle(mode);
            }

            SyncViewportAspect();
            adapter?.ObserveOrkSnapshots(snapshots);
        }

        private void SyncViewportAspect()
        {
            if (hudView == null) return;
            float aspect = (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
            if (Mathf.Abs(aspect - lastAspect) > 0.005f)
            {
                lastAspect = aspect;
                hudView.LayoutForViewportAspect(aspect);
            }
        }

        private void OnDestroy()
        {
            DetachBattle();
            if (instance == this)
                instance = null;
        }
    }
}
