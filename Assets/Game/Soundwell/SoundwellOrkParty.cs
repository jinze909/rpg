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
        private bool battleStarted;
        // The party remains ORK-owned for battles, but its exploration input is
        // exclusively handled by the saved Soundwell 2D Input System adapter.
        private bool playerAssignedToOrk;
        private bool battleObservedActive;
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

            placeholderSprite = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f), 100f);

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
                .Append(Beast.Status[1].GetValue())
                .Append('\n').Append(SoundwellUiLocalization.Get("battle.menu_pending"));
            return text.ToString();
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
            battleStartedAt = 0f;
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

        private GameObject CreateFollower(string objectName, Vector2 offset, Color tint)
        {
            var actor = new GameObject(objectName);
            actor.SetActive(false);
            actor.transform.SetParent(transform, true);
            actor.transform.position = leader.position + (Vector3)offset;
            actor.transform.localScale = new Vector3(0.56f, 0.56f, 1f);

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

            if (!IsReady || leader == null || IsBattleRunning())
                return;

            for (int i = 1; i < actors.Count; i++)
            {
                Transform follower = actors[i].transform;
                Vector3 target = leader.position + (Vector3)FollowOffsets[i - 1];
                target.z = 0f;
                follower.position = Vector3.MoveTowards(follower.position,
                    target, FollowSpeed * Time.deltaTime);
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
        }
    }
}
