using System.Collections;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using GamingIsLove.ORKFramework.Components;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Proves the installed ORK runtime owns the three-member player group and
    /// starts a real Turn Based arena battle against the Soundwell threat.
    /// This uses ORK data, group, battle and combatant handlers, not a parallel
    /// combat simulator.
    /// </summary>
    public sealed class SoundwellOrkBattleTests
    {
        [UnityTest]
        public IEnumerator ThreePlayerCombatantsEnterOneOrkTurnBasedBattle()
        {
            // This suite runs as an EditMode test. AddCombatant registers its
            // runtime actor from Start, so construct the encounter only after
            // the saved Bootstrap scene has entered a real Play Mode session.
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return WaitForFrameworkInitialization();

            Assert.That(ORK.Combatants.Get(0), Is.Not.Null, "The project must expose its saved default combatant.");
            Assert.That(ORK.Factions.Get(0), Is.Not.Null, "The player faction must be configured.");
            Assert.That(ORK.Factions.Get(1), Is.Not.Null, "The enemy faction must be configured.");

            var playerGroup = new Group(ORK.Factions.Get(0));
            var enemyGroup = new Group(ORK.Factions.Get(1));
            var partyObjects = new List<GameObject>(3);

            for (int i = 0; i < 3; i++)
            {
                partyObjects.Add(CreateCombatantObject("Soundwell Rescuer " + (i + 1),
                    new Vector3(-2f + i * 2f, -1.5f, 0f), 0));
            }

            GameObject beastObject = CreateCombatantObject("Soundwell Borrowed-Voice Beast",
                new Vector3(0f, 1.5f, 0f), 1);

            float spawnDeadline = Time.realtimeSinceStartup + 8f;
            var party = new List<Combatant>(3);
            while ((party.Count < 3 || ORKComponentHelper.GetCombatant(beastObject) == null) &&
                   Time.realtimeSinceStartup < spawnDeadline)
            {
                party.Clear();
                foreach (GameObject partyObject in partyObjects)
                {
                    Combatant member = ORKComponentHelper.GetCombatant(partyObject);
                    if (member != null) party.Add(member);
                }
                yield return null;
            }

            foreach (GameObject partyObject in partyObjects)
            {
                AddCombatant component = partyObject.GetComponent<AddCombatant>();
                Assert.That(component, Is.Not.Null);
                Assert.That(component.isActiveAndEnabled, Is.True);
                Assert.That(component.didStart, Is.True,
                    "The encounter must run in Play Mode so ORK's Start-time registration executes.");
            }

            Assert.That(party.Count, Is.EqualTo(3), "ORK Add Combatant components must create all three party members.");
            // All four actors are now registered by ORK. The player and enemy
            // groups below are explicit runtime groups, not a parallel simulator.

            Combatant beast = ORKComponentHelper.GetCombatant(beastObject);
            Assert.That(beast, Is.Not.Null, "ORK Add Combatant must create the enemy combatant.");
            Assert.That(beast.GameObject, Is.SameAs(beastObject));

            // ORK 3.22 resolves BattleSystemSelection to BattleSystemAsset.Settings,
            // whose runtime wrapper is the base BattleSystem type. Verify the actual
            // selected asset and settings identity instead of guessing a derived CLR type.
            const string turnBasedAssetPath =
                "Assets/Gaming Is Love/_Data/Battle System/0 - Turn Based.asset";
            BattleSystemAsset turnBasedAsset =
                AssetDatabase.LoadAssetAtPath<BattleSystemAsset>(turnBasedAssetPath);
            Assert.That(turnBasedAsset, Is.Not.Null,
                "The saved Turn Based battle-system asset must exist.");
            Assert.That(ORK.BattleSettings.defaultBattleSystem, Is.Not.Null,
                "ORK must expose its saved default battle-system selection.");
            Assert.That(ORK.BattleSettings.defaultBattleSystem.StoredAsset, Is.SameAs(turnBasedAsset),
                "ORK's global default must select the saved Turn Based asset.");
            BattleSystem savedTurnBasedSettings =
                ORK.BattleSettings.defaultBattleSystem.StoredAsset.Settings;
            Assert.That(savedTurnBasedSettings, Is.SameAs(turnBasedAsset.Settings),
                "ORK must resolve the selected asset's settings object.");

            // AddCombatant assigns each spawned combatant's group the framework-resolved
            // default settings. Preserve that same authoritative object in the encounter.
            var playerBattleSystem = party[0].Group.BattleSystem;
            var enemyBattleSystem = beast.Group.BattleSystem;
            Assert.That(playerBattleSystem, Is.SameAs(savedTurnBasedSettings),
                "The player AddCombatant setup must retain the selected Turn Based settings.");
            Assert.That(enemyBattleSystem, Is.SameAs(savedTurnBasedSettings),
                "The enemy AddCombatant setup must retain the selected Turn Based settings.");
            playerGroup.BattleSystem = playerBattleSystem;
            enemyGroup.BattleSystem = enemyBattleSystem;

            foreach (Combatant member in party)
            {
                Assert.That(member.GameObject, Is.Not.Null);
                if (!playerGroup.IsMember(member)) playerGroup.Join(member, false, false);
            }
            if (!enemyGroup.IsMember(beast)) enemyGroup.Join(beast, false, false);

            Assert.That(playerGroup.GetGroup().Count, Is.EqualTo(3),
                "All three rescuers must be real members of the same ORK group.");
            Assert.That(enemyGroup.GetGroup().Count, Is.EqualTo(1),
                "The threat must be a real ORK enemy combatant.");

            // ORK 3.22's CreateBattle factory requires an active-group leader
            // with a live GameObject. Make that invariant explicit instead of
            // relying on implicit leader selection during Group.Join.
            playerGroup.SetLeader(party[0], false);
            ORK.Game.PlayerHandler.SetGroup(0, playerGroup, false);
            ORK.Game.PlayerHandler.SetActiveGroup(0, false, false);
            ORK.Game.PlayerHandler.SetPlayer(party[0], false);
            Assert.That(ORK.Game.PlayerHandler.ActiveGroup, Is.SameAs(playerGroup));
            Assert.That(ORK.Game.ActiveGroup.Leader, Is.SameAs(party[0]),
                "ORK's active player group must have the intended leader.");
            Assert.That(ORK.Game.ActiveGroup.Leader.GameObject, Is.SameAs(partyObjects[0]),
                "ORK's active leader must be backed by the registered player GameObject.");

            // CreateBattle supplies the runtime arena component; ORK requires
            // StartGroup to start it with the enemy group. The player group is
            // already registered as the active player group above.
            BattleComponent battle = BattleComponent.CreateBattle(new List<Combatant> { beast });
            Assert.That(battle, Is.Not.Null, "ORK must create the arena Battle component.");
            battle.StartGroup(enemyGroup, null);

            float battleDeadline = Time.realtimeSinceStartup + 8f;
            while (ORK.Game.Combatants.GetInBattle().Count < 4 &&
                   Time.realtimeSinceStartup < battleDeadline)
            {
                yield return null;
            }

            List<Combatant> participants = ORK.Game.Combatants.GetInBattle();
            Assert.That(participants.Count, Is.EqualTo(4),
                "ORK must register the complete three-person party and the beast in the same battle.");
            foreach (Combatant member in party)
            {
                Assert.That(participants, Does.Contain(member));
                Assert.That(member.GameObject, Is.Not.Null);
            }
            Assert.That(participants, Does.Contain(beast));
            Assert.That(playerGroup.GetBattle().Count, Is.EqualTo(3));
            Assert.That(enemyGroup.GetBattle().Count, Is.EqualTo(1));
            Assert.That(playerGroup.BattleSystem, Is.Not.Null);
            Assert.That(playerGroup.BattleSystem, Is.SameAs(turnBasedAsset.Settings),
                "The player group must retain the selected Turn Based settings object.");
            Assert.That(enemyGroup.BattleSystem, Is.Not.Null);
            Assert.That(enemyGroup.BattleSystem, Is.SameAs(turnBasedAsset.Settings),
                "The enemy group must retain the selected Turn Based settings object.");

            LogAssert.Expect(UnityEngine.LogType.Log, "RPG_SOUNDWELL_ORK_BATTLE_VALIDATED");
            Debug.Log("RPG_SOUNDWELL_ORK_BATTLE_VALIDATED");
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator WaitForFrameworkInitialization()
        {
            double deadline = EditorApplication.timeSinceStartup + 30;
            while ((!Maki.Initialized || !ORK.Initialized) && EditorApplication.timeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(Maki.Initialized, Is.True, "Makinom did not finish loading the saved project.");
            Assert.That(ORK.Initialized, Is.True, "ORK did not initialize from the saved Bootstrap scene.");
        }

        private static GameObject CreateCombatantObject(string objectName, Vector3 position, int factionId)
        {
            var actor = new GameObject(objectName);
            actor.SetActive(false);
            actor.transform.position = position;
            actor.AddComponent<SpriteRenderer>();

            // ORK's Add Combatant component stores its setup in a nested BaseData
            // object, not direct serialized factionId/combatantId fields.
            AddCombatant addCombatant = actor.AddComponent<AddCombatant>();
            actor.SetActive(true);

            // Set the referenced assets before the first Play Mode Start callback.
            string factionAssetPath = factionId == 0
                ? "Assets/Gaming Is Love/_Data/Faction/0 - Player.asset"
                : "Assets/Gaming Is Love/_Data/Faction/1 - Enemies.asset";
            FactionAsset factionAsset = AssetDatabase.LoadAssetAtPath<FactionAsset>(factionAssetPath);
            CombatantAsset combatantAsset = AssetDatabase.LoadAssetAtPath<CombatantAsset>(
                "Assets/Gaming Is Love/_Data/Combatant/0 - Default.asset");
            Assert.That(factionAsset, Is.Not.Null, "The saved ORK faction asset must exist: " + factionAssetPath);
            Assert.That(combatantAsset, Is.Not.Null, "The saved ORK default combatant asset must exist.");

            addCombatant.settings.faction.SetAsset(factionAsset);
            addCombatant.settings.combatantSetting.combatant.SetAsset(combatantAsset);
            addCombatant.conditionSetting.autoCheckType = ConditionAutoCheckType.Start;
            return actor;
        }

        [UnityTearDown]
        public IEnumerator RestoreEditMode()
        {
            if (EditorApplication.isPlaying && ORK.Initialized &&
                ORK.Game.Combatants.GetInBattle().Count > 0)
            {
                // ORK 3.22 ends registered arena battles through its combatant
                // handler; BattleComponent.Stop is not part of the ORK 3 API.
                ORK.Game.Combatants.EndBattle();
                yield return null;
            }

            if (EditorApplication.isPlaying)
            {
                yield return new ExitPlayMode();
            }
        }
    }
}
