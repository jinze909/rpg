using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using GamingIsLove.ORKFramework.Components;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Exercises the live Soundwell scene's ORK party setup, then starts one
    /// real Turn Based battle with its three rescuers and the borrowed-voice beast.
    /// </summary>
    public sealed class SoundwellOrkBattleTests
    {
        [UnityTest]
        public IEnumerator ThreePlayerCombatantsEnterOneOrkTurnBasedBattle()
        {
            DumpUiAssetConfiguration();
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return WaitForFrameworkInitialization();

            AsyncOperation load = SceneManager.LoadSceneAsync(SoundwellEntrance.SceneName);
            Assert.That(load, Is.Not.Null, "The saved Soundwell scene must load from the Bootstrap runtime.");
            while (!load.isDone)
                yield return null;

            GameObject root = GameObject.Find("Soundwell signal trial");
            Assert.That(root, Is.Not.Null, "The saved Soundwell scene root must exist.");
            SoundwellOrkParty party = root.GetComponent<SoundwellOrkParty>();
            SoundwellTrial trial = root.GetComponent<SoundwellTrial>();
            Assert.That(party, Is.Not.Null, "The live scene must own its ORK party setup.");
            Assert.That(trial, Is.Not.Null, "The live scene must own its rescue route state.");

            float partyDeadline = Time.realtimeSinceStartup + 12f;
            while (!party.IsReady && string.IsNullOrEmpty(party.InitializationError) &&
                   Time.realtimeSinceStartup < partyDeadline)
                yield return null;

            Assert.That(party.IsReady, Is.True,
                "The live Soundwell scene must register its ORK party. " + party.InitializationError);
            float trialDeadline = Time.realtimeSinceStartup + 8f;
            while (!trial.IsReady && Time.realtimeSinceStartup < trialDeadline)
                yield return null;
            Assert.That(trial.IsReady, Is.True, "The live rescue trial must initialize its Makinom state.");
            trial.ReadInput = false;
            Assert.That(party.Members.Count, Is.EqualTo(3));
            Assert.That(party.Actors.Count, Is.EqualTo(3));
            Assert.That(party.PlayerGroup.GetGroup().Count, Is.EqualTo(3));
            Assert.That(party.EnemyGroup.GetGroup().Count, Is.EqualTo(1));
            Assert.That(party.Members[0].GameObject.transform, Is.SameAs(party.Leader));
            Assert.That(party.Actors[1].GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(party.Actors[2].GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(party.PlayerGroup.BattleSystem, Is.SameAs(party.TurnBasedAsset.Settings));
            Assert.That(party.EnemyGroup.BattleSystem, Is.SameAs(party.TurnBasedAsset.Settings));

            Group playerGroup = party.PlayerGroup;
            Group enemyGroup = party.EnemyGroup;
            Rigidbody2D leaderBody = party.Leader.GetComponent<Rigidbody2D>();
            Assert.That(leaderBody, Is.Not.Null, "The ORK leader must share the live exploration body.");
            leaderBody.position = new Vector2(-0.55f, 0f);
            Physics2D.SyncTransforms();
            Assert.That(trial.CanEngageBeast, Is.True,
                "The undecided route must offer direct engagement at the real gate panel.");
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.DirectCombatStarted, Is.True,
                "The existing gate interaction must start the direct ORK rescue route.");
            Assert.That(ORK.Game.PlayerHandler.ActiveGroup, Is.SameAs(playerGroup));
            Assert.That(ORK.Game.ActiveGroup.Leader, Is.SameAs(party.Members[0]));

            float battleDeadline = Time.realtimeSinceStartup + 8f;
            while (ORK.Game.Combatants.GetInBattle().Count < 4 &&
                   Time.realtimeSinceStartup < battleDeadline)
                yield return null;

            List<Combatant> participants = ORK.Game.Combatants.GetInBattle();
            Assert.That(participants.Count, Is.EqualTo(4),
                "ORK must register all three scene rescuers and the beast in one battle.");
            foreach (Combatant member in party.Members)
            {
                Assert.That(participants, Does.Contain(member));
                Assert.That(member.GameObject, Is.Not.Null);
            }
            Assert.That(participants, Does.Contain(party.Beast));
            Assert.That(playerGroup.GetBattle().Count, Is.EqualTo(3));
            Assert.That(enemyGroup.GetBattle().Count, Is.EqualTo(1));

            float turnDeadline = Time.realtimeSinceStartup + 6f;
            while (!party.Members.Any(member => member.Actions.IsChoosing) &&
                   Time.realtimeSinceStartup < turnDeadline)
                yield return null;
            Assert.That(party.Members.Any(member => member.Actions.IsChoosing), Is.True,
                "The direct route must enter a real ORK player turn, not only register battle participants.");

            party.Beast.Status.Death();
            ORK.Game.Combatants.CheckDeath();
            float victoryDeadline = Time.realtimeSinceStartup + 8f;
            while (!trial.DirectCombatComplete && Time.realtimeSinceStartup < victoryDeadline)
                yield return null;
            Assert.That(trial.DirectCombatComplete, Is.True,
                "The gate must remain locked until ORK reports the beast defeated.");
            leaderBody.position = new Vector2(-0.55f, 0f);
            Physics2D.SyncTransforms();
            Assert.That(trial.CanOpenGate, Is.True,
                "A confirmed ORK victory must unlock the same gate without a bell lure.");
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.GateOpen, Is.True,
                "The direct-combat route must open the same physical gate used by the bell route.");

            LogAssert.Expect(UnityEngine.LogType.Log, "RPG_SOUNDWELL_ORK_BATTLE_VALIDATED");
            Debug.Log("RPG_SOUNDWELL_ORK_BATTLE_VALIDATED");
            LogAssert.NoUnexpectedReceived();
        }

        private static void DumpUiAssetConfiguration()
        {
            Debug.Log("RPG_SOUNDWELL_UI_DIAGNOSTICS_BEGIN");
            DumpAsset("Assets/Gaming Is Love/_Data/_Settings/UI System Settings.asset");
            DumpAsset("Assets/Gaming Is Love/_Data/_Settings/UI Boxes.asset");
            DumpAsset("Assets/Gaming Is Love/_Data/_Settings/UI Settings.asset");
            DumpAsset("Assets/Gaming Is Love/_Data/UI Box/0 - Default.asset");
            DumpAsset("Assets/Gaming Is Love/_Data/Battle Menu/0 - Default.asset");

            string[] typeFragments =
            {
                "UnityUISystem", "UnityUIBoxSettings", "DefaultUIPrefabs",
                "UISystemSettings", "UIBoxAsset", "BattleMenuAsset",
                "UnityUIEditorFunctions", "MakinomAssetHelper"
            };
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(type => type != null).ToArray(); }
                foreach (Type type in types)
                {
                    if (typeFragments.Any(fragment => type.FullName != null &&
                        type.FullName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0))
                        DumpType(type, null, 0);
                }
            }
            Debug.Log("RPG_SOUNDWELL_UI_DIAGNOSTICS_END");
        }

        private static void DumpAsset(string path)
        {
            UnityEngine.Object asset = UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);
            Debug.Log("RPG_SOUNDWELL_UI_ASSET " + path + " => " +
                (asset == null ? "null" : asset.GetType().AssemblyQualifiedName));
            if (asset != null)
                DumpType(asset.GetType(), asset, 0);
        }

        private static void DumpType(Type type, object instance, int depth)
        {
            Debug.Log("RPG_SOUNDWELL_UI_TYPE " + type.FullName + " [" + type.Assembly.GetName().Name + "]");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (FieldInfo field in type.GetFields(flags))
            {
                if (!IsUiMember(field.Name))
                    continue;
                object value = null;
                try { if (instance != null && !field.IsStatic) value = field.GetValue(instance); }
                catch (Exception ex) { Debug.Log("RPG_SOUNDWELL_UI_FIELD_ERROR " + field.Name + " " + ex.GetType().Name); }
                Debug.Log("RPG_SOUNDWELL_UI_FIELD " + field.Name + " : " + field.FieldType.FullName +
                    " => " + Describe(value));
                if (depth == 0 && value != null && !field.FieldType.IsPrimitive &&
                    field.FieldType != typeof(string) && !(value is UnityEngine.Object) &&
                    value.GetType().Assembly == type.Assembly)
                    DumpType(value.GetType(), value, depth + 1);
            }
            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                if (!IsUiMember(property.Name) || property.GetIndexParameters().Length != 0 ||
                    property.GetGetMethod(true) == null)
                    continue;
                object value = null;
                try { if (instance != null) value = property.GetValue(instance, null); }
                catch (Exception ex) { Debug.Log("RPG_SOUNDWELL_UI_PROPERTY_ERROR " + property.Name + " " + ex.GetType().Name); }
                Debug.Log("RPG_SOUNDWELL_UI_PROPERTY " + property.Name + " : " + property.PropertyType.FullName +
                    " => " + Describe(value));
                if (depth == 0 && value != null && !(value is UnityEngine.Object) &&
                    value.GetType().Assembly == type.Assembly)
                    DumpType(value.GetType(), value, depth + 1);
            }
        }

        private static bool IsUiMember(string name)
        {
            return name.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Box", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Prefab", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Settings", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("System", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Data", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Asset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Type", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Describe(object value)
        {
            if (value == null) return "null";
            if (value is UnityEngine.Object unityObject) return unityObject.name + " (" + unityObject.GetType().FullName + ")";
            if (value is string text) return text;
            if (value.GetType().IsPrimitive || value.GetType().IsEnum) return value.ToString();
            return value.GetType().FullName;
        }

        private static IEnumerator WaitForFrameworkInitialization()
        {
            double deadline = EditorApplication.timeSinceStartup + 30;
            while ((!Maki.Initialized || !ORK.Initialized) && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            Assert.That(Maki.Initialized, Is.True, "Makinom did not finish loading the saved project.");
            Assert.That(ORK.Initialized, Is.True, "ORK did not initialize from the saved Bootstrap scene.");
        }

        [UnityTearDown]
        public IEnumerator RestoreEditMode()
        {
            if (EditorApplication.isPlaying && ORK.Initialized &&
                ORK.Game.Combatants.GetInBattle().Count > 0)
            {
                ORK.Game.Combatants.EndBattle();
                yield return null;
            }

            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();
        }
    }
}
