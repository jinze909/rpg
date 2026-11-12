using System.Collections;
using System.Collections.Generic;
using System.Text;
using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using GamingIsLove.ORKFramework.Components;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Builds the actual three-member ORK party for the Soundwell scene.
    /// The leader remains the exploration controller; the two visible
    /// placeholders follow in formation and all three belong to one ORK group.
    /// </summary>
    // Suppress ORK's legacy 3D ButtonPlayerController before its Update; the
    // SoundwellTrial/Input System already owns 2D Rigidbody2D movement.
    [DefaultExecutionOrder(-10000)]
    public sealed class SoundwellOrkParty : MonoBehaviour
    {
        private static readonly Vector2[] FollowOffsets =
        {
            new Vector2(-0.45f, -0.48f),
            new Vector2(-0.45f, 0.48f)
        };

        private const float FollowSpeed = 4.5f;
        private const float WalkFrameSeconds = 0.15f;

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
        private const float CompanionAttackPoseSeconds = 0.30f;
        private readonly float[] companionAttackUntil = new float[3];
        private const float FrameworkTimeout = 30f;
        private const float RegistrationTimeout = 8f;

        [SerializeField] private Transform leader;
        [SerializeField] private Transform beast;
        [SerializeField] private FactionAsset playerFaction;
        [SerializeField] private FactionAsset enemyFaction;
        [SerializeField] private CombatantAsset defaultCombatant;
        [SerializeField] private BattleSystemAsset turnBasedBattleSystem;

        private readonly List<GameObject> actors = new List<GameObject>(3);
        private readonly List<Combatant> members = new List<Combatant>(3);
        private Sprite placeholderSprite;
        private Texture2D placeholderTexture;
        private Sprite walkingSprite;
        private Texture2D walkingTexture;
        private Sprite attackSprite;
        private Texture2D attackTexture;
        private bool battleStarted;
        // The party remains ORK-owned for battles, but its exploration input is
        // exclusively handled by the saved Soundwell 2D Input System adapter.
        private bool playerAssignedToOrk;
        private bool battleObservedActive;
        private bool battleEndRequested;
        private float battleStartedAt;
        private const float BattleStartTimeout = 8f;

        public bool IsReady { get; private set; }
        public string InitializationError { get; private set; }
        public Group PlayerGroup { get; private set; }
        public Group EnemyGroup { get; private set; }
        public Combatant Beast { get; private set; }
        public Transform Leader => leader;
        public IReadOnlyList<GameObject> Actors => actors;
        public IReadOnlyList<Combatant> Members => members;
        public BattleSystemAsset TurnBasedAsset => turnBasedBattleSystem;
        public bool BattleStarted => battleStarted;
        public bool BattleInProgress => battleStarted && IsBattleRunning();
        public bool EncounterStartFailed => battleStarted && !battleObservedActive &&
            Time.realtimeSinceStartup - battleStartedAt >= BattleStartTimeout;
        public bool EncounterWon => battleStarted && battleObservedActive && !IsBattleRunning() &&
            Beast != null && Beast.Status.IsDead;
        public bool EncounterFailed => battleStarted && battleObservedActive && !IsBattleRunning() &&
            Beast != null && !Beast.Status.IsDead;

        private void Start()
        {
            StartCoroutine(Initialize());
        }

        private IEnumerator Initialize()
        {
            if (leader == null || beast == null || playerFaction == null ||
                enemyFaction == null || defaultCombatant == null || turnBasedBattleSystem == null)
            {
                Fail("The Soundwell scene is missing a serialized ORK party asset or actor reference.");
                yield break;
            }

            float frameworkDeadline = Time.realtimeSinceStartup + FrameworkTimeout;
            while (!ORK.Initialized && Time.realtimeSinceStartup < frameworkDeadline)
                yield return null;

            if (!ORK.Initialized)
            {
                Fail("ORK did not initialize before the Soundwell party timeout.");
                yield break;
            }

            if (ORK.Factions.Get(0) == null || ORK.Factions.Get(1) == null ||
                turnBasedBattleSystem.Settings == null)
            {
                Fail("The saved player/enemy factions or Turn Based settings are unavailable.");
                yield break;
            }

            // ORK's installed default ability asset has an AbilityLevel 1
            // without an ActiveAbility payload. A base-attack shortcut alone
            // is not an attack: CanChoose/CanUse always return false when its
            // active level is null (confirmed by Unity run 43050554632).
            // Configure the missing active definition on ORK's own ability,
            // before any Soundwell combatants are registered. The real ORK
            // ability and action resolver still own targeting, costs, turns,
            // damage and victory; this never writes HP or fabricates an action.
            Ability soundwellAttack = ORK.Abilities.Get(0);
            AbilityLevel attackLevel = soundwellAttack != null
                ? soundwellAttack.GetLevel(1) : null;
            if (attackLevel == null)
            {
                Fail("Soundwell ORK base attack is missing its saved level 1.");
                yield break;
            }
            if (attackLevel.active == null)
                attackLevel.active = new ActiveAbility();
            // The vendor starter battle system can route actions through
            // system-wide schematics that never calculate their outcomes.
            // Override those for this one unanimated test attack: ORK's
            // built-in no-animation path calculates user/target changes.
            // Do not change vendor settings or apply HP directly.
            attackLevel.active.replaceSystemActionSchematics = true;
            attackLevel.active.systemActionSchematics = new BattleSystemActionSchematicsSettings();
            // A selectable ORK base-attack shortcut alone deals no damage:
            // the frozen starter ability has no target status changes. Give
            // this Soundwell prototype a whole HP status step so that ORK's
            // integer-valued HP does not round fractional damage to zero.
            // The player's UGUI action still uses ORK's real turn resolver.
            // This is game-owned runtime setup, not a second HP authority.
            if (attackLevel.active.targetChange == null ||
                attackLevel.active.targetChange.Length == 0)
            {
                StatusValueAsset hpAsset = ORK.StatusValues.GetAsset(1);
                if (hpAsset == null)
                {
                    Fail("Soundwell base attack cannot resolve ORK HP asset ID 1.");
                    yield break;
                }
                var hpChange = new StatusValueStatusChangeType
                {
                    simpleOperator = SimpleOperator.Add,
                    changeValue = new FloatValue<GameObjectSelection>(-1f)
                };
                hpChange.status.SetAsset(hpAsset);
                attackLevel.active.targetChange = new[]
                {
                    new TargetStatusChange
                    {
                        change = new[]
                        {
                            new StatusChangeType
                            {
                                type = typeof(StatusValueStatusChangeType).ToString(),
                                settings = hpChange
                            }
                        }
                    }
                };
            }

            placeholderSprite = CreatePixelRescuerSprite(
                IdlePixels, "PLACEHOLDER 16px Soundwell rescuer idle", out placeholderTexture);
            walkingSprite = CreatePixelRescuerSprite(
                WalkPixels, "PLACEHOLDER 16px Soundwell rescuer walking", out walkingTexture);
            attackSprite = CreatePixelRescuerSprite(
                AttackPixels, "PLACEHOLDER 16px Soundwell rescuer attack", out attackTexture);

            EnsureInvisibleActorRenderer(leader.gameObject);
            EnsureInvisibleActorRenderer(beast.gameObject);
            ConfigureCombatant(leader.gameObject, playerFaction);
            actors.Add(leader.gameObject);

            GameObject second = CreateFollower("PLACEHOLDER Rescuer 2",
                FollowOffsets[0], new Color(0.95f, 0.72f, 0.34f));
            GameObject third = CreateFollower("PLACEHOLDER Rescuer 3",
                FollowOffsets[1], new Color(0.43f, 0.72f, 0.96f));
            actors.Add(second);
            actors.Add(third);

            ConfigureCombatant(beast.gameObject, enemyFaction);

            float registrationDeadline = Time.realtimeSinceStartup + RegistrationTimeout;
            while (Time.realtimeSinceStartup < registrationDeadline)
            {
                members.Clear();
                foreach (GameObject actor in actors)
                {
                    Combatant member = ORKComponentHelper.GetCombatant(actor);
                    if (member != null)
                        members.Add(member);
                }
                Beast = ORKComponentHelper.GetCombatant(beast.gameObject);
                if (members.Count == 3 && Beast != null)
                    break;
                yield return null;
            }

            if (members.Count != 3 || Beast == null)
            {
                Fail("ORK Add Combatant did not register all three rescuers and the Soundwell beast.");
                yield break;
            }

            PlayerGroup = new Group(ORK.Factions.Get(0));
            EnemyGroup = new Group(ORK.Factions.Get(1));
            PlayerGroup.BattleSystem = turnBasedBattleSystem.Settings;
            EnemyGroup.BattleSystem = turnBasedBattleSystem.Settings;

            foreach (Combatant member in members)
            {
                if (!PlayerGroup.IsMember(member))
                    PlayerGroup.Join(member, false, false);
            }
            if (!EnemyGroup.IsMember(Beast))
                EnemyGroup.Join(Beast, false, false);
            PlayerGroup.SetLeader(members[0], false);

            if (PlayerGroup.GetGroup().Count != 3 || EnemyGroup.GetGroup().Count != 1)
            {
                Fail("ORK did not retain the expected three-member player group and one-member enemy group.");
                yield break;
            }

            IsReady = true;
        }

        private void Update()
        {
            if (playerAssignedToOrk && IsReady && ORK.Initialized)
                DisableConflicting3DPlayerControllers();
        }

        private static void DisableConflicting3DPlayerControllers()
        {
            // ORK's default Button controller adds a 3D CharacterController in
            // Update. That component conflicts with Soundwell's real Rigidbody2D
            // and throws before the player can choose or abandon a battle.
            // Disable only the world-movement component, not ORK combat,
            // statuses, action selection or its UI ownership.
            foreach (ButtonPlayerController controller in
                     UnityEngine.Object.FindObjectsByType<ButtonPlayerController>(
                         FindObjectsSortMode.None))
            {
                if (controller != null && controller.enabled)
                    controller.enabled = false;
            }
        }

        /// <summary>
        /// Read-only snapshot for the provisional battle HUD. ORK remains the
        /// authority for HP, turn choice, battle outcomes and available actions.
        /// </summary>
        public string BuildBattleReadout()
        {
            if (!BattleInProgress || members.Count != 3 || Beast == null)
                return string.Empty;

            var text = new StringBuilder(SoundwellUiLocalization.Get("battle.heading")).Append('\n');
            for (int i = 0; i < members.Count; i++)
            {
                Combatant member = members[i];
                text.Append(SoundwellUiLocalization.Get("battle.rescuer")).Append(' ').Append(i + 1)
                    .Append("  ").Append(SoundwellUiLocalization.Get("battle.hp")).Append(' ')
                    .Append(member.Status[1].GetValue());
                if (member.Actions.IsChoosing)
                    text.Append("  ").Append(SoundwellUiLocalization.Get("battle.turn"));
                text.Append('\n');
            }
            text.Append(SoundwellUiLocalization.Get("battle.beast")).Append("  ")
                .Append(SoundwellUiLocalization.Get("battle.hp")).Append(' ')
                .Append(Beast.Status[1].GetValue());
            // A defeated enemy has no further attack choice to present.
            // A missing next chooser while its HP is positive is different
            // from a completed action resolving ORK death/victory.
            if (ReadyBaseAttackerIndex < 0 && !Beast.Status.IsDead &&
                Beast.Status[1].GetValue() > 0f)
                text.Append('\n').Append(SoundwellUiLocalization.Get("battle.menu_pending"));
            return text.ToString();
        }

        // The current Soundwell encounter has one real enemy. Determine
        // which ORK combatant has a legal base attack this turn; never choose
        // turns or maintain a second battle-action queue in the scene.
        public int ReadyBaseAttackerIndex
        {
            get
            {
                if (!BattleInProgress || Beast == null || Beast.Status.IsDead)
                    return -1;
                for (int i = 0; i < members.Count; i++)
                {
                    Combatant member = members[i];
                    if (member == null || member.Status.IsDead || !member.Actions.IsChoosing)
                        continue;
                    AbilityShortcut attack = member.Abilities.GetCurrentBaseAttack();
                    if (attack != null && attack.CanChoose(member, true, true) &&
                        attack.CanTarget(member, Beast))
                        return i;
                }
                return -1;
            }
        }

        // The only selectable target is ORK's actual beast combatant. Use the
        // configured ORK base attack, its action costs and its turn processing;
        // no manual HP subtraction or locally manufactured damage event.
        public bool TryAttackBeast()
        {
            int index = ReadyBaseAttackerIndex;
            if (index < 0)
                return false;
            Combatant attacker = members[index];
            AbilityShortcut attack = attacker.Abilities.GetCurrentBaseAttack();
            bool targetAllowed = attack != null && attack.CanTarget(attacker, Beast);
            bool accepted = targetAllowed &&
                attack.Use(attacker, new List<Combatant> { Beast }, true);
            if (accepted && index > 0 && index < actors.Count)
            {
                // Presentation only. ORK has already accepted the real turn
                // action; show which companion acted without touching status,
                // damage, Combatant placement, or the battle action queue.
                // ORK can slow/pause scaled gameplay time while resolving a
                // turn. Action feedback uses unscaled presentation time so it
                // expires even while a battle transition owns the clock.
                companionAttackUntil[index] = Time.unscaledTime + CompanionAttackPoseSeconds;
                SpriteRenderer follower = actors[index].GetComponent<SpriteRenderer>();
                if (follower != null)
                    follower.sprite = attackSprite;
            }
            return accepted;
        }

        public bool TryStartDirectEncounter()
        {
            if (!IsReady || battleStarted || Beast == null || Beast.Status.IsDead ||
                !ORK.Initialized || ORK.Control.InBattle || IsBattleRunning() ||
                PlayerGroup == null || EnemyGroup == null || members.Count != 3)
                return false;

            PlayerGroup.SetLeader(members[0], false);
            ORK.Game.PlayerHandler.SetGroup(0, PlayerGroup, false);
            ORK.Game.PlayerHandler.SetActiveGroup(0, false, false);
            ORK.Game.PlayerHandler.SetPlayer(members[0], false);
            playerAssignedToOrk = true;
            DisableConflicting3DPlayerControllers();

            BattleComponent battle = BattleComponent.CreateBattle(new List<Combatant> { Beast });
            if (battle == null)
                return false;

            battleStarted = true;
            battleObservedActive = false;
            battleEndRequested = false;
            battleStartedAt = Time.realtimeSinceStartup;
            battle.StartGroup(EnemyGroup, null);
            return true;
        }

        public void ResetEncounter()
        {
            if (ORK.Initialized && battleStarted)
            {
                // CombatantHandler.EndBattle() only detaches participants; ORK's
                // battle arena/control state can still block StartInteraction
                // on the next encounter. Clear the actual ORK battle owner only
                // once its participants have left, before reloading the scene.
                if (IsBattleRunning())
                    ORK.Game.Combatants.EndBattle();
                if (ORK.Game.Combatants.GetInBattle().Count == 0)
                    ORK.Battle.ClearBattle();
            }

            if (Beast != null)
                Beast.Status.Regenerate(true);
            foreach (Combatant member in members)
            {
                if (member != null)
                    member.Status.Regenerate(true);
            }

            battleStarted = false;
            battleObservedActive = false;
            battleEndRequested = false;
            battleStartedAt = 0f;
            for (int i = 1; i < companionAttackUntil.Length; i++)
                companionAttackUntil[i] = 0f;
        }

        public void ReleaseFailedEncounter()
        {
            ResetEncounter();
        }

        private bool IsBattleRunning()
        {
            if (!ORK.Initialized)
                return false;

            List<Combatant> participants = ORK.Game.Combatants.GetInBattle();
            if (Beast != null && participants.Contains(Beast))
                return true;
            foreach (Combatant member in members)
            {
                if (member != null && participants.Contains(member))
                    return true;
            }
            return false;
        }

        // Recover the retired victory candidate's valid battle-end logic,
        // using the real ORK 3.22 Status.IsDead API (Combatant.Dead does
        // not exist in this pinned framework).
        private bool AreAllBattleEnemiesDefeated()
        {
            if (EnemyGroup == null)
                return false;

            List<Combatant> enemies = EnemyGroup.GetBattle();
            if (enemies.Count == 0)
                return Beast != null && Beast.Status.IsDead;

            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && !enemies[i].Status.IsDead)
                    return false;
            }
            return true;
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
            return Sprite.Create(texture,
                new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                16f, 0, SpriteMeshType.FullRect);
        }

        private GameObject CreateFollower(string objectName, Vector2 offset, Color tint)
        {
            var actor = new GameObject(objectName);
            actor.SetActive(false);
            actor.transform.SetParent(transform, true);
            actor.transform.position = leader.position + (Vector3)offset;
            actor.transform.localScale = Vector3.one; // 16 pixels at 16 PPU = one world unit.

            var renderer = actor.AddComponent<SpriteRenderer>();
            renderer.sprite = placeholderSprite;
            renderer.color = tint;
            renderer.sortingOrder = 3;

            AddCombatant addCombatant = actor.AddComponent<AddCombatant>();
            actor.SetActive(true);
            ConfigureCombatant(addCombatant, playerFaction);
            return actor;
        }

        private void ConfigureCombatant(GameObject actor, FactionAsset faction)
        {
            AddCombatant addCombatant = actor.GetComponent<AddCombatant>();
            if (addCombatant == null)
                addCombatant = actor.AddComponent<AddCombatant>();
            ConfigureCombatant(addCombatant, faction);
        }

        private void ConfigureCombatant(AddCombatant addCombatant, FactionAsset faction)
        {
            // Runtime AddComponent does not apply editor Reset defaults. Set
            // the saved asset selections after activation but before Start.
            addCombatant.settings.faction.SetAsset(faction);
            addCombatant.settings.combatantSetting.combatant.SetAsset(defaultCombatant);
            addCombatant.conditionSetting.autoCheckType = ConditionAutoCheckType.Start;
        }

        private static void EnsureInvisibleActorRenderer(GameObject actor)
        {
            SpriteRenderer renderer = actor.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = actor.AddComponent<SpriteRenderer>();
            renderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (battleStarted && IsBattleRunning())
                battleObservedActive = true;

            // The ORK death check, not a test or a parallel combat UI,
            // authoritatively decides whether the whole enemy group fell.
            // End the encounter once, then let the existing gate readout
            // consume ORK's result. Never finish an unfinished battle.
            if (battleStarted && battleObservedActive && !battleEndRequested &&
                IsBattleRunning() && AreAllBattleEnemiesDefeated())
            {
                battleEndRequested = true;
                ORK.Game.Combatants.EndBattle();
            }

            if (!IsReady || leader == null)
                return;

            // Two authored pixel frames and SpriteRenderer.flipX preserve pixel
            // outlines. Never animate the ORK Combatant or change its authority.
            bool inBattle = IsBattleRunning();
            for (int i = 1; i < actors.Count; i++)
            {
                Transform follower = actors[i].transform;
                SpriteRenderer renderer = actors[i].GetComponent<SpriteRenderer>();
                if (inBattle)
                {
                    renderer.sprite = Time.unscaledTime < companionAttackUntil[i]
                        ? attackSprite : placeholderSprite;
                    continue;
                }

                Vector3 target = leader.position + (Vector3)FollowOffsets[i - 1];
                target.z = 0f;
                Vector3 prior = follower.position;
                follower.position = Vector3.MoveTowards(prior,
                    target, FollowSpeed * Time.deltaTime);
                Vector3 displacement = follower.position - prior;
                bool moving = displacement.sqrMagnitude > 0.000001f;
                if (Mathf.Abs(displacement.x) > 0.001f)
                    renderer.flipX = displacement.x < 0f;
                renderer.sprite = moving &&
                    Mathf.FloorToInt(Time.time / WalkFrameSeconds) % 2 == 1
                    ? walkingSprite : placeholderSprite;
            }
        }

        private void Fail(string reason)
        {
            InitializationError = reason;
            Debug.LogError("Soundwell ORK party initialization failed: " + reason, this);
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
        }
    }
}
