using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Rpg.Editor
{
    // Native Unity Localization tables, created in Editor and saved in the game
    // Resources folder. This first isolated UI slice does not create a second
    // ORK database or Addressables authority for RPG text.
    public static class SoundwellLocalizationSetup
    {
        private const string Folder = "Assets/Game/Resources/Soundwell/Localization";
        private const string SharedPath = Folder + "/SoundwellUI-Shared.asset";
        private const string EnPath = Folder + "/SoundwellUI-en.asset";
        private const string ZhPath = Folder + "/SoundwellUI-zh-Hans.asset";
        private const string CjkFontPath = "Assets/Game/Resources/Soundwell/Fonts/SoundwellChinese.ttf";
        private static readonly (string key, string en, string zh)[] Entries =
        {
            ("hud.title", "SOUNDWELL  /  SIGNAL TRIAL", "声井 / 信号试炼"),
            ("hud.entrance", "PLACEHOLDER   /   Entrance and observation lane", "占位场景 / 入口与观察通道"),
            ("hud.retreat", "RETREAT", "撤退点"),
            ("hud.copper_bell", "COPPER BELL", "铜铃"),
            ("hud.side_stand", "SIDE BELL STAND", "侧边铃架"),
            ("hud.controls", "[WASD / Arrows / Stick] Move    [Esc / Pad B] Back to entry", "[WASD / 方向键 / 摇杆] 移动    [Esc / B] 返回入口"),
            ("hud.focus", "TAB / START: FOCUS ACTIONS", "TAB / START：选择操作"),
            ("hud.focus_active", "ACTION FOCUS  /  MOVE: SELECT  /  ENTER OR A: USE  /  ESC OR B: RESUME", "操作选择 / 方向键：选择 / Enter 或 A：确认 / Esc 或 B：返回"),
            ("action.ring", "RING  [E / A]", "摇铃 [E / A]"),
            ("action.wrap", "WRAP  [Q / X]", "包裹 [Q / X]"),
            ("action.unwrap", "UNWRAP  [Q / X]", "解开 [Q / X]"),
            ("action.retry", "RESET  [R / Y]", "重试 [R / Y]"),
            ("action.back", "BACK  [Esc / B]", "返回 [Esc / B]"),
            ("action.lift", "LIFT  [F / LB]", "拿起 [F / LB]"),
            ("action.drop", "DROP  [F / LB]", "放下 [F / LB]"),
            ("action.language", "中文 / EN", "EN / 中文"),
            // Deliberately absent in Chinese for real EN fallback verification.
            ("diagnostic.fallback", "English table fallback", null),
        };

        public static void Prepare()
        {
            if (AssetDatabase.LoadAssetAtPath<StringTable>(EnPath) != null)
            {
                Validate();
                return;
            }
            if (AssetDatabase.IsValidFolder(Folder))
                throw new InvalidOperationException("Partial M9 localization data found; refusing to overwrite.");
            if (!AssetDatabase.IsValidFolder("Assets/Game/Resources/Soundwell"))
                throw new InvalidOperationException("Saved game-owned Soundwell resources are missing.");
            AssetDatabase.CreateFolder("Assets/Game/Resources/Soundwell", "Localization");
            var shared = ScriptableObject.CreateInstance<SharedTableData>();
            shared.TableCollectionName = "Soundwell UI";
            AssetDatabase.CreateAsset(shared, SharedPath);
            var en = ScriptableObject.CreateInstance<StringTable>();
            en.SharedData = shared;
            en.LocaleIdentifier = new LocaleIdentifier("en");
            var zh = ScriptableObject.CreateInstance<StringTable>();
            zh.SharedData = shared;
            zh.LocaleIdentifier = new LocaleIdentifier("zh-Hans");
            foreach (var item in Entries)
            {
                en.AddEntry(item.key, item.en);
                if (item.zh != null)
                    zh.AddEntry(item.key, item.zh);
            }
            AssetDatabase.CreateAsset(en, EnPath);
            AssetDatabase.CreateAsset(zh, ZhPath);
            EditorUtility.SetDirty(shared);
            EditorUtility.SetDirty(en);
            EditorUtility.SetDirty(zh);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Validate();
        }

        public static void Validate()
        {
            // The imported OFL font must be a real game asset, not a system
            // font that happens to be installed in one CI container.
            var font = AssetDatabase.LoadAssetAtPath<Font>(CjkFontPath);
            if (font == null || !font.HasCharacter('中') ||
                !font.HasCharacter('声') || !font.HasCharacter('铃'))
                throw new InvalidOperationException("M9 requires the saved game-owned CJK font with real glyphs.");
            var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>(SharedPath);
            var en = AssetDatabase.LoadAssetAtPath<StringTable>(EnPath);
            var zh = AssetDatabase.LoadAssetAtPath<StringTable>(ZhPath);
            if (shared == null || en == null || zh == null ||
                en.SharedData != shared || zh.SharedData != shared ||
                en.LocaleIdentifier.Code != "en" || zh.LocaleIdentifier.Code != "zh-Hans")
                throw new InvalidOperationException("M9 requires two saved StringTables with one shared key source.");
            foreach (var item in Entries)
                if (en.GetEntry(item.key)?.LocalizedValue != item.en ||
                    (item.zh != null && zh.GetEntry(item.key)?.LocalizedValue != item.zh) ||
                    (item.zh == null && zh.GetEntry(item.key) != null))
                    throw new InvalidOperationException("Incorrect saved Soundwell UI key: " + item.key);
            Debug.Log("RPG_SOUNDWELL_LOCALIZATION_VALIDATED");
        }
    }
}
