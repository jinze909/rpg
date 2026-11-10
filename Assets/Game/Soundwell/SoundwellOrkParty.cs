using System.Collections;
using System.Collections.Generic;
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

        public bool IsReady { get; private set; }
        public string InitializationError { get; private set; }
        public Group PlayerGroup { get; private set; }
        public Group EnemyGroup { get; private set; }
        public Combatant Beast { get; private set; }
        public Transform Leader => leader;
        public IReadOnlyList<GameObject> Actors => actors;
        public IReadOnlyList<Combatant> Members => members;
        public BattleSystemAsset TurnBasedAsset => turnBasedBattleSystem;

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
            if (!IsReady || leader == null ||
                (ORK.Initialized && ORK.Game.Combatants.GetInBattle().Count > 0))
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
