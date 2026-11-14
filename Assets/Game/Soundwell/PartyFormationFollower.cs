using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>Follower trajectory sampling, formation placement and presentation; no ORK dependency.</summary>
    public sealed class PartyFormationFollower : MonoBehaviour
    {
        private struct Sample { public float At; public Vector3 Position; }
        private static readonly string[] IdlePixels =
        {
            "................",
            ".....######.....",
            ".....#++++#.....",
            ".....#++++#.....",
            ".....#++++#.....",
            "......####......",
            "...##########...",
            "...#+######+#...",
            "...#+######+#...",
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
            "...###....##....",
            "..###......##...",
            "..##........##..",
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
            "...#############",
            "...#+######++++#",
            "...#+######++++#",
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
            "...##+####+##...",
            "...##+####+##...",
            "...##########...",
            "....#######.....",
            "....###..##.....",
            "....###..##.....",
            "...####..###....",
            "...###....###...",
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
            "...###...##.....",
            "..###....###....",
            ".###......###...",
            ".##........##...",
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
            "...#############",
            "...##+####++++##",
            "...##+####++++##",
            "...##########...",
            "....#######.....",
            "....###..##.....",
            "....###..##.....",
            "...####..###....",
            "...###....###...",
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
        private readonly GameObject[] actors = new GameObject[3];
        private readonly List<Sample> history = new List<Sample>(32);
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
        private Transform parent;
        private SoundwellPartyConfig config;
        private bool ready;

        public void Configure(Transform partyLeader, Transform partyParent, SoundwellPartyConfig settings)
        {
            leader = partyLeader;
            parent = partyParent;
            config = settings;
            history.Clear();
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
            actor.transform.position = leader.position + (Vector3)offset;
            actor.transform.localScale = Vector3.one;
            var renderer = actor.AddComponent<SpriteRenderer>();
            renderer.sprite = index == 2 ? hoodIdleSprite : placeholderSprite;
            renderer.color = tint;
            renderer.sortingOrder = 3;
            actors[index] = actor;
            return actor;
        }

        public void SetFrameworkReady() => ready = true;

        private void RecordLeader()
        {
            if (leader == null) return;
            history.Add(new Sample { At = Time.time, Position = leader.position });
            while (history.Count > 32 ||
                (history.Count > 2 && history[1].At < Time.time - 0.7f))
                history.RemoveAt(0);
        }

        private Vector3 FollowSample()
        {
            float desired = Time.time - config.FollowerLagSeconds;
            for (int i = history.Count - 1; i > 0; i--)
            {
                Sample before = history[i - 1], after = history[i];
                if (before.At <= desired)
                {
                    float span = after.At - before.At;
                    return span <= 0f ? after.Position :
                        Vector3.Lerp(before.Position, after.Position,
                            Mathf.Clamp01((desired - before.At) / span));
                }
            }
            return history.Count == 0 ? leader.position : history[0].Position;
        }

        public void RenderFollowers(bool inBattle, bool secondDefending, bool thirdDefending)
        {
            if (!ready || leader == null || config == null) return;
            RecordLeader();
            Vector3 pathPosition = FollowSample();
            for (int i = 1; i < actors.Length; i++)
            {
                GameObject actor = actors[i];
                if (actor == null) continue;
                SpriteRenderer renderer = actor.GetComponent<SpriteRenderer>();
                if (renderer == null) continue;
                Sprite idle = i == 2 ? hoodIdleSprite : placeholderSprite;
                Sprite walking = i == 2 ? hoodWalkingSprite : walkingSprite;
                if (inBattle)
                {
                    bool defending = i == 2 ? thirdDefending : secondDefending;
                    renderer.sprite = Time.unscaledTime < companionAttackUntil[i]
                        ? (i == 2 ? hoodAttackSprite : attackSprite)
                        : (defending || Time.unscaledTime < companionGuardUntil[i])
                            ? (i == 2 ? hoodGuardSprite : guardSprite)
                            : Time.unscaledTime < companionStepUntil[i] ? walking : idle;
                    continue;
                }
                Vector3 goal = pathPosition + (Vector3)(i == 2
                    ? config.ThirdFollowerOffset : config.SecondFollowerOffset);
                goal.z = 0f;
                Vector3 beforePosition = actor.transform.position;
                actor.transform.position = Vector3.MoveTowards(beforePosition, goal,
                    config.FormationFollowSpeed * Time.deltaTime);
                Vector3 delta = actor.transform.position - beforePosition;
                if (Mathf.Abs(delta.x) > 0.001f)
                    renderer.flipX = delta.x < 0f;
                renderer.sprite = delta.sqrMagnitude > 0.000001f &&
                    Mathf.FloorToInt(Time.time / config.WalkFrameSeconds) % 2 == 1
                        ? walking : idle;
            }
        }

        public void ShowGuardPose(int index, Transform threat, bool battleStarted)
        {
            if (index <= 0 || index >= actors.Length || actors[index] == null) return;
            companionGuardUntil[index] = Time.unscaledTime + CompanionGuardPoseSeconds;
            // A newly accepted ORK Guard must win over the previous action's
            // one-shot pose on the very next render, not briefly flash Attack
            // or Step again. ORK alone still owns the Defending flag.
            companionAttackUntil[index] = 0f;
            companionStepUntil[index] = 0f;
            if (!battleStarted) return;
            SpriteRenderer renderer = actors[index].GetComponent<SpriteRenderer>();
            if (renderer == null) return;
            renderer.sprite = index == 2 ? hoodGuardSprite : guardSprite;
            if (threat == null) return;
            float dx = threat.position.x - actors[index].transform.position.x;
            if (Mathf.Abs(dx) > 0.01f) renderer.flipX = dx > 0f;
        }

        public void ShowAttackPose(int index, Transform threat)
        {
            if (index <= 0 || index >= actors.Length || actors[index] == null) return;
            companionAttackUntil[index] = Time.unscaledTime + CompanionAttackPoseSeconds;
            // Remove the old Guard/Step presentation window when a different
            // real action is accepted. Do not resurrect it after Attack fades.
            companionGuardUntil[index] = 0f;
            companionStepUntil[index] = 0f;
            SpriteRenderer renderer = actors[index].GetComponent<SpriteRenderer>();
            if (renderer == null) return;
            renderer.sprite = index == 2 ? hoodAttackSprite : attackSprite;
            if (threat == null) return;
            float dx = threat.position.x - actors[index].transform.position.x;
            if (Mathf.Abs(dx) > 0.01f) renderer.flipX = dx < 0f;
        }

        public void ShowStepPose(int index, Vector2 direction)
        {
            if (index <= 0 || index >= actors.Length || actors[index] == null) return;
            companionStepUntil[index] = Time.unscaledTime + CompanionStepPoseSeconds;
            // ORK movement is a new accepted action and must not replay the
            // previous turn's attack or timed guard art when it is rendered.
            companionAttackUntil[index] = 0f;
            companionGuardUntil[index] = 0f;
            SpriteRenderer renderer = actors[index].GetComponent<SpriteRenderer>();
            if (renderer == null) return;
            renderer.sprite = index == 2 ? hoodWalkingSprite : walkingSprite;
            if (Mathf.Abs(direction.x) > 0.01f) renderer.flipX = direction.x < 0f;
        }

        public void ResetPoses()
        {
            for (int i = 1; i < actors.Length; i++)
                companionStepUntil[i] = companionAttackUntil[i] = companionGuardUntil[i] = 0f;
        }

        // A real 16-PPU, point-filtered pixel silhouette, not a stretched
        // white 1x1 texture. Tint is applied by the SpriteRenderer, so both
        // rescue followers share one owned sprite and texture per scene.
        private static Sprite CreatePixelRescuerSprite(
            string[] rows, string name, out Texture2D texture)
        {
            const int size = 16;
            if (rows.Length != size)
                throw new System.InvalidOperationException("Pixel rescuer requires 16 rows.");

            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                if (rows[size - 1 - y].Length != size)
                    throw new System.InvalidOperationException("Pixel rescuer row must be 16 pixels.");
                for (int x = 0; x < size; x++)
                {
                    char part = rows[size - 1 - y][x];
                    pixels[y * size + x] = part == '#'
                        ? new Color32(255, 255, 255, 255)
                        : part == '+'
                            ? new Color32(176, 176, 176, 255)
                            : new Color32(0, 0, 0, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture,
                new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                16f, 0, SpriteMeshType.FullRect);
            // Texture.name does not propagate to Sprite.Create. Keep each
            // visible pose identifiable for the guard-action regression and
            // for artifact inspection without changing ORK battle state.
            sprite.name = name;
            return sprite;
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
