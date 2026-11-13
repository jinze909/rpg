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
            // Surveyor feedback and earned post-rescue clues share the same saved UI tables.
            ("surveyor.route_posted", "SURVEYOR: My map kit is safe. The verified route is posted where the false sign stood.", "测路人：地图工具已找回。我在旧路标处竖起了核实过的返程指示。"),
            ("surveyor.kit_reclaimed", "SURVEYOR: My map kit is safe. I'll mark the correct route by my old sign.", "测路人：地图工具找回来了。我会回旧路标处标明正确路线。"),
            ("surveyor.cart_mentioned", "SURVEYOR: I've taken down that false trail sign. You found my cart; I'll get my map kit.", "测路人：错误路标已经拆下。你找到了我的骡车，我去取回地图工具。"),
            ("surveyor.sign_removed", "SURVEYOR: My marker was wrong. I've taken down that false trail sign.", "测路人：是我标错了方向。那块误导人的路牌已拆下。"),
            ("observed.cart", "OBSERVED: The abandoned cart was dragged to the gate; its tracks lead inside.", "观察：废弃的骡车曾被拖到闸门，车辙通向内室。"),
            ("observed.rope", "OBSERVED: The bell cord was cut. A rough knot remains.", "观察：铃绳被割断，断口还留着一个粗糙的绳结。"),
            ("observed.claws", "OBSERVED: Claw marks turn toward the bell stand, not RETREAT.", "观察：爪痕转向侧边铃架，而不是撤退点。"),
            ("hint.route_posted", "SAFE ROUTE POSTED: The surveyor replaced his false marker with a verified route.", "安全路线已标记：测路人用核实过的指示替换了错误路标。"),
            ("hint.kit_reclaimed", "MAP KIT RECOVERED: The surveyor is returning to post the safe route.", "地图工具已取回：测路人正返回旧路标处标记安全路线。"),
            ("hint.cart_tell_nearby", "ROUTE CORRECTED: [G / RB] Tell the surveyor about his cart, or leave it for now.", "路线已纠正：[G / RB] 告诉测路人骡车的位置，或暂时离开。"),
            ("hint.cart_tell_later", "ROUTE CORRECTED: You can tell the surveyor about his cart when nearby.", "路线已纠正：靠近测路人时，可以告诉他骡车的位置。"),
            ("hint.cart_fetching", "ROUTE CORRECTED: The surveyor is returning to the cart for his map kit.", "路线已纠正：测路人正返回骡车取回地图工具。"),
            ("hint.sign_removed", "ROUTE CORRECTED: The surveyor removed the false trail marker himself.", "路线已纠正：测路人亲手拆掉了错误的方向标记。"),
            // Contextual puzzle, plank and escort prompts stay aligned with the actual UI locale.
            ("hint.plank_tie", "[G / RB] Tie the recovered cord to guide the surveyor. Rescue not complete.", "[G / RB] 系好找回的绳索引导测路人。救援尚未完成。"),
            ("hint.plank_lead", "[G / RB] Lead the surveyor through the gate. Rescue not complete.", "[G / RB] 带测路人穿过闸门。救援尚未完成。"),
            ("hint.plank_braced", "Upper plank braced; surveyor can stand on it. Rescue not complete.", "上层踏板已加固，测路人可以踏上去。救援尚未完成。"),
            ("hint.plank_brace", "[G / RB] Brace the upper plank for the surveyor.", "[G / RB] 为测路人加固上层踏板。"),
            ("hint.plank_ask_first", "Ask the surveyor which plank is safe before bracing anything.", "先问测路人哪块踏板安全，再动手加固。"),
            ("hint.surveyor_marked", "Upper plank marked. The surveyor is still trapped; brace it before he can cross.", "上层踏板已经标明，但测路人仍被困住；先加固才能通过。"),
            ("hint.surveyor_ask", "[G / RB] Ask the surveyor which plank can carry someone.", "[G / RB] 问测路人哪块踏板能够承重。"),
            ("hint.gate_escort", "Escort the surveyor through the open gate to RETREAT.", "带测路人穿过已开启的闸门，前往撤退点。"),
            ("hint.gate_open", "Gate open: cross into the soundwell inner room.", "闸门已开：进入声井内室。"),
            ("hint.gate_lure_open", "[G / RB] Open the gate while the beast follows the side bell.", "野兽追着侧铃时，[G / RB] 开启闸门。"),
            ("hint.gate_claw_route", "Move the bell to the SIDE STAND and draw the beast away to open this gate.", "将铜铃搬到侧边铃架，引开野兽后再开启闸门。"),
            ("hint.gate_search", "The beast guards this gate. Look for evidence of what draws its attention.", "野兽守着这道闸门。找找能引起它注意的线索。"),
            ("hint.bell_wrapped", "[E / Pad A] Try the wrapped bell   [Q / Pad X] Remove the cloth", "[E / 手柄 A] 试着摇包好的铜铃   [Q / 手柄 X] 解开布料"),
            ("hint.bell_ready", "[E / Pad A] Ring the bell   [Q / Pad X] Wrap with the wet cloth   [F / LB] Lift", "[E / 手柄 A] 摇铃   [Q / 手柄 X] 用湿布包铃   [F / LB] 拿起"),
            ("hint.reset_here", "[R / Pad Y] Reset the experiment", "[R / 手柄 Y] 重置试验"),
            ("hint.escort_to_retreat", "Lead the surveyor to RETREAT. Do not leave him behind.", "带测路人走到撤退点，别把他落在身后。"),
            ("hint.return_to_reset", "Return to RETREAT to reset the experiment.", "回到撤退点才能重置试验。"),
            ("hint.approach_bell", "Walk to the copper bell. Watch the beast beyond the grate.", "走近铜铃，观察铁栅另一侧的野兽。"),
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
