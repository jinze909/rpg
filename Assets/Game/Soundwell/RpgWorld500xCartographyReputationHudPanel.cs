using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Interactive UGUI Cartography & Regional Reputation panel bound to
    /// RpgWorld500xSceneBootstrapper. Lets players switch across all 4 500x-area
    /// regions to inspect faction reputation ranks, camp supply discounts, Sworn
    /// honor titles, 15-district coverage, and 25%/50%/100% cartography milestones.
    /// </summary>
    public sealed class RpgWorld500xCartographyReputationHudPanel : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly GameObject _rootObject;
        private readonly Transform _tabContainer;
        private readonly Transform _rowContainer;
        private readonly Text _headerText;
        private readonly Text _summaryText;
        private readonly Font _font;
        private readonly Dictionary<RpgWorldMapId, Button> _regionTabButtons =
            new Dictionary<RpgWorldMapId, Button>();
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private RpgWorldMapId _selectedMapId;
        private bool _visible;
        private bool _disposed;

        public bool IsVisible => !IsDisposed && _visible;
        public bool IsDisposed => _disposed || _rootObject == null;
        public RpgWorldMapId SelectedMapId => _selectedMapId;
        public int DisplayedRowCount => _spawnedRows.Count;
        public string HeaderText => _headerText != null ? _headerText.text : string.Empty;
        public string SummaryText => _summaryText != null ? _summaryText.text : string.Empty;

        public RpgWorld500xCartographyReputationHudPanel(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _font = ResolveUiFont();
            _selectedMapId = _bootstrapper.IsDisposed
                ? RpgWorldMapId.Soundwell
                : _bootstrapper.Driver.Facade.CurrentMapId;

            _rootObject = new GameObject("World500xCartographyReputationHudPanel", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            var canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 65;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject modal = CreateBox(
                _rootObject.transform,
                "CartographyReputationModalBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(740f, 490f),
                new Color(0.07f, 0.09f, 0.12f, 0.95f));

            _headerText = CreateText(
                modal.transform,
                "HeaderLabel",
                new Vector2(0.04f, 0.89f), new Vector2(0.96f, 0.96f),
                _font, 15, TextAnchor.MiddleLeft,
                new Color(0.96f, 0.86f, 0.58f, 1f));

            var tabsGo = new GameObject("RegionTabs", typeof(RectTransform));
            tabsGo.transform.SetParent(modal.transform, false);
            var tabsRect = tabsGo.GetComponent<RectTransform>();
            tabsRect.anchorMin = new Vector2(0.04f, 0.80f);
            tabsRect.anchorMax = new Vector2(0.96f, 0.88f);
            tabsRect.offsetMin = Vector2.zero;
            tabsRect.offsetMax = Vector2.zero;
            _tabContainer = tabsGo.transform;

            BuildRegionTabs();

            _summaryText = CreateText(
                modal.transform,
                "SummaryLabel",
                new Vector2(0.04f, 0.67f), new Vector2(0.96f, 0.79f),
                _font, 12, TextAnchor.MiddleLeft,
                new Color(0.84f, 0.92f, 0.96f, 1f));

            var rowsGo = new GameObject("DistrictAndMilestoneRows", typeof(RectTransform));
            rowsGo.transform.SetParent(modal.transform, false);
            var rowsRect = rowsGo.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0.04f, 0.04f);
            rowsRect.anchorMax = new Vector2(0.96f, 0.65f);
            rowsRect.offsetMin = Vector2.zero;
            rowsRect.offsetMax = Vector2.zero;
            _rowContainer = rowsGo.transform;

            _visible = false;
            _rootObject.SetActive(false);
            Refresh();
        }

        public bool Show()
        {
            if (IsDisposed)
            {
                return false;
            }

            if (!_bootstrapper.IsDisposed)
            {
                _selectedMapId = _bootstrapper.Driver.Facade.CurrentMapId;
            }

            _visible = true;
            _rootObject.SetActive(true);
            Refresh();
            return true;
        }

        public bool Hide()
        {
            if (IsDisposed)
            {
                return false;
            }

            _visible = false;
            _rootObject.SetActive(false);
            return true;
        }

        public bool TrySelectRegion(RpgWorldMapId mapId)
        {
            if (IsDisposed || !_regionTabButtons.TryGetValue(mapId, out Button btn) || btn == null)
            {
                return false;
            }

            btn.onClick.Invoke();
            return true;
        }

        public void Refresh()
        {
            if (IsDisposed || _bootstrapper.IsDisposed)
            {
                return;
            }

            ClearRows();

            IReadOnlyDictionary<string, int> store = _bootstrapper.Store;
            Func<string, int> read = k => (store != null && store.TryGetValue(k, out int v)) ? v : 0;
            var repService = new RpgWorld500xRegionalReputationService(read);
            RpgRegionalReputationSnapshot rep = repService.GetSnapshot(_selectedMapId);

            RpgWorld500xGrandRuntimeFacade facade = _bootstrapper.Driver.Facade;
            bool isCurrentMap = facade.CurrentMapId == _selectedMapId;
            string mapNameZh = ResolveMapDisplayNameZh(_selectedMapId);
            string honorSegment = string.IsNullOrEmpty(rep.HonorTitleZh)
                ? string.Empty
                : "｜称号【" + rep.HonorTitleZh + "】";

            _headerText.text = mapNameZh + (isCurrentMap ? " [当前所在]" : string.Empty) +
                               " · " + rep.FactionNameZh + "（" + rep.RankNameZh + " " + rep.Points + " 点" +
                               "｜营地折扣 -" + rep.CampSupplyDiscountPercent + "%" + honorSegment + "）";

            UpdateTabHighlight();

            RpgWorld500xCartographyMilestone[] milestones = RpgWorld500xDistrictCatalog.GetMilestones(_selectedMapId);
            int claimedMilestones = 0;
            for (int i = 0; i < milestones.Length; i++)
            {
                if (read(milestones[i].MakinomKey) == 1)
                {
                    claimedMilestones++;
                }
            }

            string nextRankText = rep.PointsToNextRank > 0
                ? "距下一声望阶差 " + rep.PointsToNextRank + " 点"
                : "已达最高声望阶";

            if (isCurrentMap)
            {
                RpgWorld500xMapCompletionSummary comp = facade.GetCompletionSummary();
                _summaryText.text = "综合堪舆度：" + comp.OverallCompletionPercent + "%（区块 " +
                                    comp.VisitedChunks + "/" + comp.TotalChunks +
                                    "｜地标 " + comp.DiscoveredMarkers + "/" + comp.TotalMarkers +
                                    "｜暗藏补给 " + comp.LootedCaches + "/" + comp.TotalCaches +
                                    "｜捷径 " + comp.UnlockedShortcuts + "/" + comp.TotalShortcuts +
                                    "｜轶事点 " + comp.InspectedStorySpots + "/" + comp.TotalStorySpots + "）\n" +
                                    "堪舆里程碑：" + claimedMilestones + "/" + milestones.Length + "｜" + nextRankText;
            }
            else
            {
                _summaryText.text = "非当前所在区域（切换前往后可实时统计区块迷雾覆盖）\n" +
                                    "已领堪舆里程碑：" + claimedMilestones + "/" + milestones.Length + "｜" + nextRankText;
            }

            IReadOnlyList<RpgWorld500xDistrictDef> districts = RpgWorld500xDistrictCatalog.GetDistricts(_selectedMapId);
            string activeDistrictId = isCurrentMap ? facade.DistrictTracker.CurrentDistrictId : string.Empty;

            const float rowHeight = 32f;
            const float spacing = 5f;
            int rowIndex = 0;

            for (int i = 0; i < districts.Count; i++)
            {
                RpgWorld500xDistrictDef d = districts[i];
                bool isHere = isCurrentMap && string.Equals(d.Id, activeDistrictId, StringComparison.Ordinal);
                string tag = isHere ? "[所在片区] " : "[片区] ";
                string line = tag + d.DisplayNameZh + "（范围 " + d.BoundsTiles.width + "×" + d.BoundsTiles.height + " 格）";
                SpawnInfoRow(
                    "DistrictRow_" + d.Id,
                    rowIndex++,
                    rowHeight,
                    spacing,
                    line,
                    isHere ? new Color(0.18f, 0.28f, 0.22f, 0.92f) : new Color(0.12f, 0.14f, 0.19f, 0.88f));
            }

            for (int i = 0; i < milestones.Length; i++)
            {
                RpgWorld500xCartographyMilestone m = milestones[i];
                bool claimed = read(m.MakinomKey) == 1;
                string status = claimed ? "已领取" : "未达成";
                string line = "[堪舆里程碑 · " + status + "] " + m.TitleZh + "｜奖励：盐银 +" + m.SaltSilverReward;
                SpawnInfoRow(
                    "MilestoneRow_" + m.ThresholdPercent,
                    rowIndex++,
                    rowHeight,
                    spacing,
                    line,
                    claimed ? new Color(0.18f, 0.22f, 0.16f, 0.90f) : new Color(0.14f, 0.13f, 0.18f, 0.86f));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _visible = false;
            ClearRows();
            _regionTabButtons.Clear();
            DestroySafe(_rootObject);
        }

        private void BuildRegionTabs()
        {
            RpgExpandedMapSpec[] specs = RpgExpandedWorldScale.AllSpecs;
            float width = specs.Length > 0 ? 1f / specs.Length : 1f;
            for (int i = 0; i < specs.Length; i++)
            {
                RpgExpandedMapSpec spec = specs[i];
                RpgWorldMapId capturedId = spec.MapId;
                float xMin = i * width;
                float xMax = (i + 1) * width;

                var go = new GameObject("Tab_" + spec.MapId, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_tabContainer, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(xMin + 0.006f, 0.05f);
                rect.anchorMax = new Vector2(xMax - 0.006f, 0.95f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var btn = go.GetComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    _selectedMapId = capturedId;
                    Refresh();
                });

                Text txt = CreateText(
                    go.transform,
                    "Label",
                    Vector2.zero, Vector2.one,
                    _font, 12, TextAnchor.MiddleCenter,
                    Color.white);
                txt.text = spec.DisplayNameZh;

                _regionTabButtons[spec.MapId] = btn;
            }
        }

        private void UpdateTabHighlight()
        {
            foreach (KeyValuePair<RpgWorldMapId, Button> kv in _regionTabButtons)
            {
                if (kv.Value == null)
                {
                    continue;
                }

                var img = kv.Value.GetComponent<Image>();
                if (img != null)
                {
                    img.color = kv.Key == _selectedMapId
                        ? new Color(0.24f, 0.36f, 0.48f, 0.96f)
                        : new Color(0.14f, 0.16f, 0.21f, 0.86f);
                }
            }
        }

        private void SpawnInfoRow(
            string name,
            int index,
            float height,
            float spacing,
            string label,
            Color background)
        {
            var rowGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            rowGo.transform.SetParent(_rowContainer, false);
            var rect = rowGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -(index * (height + spacing)));
            rect.sizeDelta = new Vector2(0f, height);
            rowGo.GetComponent<Image>().color = background;

            Text text = CreateText(
                rowGo.transform,
                "Label",
                new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.95f),
                _font, 12, TextAnchor.MiddleLeft,
                Color.white);
            text.text = label;

            _spawnedRows.Add(rowGo);
        }

        private void ClearRows()
        {
            for (int i = 0; i < _spawnedRows.Count; i++)
            {
                GameObject row = _spawnedRows[i];
                if (row != null)
                {
                    row.SetActive(false);
                    DestroySafe(row);
                }
            }

            _spawnedRows.Clear();
        }

        private static string ResolveMapDisplayNameZh(RpgWorldMapId mapId)
        {
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                if (spec.MapId == mapId)
                {
                    return spec.DisplayNameZh;
                }
            }

            return mapId.ToString();
        }

        private static GameObject CreateBox(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Font font,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            return text;
        }

        private static Font ResolveUiFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }

        private static void DestroySafe(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }
}
