using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public enum RpgQuestJournalTab
    {
        ActiveQuests = 0,
        CompletedQuests = 1,
        FastTravelBeacons = 2
    }

    /// <summary>
    /// Interactive UGUI Landmark Quest Journal and Fast-Travel Beacon panel for
    /// 500x-area RPG maps. Displays exploration completion stats, tracks active
    /// or completed landmark quests with one-click tracking, and dispatches
    /// fast-travel requests strictly for discovered beacons.
    /// </summary>
    public sealed class RpgWorld500xQuestJournalPanel : IDisposable
    {
        private readonly RpgMapExplorationTracker tracker;
        private readonly RpgExpandedMapSpec spec;
        private readonly Action<string, Vector2Int> onFastTravelRequested;
        private readonly Action<string> onTrackedQuestChanged;
        private readonly GameObject rootObject;
        private readonly Text headerText;
        private readonly Text bodySummaryText;
        private readonly Transform rowContainer;
        private readonly ScrollRect rowScroll;
        private readonly Dictionary<RpgQuestJournalTab, Image> tabBackgrounds = new Dictionary<RpgQuestJournalTab, Image>();
        private readonly List<GameObject> spawnedRows = new List<GameObject>();
        private readonly Dictionary<string, Button> questTrackButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> fastTravelButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Font defaultFont;
        private RpgQuestJournalTab currentTab = RpgQuestJournalTab.ActiveQuests;
        private bool visible;
        private bool disposed;

        public GameObject RootObject => rootObject;
        public RpgQuestJournalTab CurrentTab => currentTab;
        public bool IsVisible => !IsDisposed && visible;
        public bool IsDisposed => disposed || rootObject == null;
        public string HeaderText => headerText != null ? headerText.text : string.Empty;
        public string BodySummaryText => bodySummaryText != null ? bodySummaryText.text : string.Empty;
        public int DisplayedRowCount => spawnedRows.Count;

        public RpgWorld500xQuestJournalPanel(
            Transform parent,
            RpgMapExplorationTracker tracker,
            Action<string, Vector2Int> onFastTravelRequested = null,
            Action<string> onTrackedQuestChanged = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (tracker == null) throw new ArgumentNullException(nameof(tracker));

            this.tracker = tracker;
            spec = RpgExpandedWorldScale.GetSpec(tracker.MapId);
            this.onFastTravelRequested = onFastTravelRequested;
            this.onTrackedQuestChanged = onTrackedQuestChanged;
            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            rootObject = new GameObject("RpgWorld500xQuestJournal_" + spec.MapId,
                typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            rootObject.transform.SetParent(parent, false);
            try
            {
                var canvas = rootObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 55;

                var panelObj = new GameObject("JournalPanel", typeof(RectTransform), typeof(Image));
                panelObj.transform.SetParent(rootObject.transform, false);
                var panelRect = panelObj.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.sizeDelta = new Vector2(640f, 440f);
                panelObj.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.13f, 0.94f);

                var headerObj = new GameObject("HeaderText", typeof(RectTransform), typeof(Text));
                headerObj.transform.SetParent(panelObj.transform, false);
                var headerRect = headerObj.GetComponent<RectTransform>();
                headerRect.anchorMin = new Vector2(0.04f, 0.86f);
                headerRect.anchorMax = new Vector2(0.96f, 0.97f);
                headerRect.offsetMin = Vector2.zero;
                headerRect.offsetMax = Vector2.zero;
                headerText = headerObj.GetComponent<Text>();
                headerText.font = defaultFont;
                headerText.fontSize = 15;
                headerText.color = new Color(0.98f, 0.90f, 0.68f, 1f);

                var summaryObj = new GameObject("SummaryText", typeof(RectTransform), typeof(Text));
                summaryObj.transform.SetParent(panelObj.transform, false);
                var summaryRect = summaryObj.GetComponent<RectTransform>();
                summaryRect.anchorMin = new Vector2(0.04f, 0.76f);
                summaryRect.anchorMax = new Vector2(0.96f, 0.85f);
                summaryRect.offsetMin = Vector2.zero;
                summaryRect.offsetMax = Vector2.zero;
                bodySummaryText = summaryObj.GetComponent<Text>();
                bodySummaryText.font = defaultFont;
                bodySummaryText.fontSize = 12;
                bodySummaryText.color = new Color(0.84f, 0.88f, 0.92f, 1f);

                // These are real player-facing tabs, not only a programmatic SetTab API.
                var tabBar = new GameObject("TabBar", typeof(RectTransform));
                tabBar.transform.SetParent(panelObj.transform, false);
                var tabBarRect = tabBar.GetComponent<RectTransform>();
                tabBarRect.anchorMin = new Vector2(0.04f, 0.67f);
                tabBarRect.anchorMax = new Vector2(0.96f, 0.75f);
                tabBarRect.offsetMin = Vector2.zero;
                tabBarRect.offsetMax = Vector2.zero;
                CreateTab(tabBar.transform, RpgQuestJournalTab.ActiveQuests, "进行中", 0f, 1f / 3f);
                CreateTab(tabBar.transform, RpgQuestJournalTab.CompletedQuests, "已完成", 1f / 3f, 2f / 3f);
                CreateTab(tabBar.transform, RpgQuestJournalTab.FastTravelBeacons, "传送灯标", 2f / 3f, 1f);

                // A masked viewport and height-driven content keep 16+ quests reachable.
                // Previously all rows had zero-sized RectTransforms at the same origin.
                var rowsObj = new GameObject("Rows", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
                rowsObj.transform.SetParent(panelObj.transform, false);
                var rowsRect = rowsObj.GetComponent<RectTransform>();
                rowsRect.anchorMin = new Vector2(0.04f, 0.06f);
                rowsRect.anchorMax = new Vector2(0.96f, 0.65f);
                rowsRect.offsetMin = Vector2.zero;
                rowsRect.offsetMax = Vector2.zero;

                var contentObj = new GameObject("Content", typeof(RectTransform),
                    typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                contentObj.transform.SetParent(rowsObj.transform, false);
                var contentRect = contentObj.GetComponent<RectTransform>();
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.sizeDelta = Vector2.zero;
                var layout = contentObj.GetComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(2, 2, 2, 2);
                layout.spacing = 4f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                contentObj.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                rowScroll = rowsObj.GetComponent<ScrollRect>();
                rowScroll.viewport = rowsRect;
                rowScroll.content = contentRect;
                rowScroll.horizontal = false;
                rowScroll.vertical = true;
                rowScroll.movementType = ScrollRect.MovementType.Clamped;
                rowScroll.scrollSensitivity = 24f;
                rowContainer = contentRect;

                visible = false;
                rootObject.SetActive(false);
                Refresh();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool Show(RpgQuestJournalTab? tab = null)
        {
            if (IsDisposed)
                return false;
            if (tab.HasValue)
                currentTab = tab.Value;
            visible = true;
            rootObject.SetActive(true);
            Refresh();
            return true;
        }

        public bool Hide()
        {
            if (IsDisposed)
                return false;
            visible = false;
            rootObject.SetActive(false);
            return true;
        }

        public bool SetTab(RpgQuestJournalTab tab)
        {
            if (IsDisposed || !Enum.IsDefined(typeof(RpgQuestJournalTab), tab))
                return false;
            currentTab = tab;
            Refresh();
            rowScroll.verticalNormalizedPosition = 1f;
            return true;
        }

        public bool TryClickTrackQuest(string questId)
        {
            if (IsDisposed || string.IsNullOrEmpty(questId) ||
                !questTrackButtons.TryGetValue(questId, out Button btn) ||
                btn == null || !btn.interactable)
                return false;
            btn.onClick.Invoke();
            return true;
        }

        public bool TryClickFastTravel(string markerId)
        {
            if (IsDisposed || string.IsNullOrEmpty(markerId) ||
                !fastTravelButtons.TryGetValue(markerId, out Button btn) ||
                btn == null || !btn.interactable)
                return false;
            btn.onClick.Invoke();
            return true;
        }

        public void Refresh()
        {
            if (IsDisposed)
                return;

            ClearRows();
            RefreshTabAppearance();

            int totalBeacons = 0;
            int unlockedBeacons = 0;
            IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(tracker.MapId);
            for (int i = 0; i < markers.Count; i++)
            {
                if (!markers[i].SupportsFastTravel) continue;
                totalBeacons++;
                if (tracker.IsMarkerDiscovered(markers[i].Id))
                    unlockedBeacons++;
            }

            headerText.text = spec.DisplayNameZh + " · 地标日志与传送灯标";
            bodySummaryText.text = "已发现地标 " + tracker.DiscoveredMarkerCount + "/" + tracker.TotalMarkerCount +
                " | 地标任务 " + tracker.CompletedQuestCount + "/" + tracker.TotalQuestCount +
                " | 快速传送灯标 " + unlockedBeacons + "/" + totalBeacons;

            if (currentTab == RpgQuestJournalTab.FastTravelBeacons)
            {
                BuildFastTravelRows(markers);
                return;
            }

            bool wantCompleted = currentTab == RpgQuestJournalTab.CompletedQuests;
            IReadOnlyList<RpgLandmarkQuestDefinition> quests = RpgWorldLandmarkCatalog.GetQuests(tracker.MapId);
            for (int i = 0; i < quests.Count; i++)
            {
                RpgLandmarkQuestDefinition q = quests[i];
                RpgLandmarkQuestState state = tracker.GetQuestState(q.QuestId);
                bool isDone = state == RpgLandmarkQuestState.Completed;
                if (isDone != wantCompleted)
                    continue;

                int steps = tracker.GetQuestProgress(q.QuestId);
                string label = q.TitleZh + " (" + steps + "/" + q.RequiredSteps + ") — " + q.ObjectiveSummaryZh;
                Button btn = SpawnRow("QuestRow_" + q.QuestId, label,
                    actionLabel: isDone ? "已完成" : (tracker.TrackedQuestId == q.QuestId ? "追踪中" : "追踪"),
                    interactable: !isDone,
                    onClick: () =>
                    {
                        if (tracker.TrySetTrackedQuest(q.QuestId))
                        {
                            onTrackedQuestChanged?.Invoke(q.QuestId);
                            Refresh();
                        }
                    });
                questTrackButtons[q.QuestId] = btn;
            }
        }

        private void BuildFastTravelRows(IReadOnlyList<RpgMapLandmarkMarker> markers)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                RpgMapLandmarkMarker m = markers[i];
                if (!m.SupportsFastTravel)
                    continue;
                bool unlocked = tracker.IsMarkerDiscovered(m.Id);
                string label = m.TitleZh + " (" + m.Tile.x + "," + m.Tile.y + ")";
                Button btn = SpawnRow("BeaconRow_" + m.Id, label,
                    actionLabel: unlocked ? "传送" : "未发现",
                    interactable: unlocked,
                    onClick: () =>
                    {
                        if (tracker.TryGetFastTravelDestination(m.Id, out Vector2Int dest))
                            onFastTravelRequested?.Invoke(m.Id, dest);
                    });
                fastTravelButtons[m.Id] = btn;
            }
        }

        private void CreateTab(Transform tabBar, RpgQuestJournalTab tab, string title, float start, float end)
        {
            var tabObj = new GameObject("Tab_" + tab, typeof(RectTransform), typeof(Image), typeof(Button));
            tabObj.transform.SetParent(tabBar, false);
            var rect = tabObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(start + 0.006f, 0f);
            rect.anchorMax = new Vector2(end - 0.006f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var background = tabObj.GetComponent<Image>();
            tabBackgrounds[tab] = background;
            tabObj.GetComponent<Button>().onClick.AddListener(() => { SetTab(tab); });

            var textObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(tabObj.transform, false);
            var labelRect = textObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = textObj.GetComponent<Text>();
            label.font = defaultFont;
            label.fontSize = 13;
            label.text = title;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.98f, 0.94f, 0.84f, 1f);
            label.raycastTarget = false;
        }

        private void RefreshTabAppearance()
        {
            foreach (KeyValuePair<RpgQuestJournalTab, Image> tab in tabBackgrounds)
                tab.Value.color = tab.Key == currentTab
                    ? new Color(0.26f, 0.35f, 0.46f, 1f)
                    : new Color(0.12f, 0.16f, 0.22f, 1f);
        }

        private Button SpawnRow(string name, string text, string actionLabel,
            bool interactable, Action onClick)
        {
            var rowObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rowObj.transform.SetParent(rowContainer, false);
            var element = rowObj.GetComponent<LayoutElement>();
            element.minHeight = 42f;
            element.preferredHeight = 42f;
            var rowImage = rowObj.GetComponent<Image>();
            rowImage.color = new Color(0.12f, 0.15f, 0.20f, 0.85f);
            rowImage.raycastTarget = false;

            var labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(rowObj.transform, false);
            var labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.02f, 0.10f);
            labelRect.anchorMax = new Vector2(0.77f, 0.90f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var labelText = labelObj.GetComponent<Text>();
            labelText.font = defaultFont;
            labelText.fontSize = 12;
            labelText.text = text;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.verticalOverflow = VerticalWrapMode.Truncate;
            labelText.color = Color.white;
            labelText.raycastTarget = false;

            var btnObj = new GameObject("ActionButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(rowObj.transform, false);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.79f, 0.10f);
            btnRect.anchorMax = new Vector2(0.98f, 0.90f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;
            btnObj.GetComponent<Image>().color = interactable
                ? new Color(0.24f, 0.30f, 0.38f, 1f)
                : new Color(0.15f, 0.17f, 0.20f, 1f);
            var btn = btnObj.GetComponent<Button>();
            btn.interactable = interactable;
            if (onClick != null)
                btn.onClick.AddListener(() => onClick());

            var btnTextObj = new GameObject("ButtonText", typeof(RectTransform), typeof(Text));
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var btnLabelRect = btnTextObj.GetComponent<RectTransform>();
            btnLabelRect.anchorMin = Vector2.zero;
            btnLabelRect.anchorMax = Vector2.one;
            btnLabelRect.offsetMin = Vector2.zero;
            btnLabelRect.offsetMax = Vector2.zero;
            var btnText = btnTextObj.GetComponent<Text>();
            btnText.font = defaultFont;
            btnText.fontSize = 12;
            btnText.text = actionLabel;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = interactable ? new Color(0.98f, 0.90f, 0.56f, 1f) : new Color(0.55f, 0.58f, 0.62f, 1f);
            btnText.raycastTarget = false;

            spawnedRows.Add(rowObj);
            return btn;
        }

        private void ClearRows()
        {
            questTrackButtons.Clear();
            fastTravelButtons.Clear();
            for (int i = 0; i < spawnedRows.Count; i++)
            {
                // In Play Mode Destroy is deferred; hide old children now so they
                // cannot overlap newly rebuilt scroll rows for one rendered frame.
                spawnedRows[i].SetActive(false);
                DestroySafe(spawnedRows[i]);
            }
            spawnedRows.Clear();
        }

        private static void DestroySafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            visible = false;
            ClearRows();
            DestroySafe(rootObject);
        }
    }
}
