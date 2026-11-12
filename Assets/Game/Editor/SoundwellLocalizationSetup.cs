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
            ("action.retreat", "RETREAT  [Esc / B]", "撤离 [Esc / B]"),
            ("action.lift", "LIFT  [F / LB]", "拿起 [F / LB]"),
            ("action.drop", "DROP  [F / LB]", "放下 [F / LB]"),
            ("action.language", "中文 / EN", "EN / 中文"),
            ("battle.heading", "ORK TURN-BASED ENCOUNTER  /  PLACEHOLDER", "ORK 回合战 / 占位"),
            ("battle.rescuer", "Rescuer", "救援队员"),
            ("battle.beast", "Beast", "野兽"),
            ("battle.hp", "HP", "生命"),
            ("battle.turn", "< TURN", "< 行动"),
            ("battle.menu_pending", "No attack available right now.", "当前没有可用的攻击指令。"),
            ("battle.guard", "Guard self", "自我防守"),
            ("battle.open_gate", "Open gate", "开启闸门"),
            ("battle.defending", "GUARDING", "防守中"),
            ("action.engage_beast", "ENGAGE BEAST [G / RB]", "迎战野兽 [G / RB]"),
            ("action.inspect_rope", "INSPECT ROPE [G / RB]", "检查绳索 [G / RB]"),
            ("action.recover_cord", "RECOVER CORD [G / RB]", "收回绳索 [G / RB]"),
            ("action.inspect_cart", "INSPECT CART [G / RB]", "检查货车 [G / RB]"),
            ("action.tell_cart", "TELL ABOUT CART [G / RB]", "告知货车位置 [G / RB]"),
            ("action.inspect_claws", "INSPECT CLAWS [G / RB]", "查看爪痕 [G / RB]"),
            ("action.tie_guide_line", "TIE GUIDE LINE [G / RB]", "系好引导绳 [G / RB]"),
            ("action.lead_surveyor", "LEAD SURVEYOR [G / RB]", "带测路人撤离 [G / RB]"),
            ("action.brace_plank", "BRACE PLANK [G / RB]", "加固木板 [G / RB]"),
            ("action.ask_surveyor", "ASK SURVEYOR [G / RB]", "询问测路人 [G / RB]"),
            ("action.open_gate_focus", "OPEN GATE [G / RB]", "开启闸门 [G / RB]"),
            // Deliberately absent in Chinese for real EN fallback verification.
            ("diagnostic.fallback", "English table fallback", null),
            ("entry.sfx_mute", "SFX ON  /  MUTE", "音效开启 / 点击静音"),
            ("entry.sfx_unmute", "SFX MUTED  /  UNMUTE", "音效已静音 / 点击开启"),
        };

        public static void Prepare()
        {
            if (AssetDatabase.LoadAssetAtPath<StringTable>(EnPath) != null)
            {
                // Only prepare may append newly authored game-owned UI keys.
                // Verify must never silently repair or overwrite translations.
                var existingEn = AssetDatabase.LoadAssetAtPath<StringTable>(EnPath);
                var existingZh = AssetDatabase.LoadAssetAtPath<StringTable>(ZhPath);
                var existingShared = AssetDatabase.LoadAssetAtPath<SharedTableData>(SharedPath);
                if (existingShared == null || existingZh == null ||
                    existingEn.SharedData != existingShared ||
                    existingZh.SharedData != existingShared)
                    throw new InvalidOperationException("Incomplete M9 StringTable assets; refusing to repair.");
                bool changed = false;
                foreach (var item in Entries)
                {
                    var english = existingEn.GetEntry(item.key);
                    if (english == null)
                    {
                        existingEn.AddEntry(item.key, item.en);
                        changed = true;
                    }
                    else if (english.LocalizedValue != item.en)
                        throw new InvalidOperationException("Existing M9 English translation differs: " + item.key);

                    var chinese = existingZh.GetEntry(item.key);
                    if (item.zh == null)
                    {
                        if (chinese != null)
                            throw new InvalidOperationException("EN fallback key acquired a Chinese override: " + item.key);
                    }
                    else if (chinese == null)
                    {
                        existingZh.AddEntry(item.key, item.zh);
                        changed = true;
                    }
                    else if (chinese.LocalizedValue != item.zh)
                        throw new InvalidOperationException("Existing M9 Chinese translation differs: " + item.key);
                }
                if (changed)
                {
                    EditorUtility.SetDirty(existingShared);
                    EditorUtility.SetDirty(existingEn);
                    EditorUtility.SetDirty(existingZh);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                }
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
