using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public enum ReturnStoryLedgerTab
    {
        MainStory = 0,
        RosterCases = 1,
        RegionalTales = 2
    }

    /// <summary>
    /// Bottom-anchored safe-region UGUI Story Ledger panel for Return ("归来").
    /// Displays the live verified main-story objective (M07-M16), all five roster
    /// cases (RC-01..RC-05), and regional tales (ST-01, ST-02, ST-03, ST-05, ST-06) with
    /// tab switching, ZH/EN localization, and viewport aspect scaling.
    /// </summary>
    public sealed class ReturnStoryLedgerView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] tabButtons = new Button[3];
        private readonly Text[] tabLabels = new Text[3];
        private readonly Button closeButton;
        private readonly Text closeLabel;
        private ReturnStoryProgressSnapshot snapshot;
        private ReturnStoryLedgerTab activeTab = ReturnStoryLedgerTab.MainStory;
        private Action onClosed;
        private bool disposed;

        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public ReturnStoryLedgerTab ActiveTab => activeTab;
        public Button MainTabButton => tabButtons[0];
        public Button RosterTabButton => tabButtons[1];
        public Button RegionalTabButton => tabButtons[2];
        public Button CloseButton => closeButton;

        public ReturnStoryLedgerView(
            Transform parent,
            ReturnStoryProgressSnapshot initialSnapshot,
            Action onClosed = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            snapshot = initialSnapshot;
            this.onClosed = onClosed;

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("Return story ledger canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 170;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("Return story ledger bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "Return story ledger border",
                new Vector2(904f, 256f), new Color(0.66f, 0.59f, 0.46f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "Return story ledger backing",
                new Vector2(900f, 252f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);

            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(-110f, 102f), new Vector2(620f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;

            closeButton = SoundwellUiTheme.CreateButton(
                panel, "Return story ledger close button", new Vector2(312f, 102f),
                new Vector2(180f, 28f), 12, out closeLabel);
            closeButton.onClick.AddListener(HandleCloseClick);

            body = SoundwellUiTheme.CreateLabel(panel, 14,
                new Vector2(0f, 32f), new Vector2(842f, 96f), ink);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            var positions = new[]
            {
                new Vector2(-282f, -74f),
                new Vector2(0f, -74f),
                new Vector2(282f, -74f)
            };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                tabButtons[i] = SoundwellUiTheme.CreateButton(
                    panel, "Return story ledger tab " + i, positions[i],
                    new Vector2(272f, 36f), 13, out tabLabels[i]);
                tabButtons[i].onClick.AddListener(() => SelectTab((ReturnStoryLedgerTab)index));
            }

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public void UpdateSnapshot(ReturnStoryProgressSnapshot nextSnapshot)
        {
            if (disposed) return;
            snapshot = nextSnapshot;
            Refresh();
        }

        public void SelectTab(ReturnStoryLedgerTab tab)
        {
            if (disposed || CanvasDestroyed())
                return;
            activeTab = tab;
            Refresh();
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        public void Refresh()
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }

            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = body.font = closeLabel.font = font;
            for (int i = 0; i < tabLabels.Length; i++)
                tabLabels[i].font = font;

            closeLabel.text = zh ? "合上名册" : "Close Ledger";
            tabLabels[0].text = zh ? "主线进度" : "Main Story";
            tabLabels[1].text = zh
                ? $"名册五案 ({snapshot.SettledRosterCaseCount}/5)"
                : $"Roster Cases ({snapshot.SettledRosterCaseCount}/5)";
            tabLabels[2].text = zh
                ? $"地方故事 ({snapshot.SettledRegionalStoryCount}/5)"
                : $"Regional Tales ({snapshot.SettledRegionalStoryCount}/5)";

            for (int i = 0; i < tabButtons.Length; i++)
                tabButtons[i].interactable = (int)activeTab != i;

            switch (activeTab)
            {
                case ReturnStoryLedgerTab.MainStory:
                    heading.text = zh ? "《归来》 · 航程主线名册" : "RETURN · MAIN STORY LEDGER";
                    body.text = FormatMainStoryBody(zh);
                    break;
                case ReturnStoryLedgerTab.RosterCases:
                    heading.text = zh ? "《归来》 · 名册五案核查" : "RETURN · FIVE ROSTER CASES";
                    body.text = FormatRosterCasesBody(zh);
                    break;
                case ReturnStoryLedgerTab.RegionalTales:
                    heading.text = zh ? "《归来》 · 地方故事与回信" : "RETURN · REGIONAL TALES";
                    body.text = FormatRegionalTalesBody(zh);
                    break;
            }

            SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, closeButton);
        }

        private string FormatMainStoryBody(bool zh)
        {
            string stage = FormatChapterName(snapshot.ActiveChapter, zh);
            return zh
                ? $"当前主线阶段：{stage}\n已结名册案件：{snapshot.SettledRosterCaseCount}/5 ｜ 已结地方故事：{snapshot.SettledRegionalStoryCount}/5"
                : $"Current Main Chapter: {stage}\nSettled Roster Cases: {snapshot.SettledRosterCaseCount}/5 | Settled Regional Tales: {snapshot.SettledRegionalStoryCount}/5";
        }

        private string FormatRosterCasesBody(bool zh)
        {
            return zh
                ? $"RC-01 砚叔：{FormatCaseState(snapshot.RC01State, true)} ｜ RC-02 郑远：{FormatCaseState(snapshot.RC02State, true)}\n" +
                  $"RC-03 白矶岛：{FormatCaseState(snapshot.RC03State, true)} ｜ RC-04 丁越礁：{FormatCaseState(snapshot.RC04State, true)} ｜ RC-05 第三班：{FormatCaseState(snapshot.RC05State, true)}"
                : $"RC-01 Chen Yan: {FormatCaseState(snapshot.RC01State, false)} | RC-02 Zheng Yuan: {FormatCaseState(snapshot.RC02State, false)}\n" +
                  $"RC-03 Baiji Mine: {FormatCaseState(snapshot.RC03State, false)} | RC-04 Ding Yue Reef: {FormatCaseState(snapshot.RC04State, false)} | RC-05 Third Squad: {FormatCaseState(snapshot.RC05State, false)}";
        }

        private string FormatRegionalTalesBody(bool zh)
        {
            string done = zh ? "已落盘" : "Settled";
            string open = zh ? "未完成" : "Pending";
            bool st01Done = snapshot.ST01Outcome.HasValue && snapshot.ST01Outcome.Value.Theater.HasValue;
            return zh
                ? $"ST-01 镜湾戏院：{(st01Done ? done : open)} ｜ ST-02 两盏河灯：{(snapshot.ST02Record.HasValue ? done : open)} ｜ ST-03 第三声钟澡堂：{(snapshot.ST03Record.HasValue ? done : open)}\n" +
                  $"ST-05 两扇门乔迁宴：{(snapshot.ST05Outcome.HasValue ? done : open)} ｜ ST-06 陶霁返乡：{(snapshot.ST06Record.HasValue ? done : open)}"
                : $"ST-01 Jingwan Theater: {(st01Done ? done : open)} | ST-02 River Lanterns: {(snapshot.ST02Record.HasValue ? done : open)} | ST-03 Bathhouse: {(snapshot.ST03Record.HasValue ? done : open)}\n" +
                  $"ST-05 Two Doors: {(snapshot.ST05Outcome.HasValue ? done : open)} | ST-06 Tao Ji: {(snapshot.ST06Record.HasValue ? done : open)}";
        }

        private static string FormatCaseState(ReturnRosterCaseState state, bool zh)
        {
            switch (state)
            {
                case ReturnRosterCaseState.Settled: return zh ? "已结案" : "Settled";
                case ReturnRosterCaseState.Available: return zh ? "可调查" : "Available";
                default: return zh ? "未开启" : "Locked";
            }
        }

        private static string FormatChapterName(ReturnMainChapter chapter, bool zh)
        {
            switch (chapter)
            {
                case ReturnMainChapter.BeforeM08: return zh ? "M00–M07 启航与首次营救" : "M00-M07 Outbound & Extraction";
                case ReturnMainChapter.M08FirstWorldNotice: return zh ? "M08 临潮第一张告示" : "M08 First World Notice";
                case ReturnMainChapter.M09BerthTable: return zh ? "M09 船位谈判与东岸夜渡" : "M09 Berth Table & Crossing";
                case ReturnMainChapter.M10Assembly: return zh ? "M10 三方对账与集结" : "M10 Three-Way Assembly";
                case ReturnMainChapter.M11ReverseRescue: return zh ? "M11 回澜关逆向营救" : "M11 Reverse Rescue";
                case ReturnMainChapter.M12BridgeTruth: return zh ? "M12 断桥真相与公开抉择" : "M12 Bridge Truth";
                case ReturnMainChapter.M13DepartureRisk: return zh ? "M13 终航路线与风险告知" : "M13 Departure Risk";
                case ReturnMainChapter.M14FinalPassage: return zh ? "M14 最后一趟通道" : "M14 Final Passage";
                case ReturnMainChapter.M15FinalRollCall: return zh ? "M15 终局点名与刻碑" : "M15 Final Roll Call";
                case ReturnMainChapter.M16Epilogue: return zh ? "M16 一个普通的下午" : "M16 An Ordinary Afternoon";
                default: return zh ? "全剧已终场落盘" : "Story Completed";
            }
        }

        private void HandleCloseClick()
        {
            if (disposed || CanvasDestroyed())
                return;
            var callback = onClosed;
            Dispose();
            callback?.Invoke();
        }

        private bool CanvasDestroyed()
        {
            if (canvasObject == null || panel == null || heading == null || body == null ||
                closeButton == null || closeLabel == null)
                return true;
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null || tabLabels[i] == null)
                    return true;
            }
            return false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onClosed = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (closeButton != null)
                closeButton.onClick.RemoveAllListeners();
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] != null)
                    tabButtons[i].onClick.RemoveAllListeners();
            }
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
