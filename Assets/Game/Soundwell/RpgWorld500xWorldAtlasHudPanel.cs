using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Interactive UGUI World Atlas & Camp Haven panel bound to RpgWorld500xSceneBootstrapper.
    /// Displays all four 500x regions' reputation/bounty status, clickable travel buttons
    /// for directly connected regions, and a one-click Camp Rest & Auto-Save button.
    /// </summary>
    public sealed class RpgWorld500xWorldAtlasHudPanel : IDisposable
    {
        private const string EncounterBlockedStatus = "遭遇战进行中，结束后可旅行或休整";
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly RpgWorld500xCampHavenService _campHaven;
        private readonly RpgWorld500xSaveSlotManager _saveSlotManager;
        private readonly GameObject _rootObject;
        private readonly Transform _rowContainer;
        private readonly Text _headerText;
        private readonly Text _statusBannerText;
        private readonly Button _campRestButton;
        private readonly Font _font;
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private readonly Dictionary<RpgWorldMapId, Button> _travelButtons = new Dictionary<RpgWorldMapId, Button>();
        private bool _visible;
        private bool _disposed;

        public bool IsVisible => !IsDisposed && _visible;
        public bool IsDisposed => _disposed || _rootObject == null;
        public int DisplayedRegionRowCount => _spawnedRows.Count;
        public string HeaderText => _headerText != null ? _headerText.text : string.Empty;
        public string StatusBannerText => _statusBannerText != null ? _statusBannerText.text : string.Empty;
        public string LastCampSaveJson { get; private set; } = string.Empty;

        public RpgWorld500xWorldAtlasHudPanel(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper,
            RpgWorld500xCampHavenService campHaven = null,
            RpgWorld500xSaveSlotManager saveSlotManager = null)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _campHaven = campHaven ?? _bootstrapper.CreateCampHavenService();
            _saveSlotManager = saveSlotManager ?? new RpgWorld500xSaveSlotManager();
            _font = ResolveUiFont();

            _rootObject = new GameObject("World500xAtlasHudPanel", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            var canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject panel = CreateBox(
                _rootObject.transform,
                "AtlasModalBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(680f, 420f),
                new Color(0.07f, 0.09f, 0.12f, 0.94f));

            _headerText = CreateText(
                panel.transform,
                "HeaderLabel",
                new Vector2(0.04f, 0.86f), new Vector2(0.68f, 0.96f),
                _font, 16, TextAnchor.MiddleLeft,
                new Color(0.98f, 0.90f, 0.66f, 1f));

            _campRestButton = CreateActionButton(
                panel.transform,
                "CampRestButton",
                new Vector2(0.70f, 0.86f), new Vector2(0.96f, 0.96f),
                "营地休整并存档",
                _font,
                true,
                HandleCampRestClicked);

            _statusBannerText = CreateText(
                panel.transform,
                "StatusBannerLabel",
                new Vector2(0.04f, 0.75f), new Vector2(0.96f, 0.84f),
                _font, 13, TextAnchor.MiddleLeft,
                new Color(0.84f, 0.90f, 0.94f, 1f));

            var rowsGo = new GameObject("RegionRows", typeof(RectTransform));
            rowsGo.transform.SetParent(panel.transform, false);
            var rowsRect = rowsGo.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0.04f, 0.06f);
            rowsRect.anchorMax = new Vector2(0.96f, 0.72f);
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

        public bool TryClickTravelTo(RpgWorldMapId targetMap)
        {
            if (IsDisposed || _bootstrapper.IsDisposed || RejectEncounterAction())
            {
                return false;
            }

            if (!_travelButtons.TryGetValue(targetMap, out Button btn) ||
                btn == null || !btn.interactable)
            {
                return false;
            }

            btn.onClick.Invoke();
            return true;
        }

        public bool TryClickCampRest()
        {
            if (IsDisposed || _bootstrapper.IsDisposed || RejectEncounterAction() ||
                _campRestButton == null || !_campRestButton.interactable)
            {
                return false;
            }

            _campRestButton.onClick.Invoke();
            return true;
        }

        public void Refresh()
        {
            if (IsDisposed || _bootstrapper.IsDisposed)
            {
                return;
            }

            ClearRows();

            bool encounterActive = _bootstrapper.Driver.Facade.Session.Host.Bridge.IsInDangerEncounter;
            _campRestButton.interactable = !encounterActive;
            _campRestButton.GetComponentInChildren<Text>().text = encounterActive ? "战斗中" : "营地休整并存档";
            if (encounterActive)
            {
                _statusBannerText.text = EncounterBlockedStatus;
            }
            else if (_statusBannerText.text == EncounterBlockedStatus)
            {
                _statusBannerText.text = string.Empty;
            }

            RpgWorldMapId currentMap = _bootstrapper.Driver.Facade.CurrentMapId;
            RpgWorld500xAtmosphereSnapshot atmo = _bootstrapper.Driver.Facade.Atmosphere.GetSnapshot(currentMap);
            _headerText.text = "四境世界图册 · 第 " + atmo.DayCount + " 日 " + atmo.FormattedClock + " " + atmo.WeatherDisplayName;

            List<RpgWorld500xRegionAtlasEntry> entries =
                RpgWorld500xWorldAtlasPortalService.BuildWorldAtlasOverview(_bootstrapper.Store);

            const float rowHeight = 56f;
            const float rowSpacing = 8f;
            for (int i = 0; i < entries.Count; i++)
            {
                RpgWorld500xRegionAtlasEntry entry = entries[i];
                bool isCurrent = entry.MapId == currentMap;
                RpgWorld500xRegionRouteDef route = default;
                bool hasDirectRoute = !isCurrent && RpgWorld500xWorldAtlasPortalService.TryFindDirectRoute(
                    currentMap, entry.MapId, out route);

                string rowLabel = entry.DisplayNameZh + "｜" + entry.Reputation.FactionNameZh +
                                  "（" + entry.Reputation.RankNameZh + "）｜悬赏 " +
                                  entry.ClaimedBounties + "/" + entry.TotalBounties;

                string btnLabel = isCurrent
                    ? "当前所在"
                    : (hasDirectRoute ? (encounterActive ? "战斗中" : "启程（" + route.TravelMinutesInGame + "分）") : "未直连");

                RpgWorldMapId capturedTarget = entry.MapId;
                Button travelBtn = SpawnRegionRow(
                    "RegionRow_" + entry.MapId,
                    i,
                    rowHeight,
                    rowSpacing,
                    rowLabel,
                    btnLabel,
                    hasDirectRoute && !encounterActive,
                    () => HandleTravelClicked(capturedTarget));

                _travelButtons[entry.MapId] = travelBtn;
            }
        }

        private bool RejectEncounterAction()
        {
            if (!_bootstrapper.Driver.Facade.Session.Host.Bridge.IsInDangerEncounter)
            {
                return false;
            }

            Refresh();
            return true;
        }

        private void HandleTravelClicked(RpgWorldMapId targetMap)
        {
            if (IsDisposed || _bootstrapper.IsDisposed || RejectEncounterAction())
            {
                return;
            }

            if (RpgWorld500xWorldAtlasPortalService.TryTravelToConnectedRegion(
                _bootstrapper, targetMap, out RpgWorld500xRegionTravelResult travel))
            {
                _statusBannerText.text = travel.ArrivalBannerZh;
                Refresh();
            }
        }

        private void HandleCampRestClicked()
        {
            if (IsDisposed || _bootstrapper.IsDisposed || RejectEncounterAction())
            {
                return;
            }

            RpgWorld500xCampHavenSummary summary = _campHaven.RestAndSyncCampHaven(_bootstrapper);
            LastCampSaveJson = summary.SaveSnapshotJson;
            // Commit the rested world through the same slot manager as the save menu.
            // An exported JSON snapshot alone is not a loadable checkpoint.
            if (_saveSlotManager.TryAutoSave(_bootstrapper, out _, out string error))
                _statusBannerText.text = summary.SummaryZh + "｜已存档";
            else
                _statusBannerText.text = "休整完成，自动存档失败：" + error;
            Refresh();
        }

        private Button SpawnRegionRow(
            string name,
            int index,
            float height,
            float spacing,
            string label,
            string buttonLabel,
            bool interactable,
            Action onClick)
        {
            var rowGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            rowGo.transform.SetParent(_rowContainer, false);
            var rect = rowGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -(index * (height + spacing)));
            rect.sizeDelta = new Vector2(0f, height);
            rowGo.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.20f, 0.88f);

            Text text = CreateText(
                rowGo.transform,
                "Label",
                new Vector2(0.02f, 0.08f), new Vector2(0.72f, 0.92f),
                _font, 13, TextAnchor.MiddleLeft,
                Color.white);
            text.text = label;

            Button btn = CreateActionButton(
                rowGo.transform,
                "TravelButton",
                new Vector2(0.74f, 0.14f), new Vector2(0.98f, 0.86f),
                buttonLabel,
                _font,
                interactable,
                onClick);

            _spawnedRows.Add(rowGo);
            return btn;
        }

        private void ClearRows()
        {
            _travelButtons.Clear();
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

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _visible = false;
            ClearRows();
            DestroySafe(_rootObject);
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

        private static Button CreateActionButton(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string label,
            Font font,
            bool interactable,
            Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = interactable
                ? new Color(0.22f, 0.30f, 0.38f, 0.95f)
                : new Color(0.16f, 0.18f, 0.21f, 0.70f);

            var btn = go.GetComponent<Button>();
            btn.interactable = interactable;
            if (onClick != null)
            {
                btn.onClick.AddListener(() => onClick());
            }

            Text txt = CreateText(
                go.transform,
                "Text",
                Vector2.zero, Vector2.one,
                font, 13, TextAnchor.MiddleCenter,
                interactable ? new Color(0.98f, 0.90f, 0.56f, 1f) : new Color(0.58f, 0.60f, 0.64f, 1f));
            txt.text = label;
            return btn;
        }

        private static Font ResolveUiFont()
        {
            // Atlas copy is currently authored in Chinese regardless of the
            // story locale. Use the existing pinned OFL CJK font, not the
            // Latin-only built-in font that produced blank labels in CI.
            Font font = Resources.Load<Font>("Soundwell/Fonts/SoundwellChinese");
            if (font == null || !font.HasCharacter('图') || !font.HasCharacter('战') ||
                !font.HasCharacter('营'))
            {
                throw new InvalidOperationException("The pinned atlas Chinese font is missing or lacks required glyphs.");
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
