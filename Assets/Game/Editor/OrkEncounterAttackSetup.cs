using System;
using System.Linq;
using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using Rpg.Soundwell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rpg.Editor
{
    public static class OrkEncounterAttackSetup
    {
        private const string Data = "Assets/Gaming Is Love/_Data/";
        private const string AttackPath = Data + "Ability/1 - Prototype Attack.asset";
        private const string CombatantPath = Data + "Combatant/1 - Prototype Combatant.asset";

        public static void Prepare()
        {
            var attack = AssetDatabase.LoadAssetAtPath<AbilityAsset>(AttackPath);
            var combatant = AssetDatabase.LoadAssetAtPath<CombatantAsset>(CombatantPath);
            if (attack != null && combatant != null)
                return;
            if (attack != null || combatant != null)
                throw new InvalidOperationException("Partial prototype ORK data needs review before preparation.");

            var baseAttack = Load<AbilityAsset>(Data + "Ability/0 - Default.asset");
            var baseCombatant = Load<CombatantAsset>(Data + "Combatant/0 - Default.asset");
            var hp = Load<StatusValueAsset>(Data + "Status Value/1 - HP.asset");
            var abilities = Load<MakinomSettingsAsset>(Data + "_Settings/Abilities.asset");
            var combatants = Load<MakinomSettingsAsset>(Data + "_Settings/Combatants.asset");

            attack = ScriptableObject.CreateInstance<AbilityAsset>();
            attack.SetData(baseAttack.Settings.GetData());
            attack.Settings.GUID = "218fb96f-6aa0-4350-ba6b-80f69d54bd77";
            attack.Settings.EditorName = "Prototype Attack";
            var level = attack.Settings.GetLevel(1);
            var active = level.active ?? (level.active = new ActiveAbility());
            active.replaceSystemActionSchematics = true;
            active.systemActionSchematics = new BattleSystemActionSchematicsSettings();
            if (active.targetChange == null || active.targetChange.Length == 0)
            {
                var damage = new StatusValueStatusChangeType
                {
                    simpleOperator = SimpleOperator.Add,
                    changeValue = new FloatValue<GameObjectSelection>(-1f)
                };
                damage.status.SetAsset(hp);
                active.targetChange = new[]
                {
                    new TargetStatusChange
                    {
                        change = new[] { new StatusChangeType
                        {
                            type = typeof(StatusValueStatusChangeType).ToString(),
                            settings = damage
                        } }
                    }
                };
            }

            combatant = ScriptableObject.CreateInstance<CombatantAsset>();
            combatant.SetData(baseCombatant.Settings.GetData());
            combatant.Settings.GUID = OrkPartyBridge.PrototypeCombatantGuid;
            combatant.Settings.EditorName = "Prototype Combatant";
            var selection = new ActiveAbilitySelection();
            selection.ability.SetAsset(attack);
            combatant.Settings.ownBaseAttack = true;
            combatant.Settings.baseAttack = new[] { selection };

            SaveAndRegister(attack, AttackPath, abilities);
            SaveAndRegister(combatant, CombatantPath, combatants);
            AssetDatabase.SaveAssets();
            WireParty("Assets/Game/Scenes/Bootstrap.unity", combatant);
            WireParty("Assets/Game/Scenes/SoundwellTrial.unity", combatant);
        }

        private static void SaveAndRegister<T>(T asset, string path, MakinomSettingsAsset registry)
            where T : ScriptableObject, IMakinomGenericAsset
        {
            var settings = registry.Data.ToDataObject();
            var items = settings.GetFileArray("assets");
            if (items == null || items.Length == 0)
                throw new InvalidOperationException($"Missing populated ORK registry {registry.name}.");

            asset.Index = items.Length;
            asset.SaveData(null, false, DataFile.SaveFormatType.ByteArray);
            AssetDatabase.CreateAsset(asset, path);
            var entry = new DataObject();
            entry.SetAsset("asset", asset);
            settings.RemoveArray<DataObject>("assets");
            settings.Set("assets", items.Append(entry).ToArray());
            registry.Data = settings.GetDataFile(registry.Data.name, false, DataFile.SaveFormatType.ByteArray);
            EditorUtility.SetDirty(registry);
        }

        private static void WireParty(string path, CombatantAsset combatant)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var bridge = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<OrkPartyBridge>(true))
                .Single();
            var component = new SerializedObject(bridge);
            component.FindProperty("defaultCombatant").objectReferenceValue = combatant;
            component.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Could not save prototype combatant in {path}.");
        }

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new InvalidOperationException($"Missing ORK authoring source {path}.");
    }
}
