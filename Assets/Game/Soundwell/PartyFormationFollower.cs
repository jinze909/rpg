using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>Follower trajectory sampling, formation placement and presentation; no ORK dependency.</summary>
    public sealed class PartyFormationFollower : MonoBehaviour
    {
        private struct Sample { public float At; public Vector3 Position; }
        // Exploration movement and trailing samples freeze with gameplay.
        // ORK may pause Time.timeScale while selecting a battle action, but a
        // brief hit/attack/guard/step visual must still return to its idle pose.
        // Only the pose clock is unscaled; actual ORK turns stay in ORK.
        private static float FormationClock => Time.time;
        private static float CombatVisualClock => Time.unscaledTime;
        private static readonly string[] IdlePixels =
        {
            "................",
            ".....######.....",
            ".....#++++#.....",
            ".....#++++#.....",
            ".....#++++#.....",
            "......####......",
            "...##########...",
            "..#+#######+#...",
            "..#+#######+#...",
            ".....######.....",
            ".....######.....",
            ".....##..##.....",
            ".....##..##.....",
            "....###..###....",
            "....###..###....",
            "................",
        };

        private static readonly string[] WalkPixels =
        {
            "................",
            ".....######.....",
            ".....#++++#.....",
            ".....#++++#.....",
            ".....#++++#.....",
            "......####......",
            "...##########...",
            "..###+######....",
            "..###+######....",
            ".....######.....",
            ".....######.....",
            "....###..##.....",
            "...###...##.....",
            "..###.....##....",
            "..##.......##...",
            "................",
        };
        // Distinct 16-pixel attack silhouette: a forward-reaching arm, not
        // a stretched rectangle or a gameplay-position change.
        private static readonly string[] AttackPixels =
        {
            "................",
            ".....######.....",
            ".....#++++#.....",
            ".....#++++#.....",
            ".....#++++#.....",
            "......####......",
            "...############.",
            "..#+######++++#.",
            "..#+######++++#.",
            ".....######.....",
            ".....######.....",
            ".....##..##.....",
            ".....##..##.....",
            "....###..###....",
            "....###..###....",
            "................",
        };
        // Rescuer 3's hood and asymmetric kit remain visible even with
        // identical team colors. Three authored strict 16x16 poses match the
        // existing idle/walk/attack cadence without changing ORK abilities.
        private static readonly string[] HoodIdlePixels =
        {
            "................",
            "....########....",
            "....##++++##....",
            "....##++++##....",
            "....###++###....",
            ".....######.....",
            "...##########...",
            "..##+#####+##...",
            "..##+#####+##...",
            "...##########...",
            "....#######.....",
            ".....###.##.....",
            ".....###.##.....",
            "....####.###....",
            "....###...###...",
            "................",
        };
        private static readonly string[] HoodWalkPixels =
        {
            "................",
            "....########....",
            "....##++++##....",
            "....##++++##....",
            "....###++###....",
            ".....######.....",
            "...##########...",
            "..###+####+##...",
            "..###+####+##...",
            "...##########...",
            "....#######.....",
            "....###..##.....",
            "...###...###....",
            "..###.....###...",
            "..##.......##...",
            "................",
        };
        private static readonly string[] HoodAttackPixels =
        {
            "................",
            "....########....",
            "....##++++##....",
            "....##++++##....",
            "....###++###....",
            ".....######.....",
            "...############.",
            "..##+#####++++#.",
            "..##+#####++++#.",
            "...##########...",
            "....#######.....",
            ".....###.##.....",
            ".....###.##.....",
            "....####.###....",
            "....###...###...",
            "................",
        };
        // Retain the distinct hood and torso of each rescuer while raising a
        // pixel-size shield. Only ORK's live Battle.Defending selects this pose.
        // No separate combat state or additional controller is introduced.
        private static string[] WithGuardShield(string[] idleRows)
        {
            var guarded = (string[])idleRows.Clone();
            for (int y = 6; y <= 10; y++)
            {
                char[] pixels = guarded[y].ToCharArray();
                pixels[0] = y == 6 || y == 10 ? '.' : '#';
                pixels[1] = '#';
                pixels[2] = y >= 7 && y <= 9 ? '+' : '#';
                pixels[3] = '#';
                guarded[y] = new string(pixels);
            }
            return guarded;
        }

        private const float CompanionAttackPoseSeconds = 0.30f;
        private const float CompanionGuardPoseSeconds = 0.80f;
        private const float CompanionStepPoseSeconds = 0.38f;
        private readonly float[] companionStepUntil = new float[3];
        private readonly float[] companionAttackUntil = new float[3];
        private readonly float[] companionGuardUntil = new float[3];
        // Read-only ORK HP snapshots exist only to drive a short damage flash.
        // They cannot change combatant health, turns or quest progression.
        private readonly float?[] lastCompanionHp = new float?[3];
        private readonly float[] companionHitUntil = new float[3];
        private readonly Color[] companionTints = new Color[3];
        private static readonly Color CompanionHitTint = new Color(1f, 0.43f, 0.36f);
        private const float CompanionHitSeconds = 0.30f;
        private readonly GameObject[] actors = new GameObject[3];
        // Each follower's SpriteRenderer is stable after creation, so the
        // per-frame formation render never re-resolves its component.
        private readonly SpriteRenderer[] followerRenderers = new SpriteRenderer[3];
        // Fixed-capacity ring: oldest samples are evicted in O(1), avoiding
        // per-frame List.RemoveAt(0) shifts during follower rendering.
        private readonly Sample[] history = new Sample[32];
        private int historyStart;
        private int historyCount;

        private void ClearHistory() => historyStart = historyCount = 0;

        private Sample HistoryAt(int index) =>
            history[(historyStart + index) % history.Length];

        private void AddHistory(Sample sample)
        {
            int slot = (historyStart + historyCount) % history.Length;
            history[slot] = sample;
            if (historyCount < history.Length) historyCount++;
            else historyStart = (historyStart + 1) % history.Length;
        }

        private void DropOldestHistory()
        {
            if (historyCount <= 0) return;
            historyStart = (historyStart + 1) % history.Length;
            historyCount--;
        }
        private bool checkpointRegroupPending;
        private Vector3 checkpointAnchor;
        private Sprite placeholderSprite;
        private Texture2D placeholderTexture;
        private Sprite walkingSprite;
        private Texture2D walkingTexture;
        private Sprite attackSprite;
        private Texture2D attackTexture;
        private Sprite guardSprite;
        private Texture2D guardTexture;
        private Sprite hoodIdleSprite;
        private Texture2D hoodIdleTexture;
        private Sprite hoodWalkingSprite;
        private Texture2D hoodWalkingTexture;
        private Sprite hoodAttackSprite;
        private Texture2D hoodAttackTexture;
        private Sprite hoodGuardSprite;
        private Texture2D hoodGuardTexture;

        private Transform leader;
        // Rigidbody2D.position is the gameplay position during an immediate
        // checkpoint teleport. Transform.position can still reflect the prior
        // physics step when LateUpdate samples the following formation.
        private Rigidbody2D leaderBody;
        private Transform cachedThreat;
        private Rigidbody2D cachedThreatBody;
        private Transform parent;
        private SoundwellPartyConfig config;
        private bool ready;

        public void Configure(Transform partyLeader, Transform partyParent, SoundwellPartyConfig settings)
        {
            leader = partyLeader;
            leaderBody = partyLeader != null ? partyLeader.GetComponent<Rigidbody2D>() : null;
            cachedThreat = null;
            cachedThreatBody = null;
            parent = partyParent;
            config = settings;
            ClearHistory();
            for (int i = 0; i < followerRenderers.Length; i++)
                followerRenderers[i] = null;
            RecordLeader();
            placeholderSprite = CreatePixelRescuerSprite(
                IdlePixels, "PLACEHOLDER 16px Soundwell rescuer idle", out placeholderTexture);
            walkingSprite = CreatePixelRescuerSprite(
                WalkPixels, "PLACEHOLDER 16px Soundwell rescuer walking", out walkingTexture);
            attackSprite = CreatePixelRescuerSprite(
                AttackPixels, "PLACEHOLDER 16px Soundwell rescuer attack", out attackTexture);
            guardSprite = CreatePixelRescuerSprite(
                WithGuardShield(IdlePixels), "PLACEHOLDER 16px rescuer guard shield", out guardTexture);
            hoodIdleSprite = CreatePixelRescuerSprite(
                HoodIdlePixels, "PLACEHOLDER 16px hooded rescuer idle", out hoodIdleTexture);
            hoodWalkingSprite = CreatePixelRescuerSprite(
                HoodWalkPixels, "PLACEHOLDER 16px hooded rescuer walking", out hoodWalkingTexture);
            hoodAttackSprite = CreatePixelRescuerSprite(
                HoodAttackPixels, "PLACEHOLDER 16px hooded rescuer attack", out hoodAttackTexture);
            hoodGuardSprite = CreatePixelRescuerSprite(
                WithGuardShield(HoodIdlePixels), "PLACEHOLDER 16px hooded rescuer guard shield",
                out hoodGuardTexture);
        }

        public GameObject CreateFollower(int index, string name, Vector2 offset, Color tint)
        {
            GameObject actor = new GameObject(name);
            actor.SetActive(false);
            actor.transform.SetParent(parent, true);
            actor.transform.position = LeaderPosition + WorldFormationOffset(offset);
            actor.transform.localScale = Vector3.one;
            var renderer = actor.AddComponent<SpriteRenderer>();
            renderer.sprite = index == 2 ? hoodIdleSprite : placeholderSprite;
            renderer.color = tint;
            companionTints[index] = tint;
            renderer.sortingOrder = 3;
            actors[index] = actor;
            followerRenderers[index] = renderer;
            return actor;
        }

        public void SetFrameworkReady() => ready = true;

        // The stored formation offsets are authored in the trial's local
        // plane. Convert only the offset vector (never the player position)
        // so translated/rotated/scaled rooms keep companions on the right
        // visible side without changing ORK action ownership.
        private Vector3 WorldFormationOffset(Vector2 localOffset) =>
            parent != null ? parent.TransformVector(localOffset) : (Vector3)localOffset;

        private Vector3 LeaderPosition
        {
            get
            {
                if (leaderBody == null)
                    return leader.position;
                Vector2 position = leaderBody.position;
                return new Vector3(position.x, position.y, leader.position.z);
            }
        }
        // Real beast contact can relocate the leader away and back between
        // two LateUpdates. Only the checkpoint service knows this occurred;
        // comparing consecutive sampled positions cannot detect it.
        // This is exploration presentation, never a second combat action.
        public void RegroupAfterCheckpoint(Vector2 checkpoint)
        {
            if (!ready || leader == null || config == null)
                return;
            checkpointAnchor = new Vector3(checkpoint.x, checkpoint.y, 0f);
            checkpointRegroupPending = true;
            ClearHistory();
            AddHistory(new Sample { At = FormationClock, Position = checkpointAnchor });
            for (int i = 1; i < actors.Length; i++)
            {
                if (actors[i] == null)
                    continue;
                actors[i].transform.position = checkpointAnchor + WorldFormationOffset(
                    i == 2 ? config.ThirdFollowerOffset : config.SecondFollowerOffset);
                var renderer = followerRenderers[i];
                if (renderer != null)
                    renderer.sprite = i == 2 ? hoodIdleSprite : placeholderSprite;
            }
        }

        // A checkpoint knockback or scene-owned relocation is not a walk
        // across the arena. Never make the two visible rescuers traverse the
        // obsolete path while the real leader is already back at safety.
        private const float LeaderRelocationThreshold = 3f;

        private bool RecordLeader()
        {
            if (leader == null) return false;
            Vector3 position = LeaderPosition;
            bool relocated = historyCount > 0 &&
                Vector3.Distance(HistoryAt(historyCount - 1).Position, position) >
                LeaderRelocationThreshold;
            if (relocated)
                ClearHistory();
            AddHistory(new Sample { At = FormationClock, Position = position });
            while (historyCount > 2 && HistoryAt(1).At < FormationClock - 0.7f)
                DropOldestHistory();
            return relocated;
        }

        private Vector3 FollowSample()
        {
            float desired = FormationClock - config.FollowerLagSeconds;
            for (int i = historyCount - 1; i > 0; i--)
            {
                Sample before = HistoryAt(i - 1), after = HistoryAt(i);
                if (before.At <= desired)
                {
                    float span = after.At - before.At;
                    return span <= 0f ? after.Position :
                        Vector3.Lerp(before.Position, after.Position,
                            Mathf.Clamp01((desired - before.At) / span));
                }
            }
            return historyCount == 0 ? LeaderPosition : HistoryAt(0).Position;
        }

        public void RefreshCompanionHealth(int index, float hp, bool battleActive)
        {
            if (index <= 0 || index >= actors.Length)
                return;
            if (!battleActive || float.IsNaN(hp) || float.IsInfinity(hp))
            {
                lastCompanionHp[index] = null;
                companionHitUntil[index] = float.NegativeInfinity;
                return;
            }
            // The first live snapshot establishes a baseline, not a hit.
            if (lastCompanionHp[index].HasValue &&
                hp < lastCompanionHp[index].Value - 0.001f)
                companionHitUntil[index] = CombatVisualClock + CompanionHitSeconds;
            lastCompanionHp[index] = hp;
        }

        public void RenderFollowers(bool inBattle, bool secondDefending, bool thirdDefending)
        {
            if (!ready || leader == null || config == null) return;
            // Preserve the direct checkpoint signal through the next
            // follower render even if no discontinuity was sampled.
            bool checkpointRegroup = checkpointRegroupPending && !inBattle;
            bool leaderRelocated = checkpointRegroup || RecordLeader();
            Vector3 pathPosition = checkpointRegroup ? checkpointAnchor : FollowSample();
            for (int i = 1; i < actors.Length; i++)
            {
                GameObject actor = actors[i];
                if (actor == null) continue;
                SpriteRenderer renderer = followerRenderers[i];
                if (renderer == null) continue;
                Sprite idle = i == 2 ? hoodIdleSprite : placeholderSprite;
                Sprite walking = i == 2 ? hoodWalkingSprite : walkingSprite;
                // Reuse the real ORK actor's existing sprite; a flash never
                // duplicates the combatant or modifies its authoritative HP.
                renderer.color = inBattle && CombatVisualClock < companionHitUntil[i]
                    ? CompanionHitTint : companionTints[i];
                if (inBattle)
                {
                    bool defending = i == 2 ? thirdDefending : secondDefending;
                    renderer.sprite = CombatVisualClock < companionAttackUntil[i]
                        ? (i == 2 ? hoodAttackSprite : attackSprite)
                        : (defending || CombatVisualClock < companionGuardUntil[i])
                            ? (i == 2 ? hoodGuardSprite : guardSprite)
                            : CombatVisualClock < companionStepUntil[i] ? walking : idle;
                    continue;
                }
                Vector3 goal = pathPosition + WorldFormationOffset(i == 2
                    ? config.ThirdFollowerOffset : config.SecondFollowerOffset);
                goal.z = 0f;
                if (leaderRelocated)
                {
                    // Only exploration visuals may regroup immediately. ORK
                    // alone positions the actual combatants during a battle.
                    actor.transform.position = goal;
                    renderer.sprite = idle;
                    continue;
                }
                Vector3 beforePosition = actor.transform.position;
                actor.transform.position = Vector3.MoveTowards(beforePosition, goal,
                    config.FormationFollowSpeed * Time.deltaTime);
                Vector3 delta = actor.transform.position - beforePosition;
                if (Mathf.Abs(delta.x) > 0.001f)
                    renderer.flipX = delta.x < 0f;
                renderer.sprite = delta.sqrMagnitude > 0.000001f &&
                    Mathf.FloorToInt(FormationClock / config.WalkFrameSeconds) % 2 == 1
                        ? walking : idle;
            }
            if (checkpointRegroup)
                checkpointRegroupPending = false;
        }

        private SpriteRenderer FollowerRenderer(int index)
        {
            SpriteRenderer renderer = followerRenderers[index];
            if (renderer == null && actors[index] != null)
            {
                renderer = actors[index].GetComponent<SpriteRenderer>();
                followerRenderers[index] = renderer;
            }
            return renderer;
        }

        public void ShowGuardPose(int index, Transform threat, bool battleStarted)
        {
            if (index <= 0 || index >= actors.Length || actors[index] == null) return;
            companionGuardUntil[index] = CombatVisualClock + CompanionGuardPoseSeconds;
            // A newly accepted ORK Guard must win over the previous action's
            // one-shot pose on the very next render, not briefly flash Attack
            // or Step again. ORK alone still owns the Defending flag.
            companionAttackUntil[index] = 0f;
            companionStepUntil[index] = 0f;
            if (!battleStarted) return;
            SpriteRenderer renderer = FollowerRenderer(index);
            if (renderer == null) return;
            renderer.sprite = index == 2 ? hoodGuardSprite : guardSprite;
            if (threat == null) return;
            // ORK can advance the beast's physics body before its rendered
            // Transform catches up. Face the actual encounter target, not
            // the stale sprite location after a scene-owned instant move.
            float dx = ThreatPhysicsX(threat) - actors[index].transform.position.x;
            if (Mathf.Abs(dx) > 0.01f) renderer.flipX = dx > 0f;
        }

        public void ShowAttackPose(int index, Transform threat)
        {
            if (index <= 0 || index >= actors.Length || actors[index] == null) return;
            companionAttackUntil[index] = CombatVisualClock + CompanionAttackPoseSeconds;
            // Remove the old Guard/Step presentation window when a different
            // real action is accepted. Do not resurrect it after Attack fades.
            companionGuardUntil[index] = 0f;
            companionStepUntil[index] = 0f;
            SpriteRenderer renderer = FollowerRenderer(index);
            if (renderer == null) return;
            renderer.sprite = index == 2 ? hoodAttackSprite : attackSprite;
            if (threat == null) return;
            float dx = ThreatPhysicsX(threat) - actors[index].transform.position.x;
            if (Mathf.Abs(dx) > 0.01f) renderer.flipX = dx < 0f;
        }

        private float ThreatPhysicsX(Transform target)
        {
            if (target != cachedThreat)
            {
                cachedThreat = target;
                cachedThreatBody = target != null ? target.GetComponent<Rigidbody2D>() : null;
            }
            return cachedThreatBody != null ? cachedThreatBody.position.x : target.position.x;
        }

        public void ShowStepPose(int index, Vector2 direction)
        {
            if (index <= 0 || index >= actors.Length || actors[index] == null) return;
            companionStepUntil[index] = CombatVisualClock + CompanionStepPoseSeconds;
            // ORK movement is a new accepted action and must not replay the
            // previous turn's attack or timed guard art when it is rendered.
            companionAttackUntil[index] = 0f;
            companionGuardUntil[index] = 0f;
            SpriteRenderer renderer = FollowerRenderer(index);
            if (renderer == null) return;
            renderer.sprite = index == 2 ? hoodWalkingSprite : walkingSprite;
            if (Mathf.Abs(direction.x) > 0.01f) renderer.flipX = direction.x < 0f;
        }

        public void ResetPoses()
        {
            cachedThreat = null;
            cachedThreatBody = null;
            for (int i = 1; i < actors.Length; i++)
            {
                companionStepUntil[i] = companionAttackUntil[i] = companionGuardUntil[i] = 0f;
                lastCompanionHp[i] = null;
                companionHitUntil[i] = float.NegativeInfinity;
                SpriteRenderer visual = FollowerRenderer(i);
                if (visual != null)
                    visual.color = companionTints[i];
            }
        }

        // A real 16-PPU, point-filtered pixel silhouette, not a stretched
        // white 1x1 texture. Tint is applied by the SpriteRenderer, so both
        // rescue followers share one owned sprite and texture per scene.
        private static Sprite CreatePixelRescuerSprite(
            string[] rows, string name, out Texture2D texture)
        {
            return SoundwellPixelSpriteBuilder.Create16(rows, name, out texture);
        }

        private void OnDestroy()
        {
            if (placeholderSprite != null)
                Destroy(placeholderSprite);
            if (placeholderTexture != null)
                Destroy(placeholderTexture);
            if (walkingSprite != null)
                Destroy(walkingSprite);
            if (walkingTexture != null)
                Destroy(walkingTexture);
            if (attackSprite != null)
                Destroy(attackSprite);
            if (attackTexture != null)
                Destroy(attackTexture);
            if (guardSprite != null)
                Destroy(guardSprite);
            if (guardTexture != null)
                Destroy(guardTexture);
            if (hoodIdleSprite != null)
                Destroy(hoodIdleSprite);
            if (hoodIdleTexture != null)
                Destroy(hoodIdleTexture);
            if (hoodWalkingSprite != null)
                Destroy(hoodWalkingSprite);
            if (hoodWalkingTexture != null)
                Destroy(hoodWalkingTexture);
            if (hoodAttackSprite != null)
                Destroy(hoodAttackSprite);
            if (hoodAttackTexture != null)
                Destroy(hoodAttackTexture);
            if (hoodGuardSprite != null)
                Destroy(hoodGuardSprite);
            if (hoodGuardTexture != null)
                Destroy(hoodGuardTexture);
        }
    }
}
