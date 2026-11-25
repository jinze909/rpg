using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Interactive UGUI Intra-Map Fast-Travel Beacon Warp panel bound to
    /// RpgWorld500xSceneBootstrapper. Lists all fast-travel beacons on the active
    /// 500x map with discovery status, direct-walk route &amp; DangerZone estimates
    /// via RpgWorld500xFastTravelRoutePlanner, and one-click beacon warp.
    /// </summary>
    public sealed class RpgWorld500xFastTravelBeaconHudPanel : IDisposable
    {
        private const string EncounterBlockedStatus = "遭遇战进行中，结束后可传送";
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly GameObject _rootObject;
        private readonly Transform _rowContainer;
        private readonly Text _headerText;
        private readonly Text _statusText;
        private readonly Font _font;
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private readonly Dictionary<string, Button> _warpButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        private int _unlockedBeaconCount;
        private bool _visible;
        private bool _disposed;

        public bool IsVisible => !IsDisposed && _visible;
        public bool IsDisposed => _disposed || _rootObject == null;
        public int DisplayedBeaconRowCount => _spawnedRows.Count;
        public int UnlockedBeaconCount => _unlockedBeaconCount;
        public string HeaderText => _headerText != null ? _headerText.text : string.Empty;
        public string StatusText => _statusText != null ? _statusText.text : string.Empty;

        public RpgWorld500xFastTravelBeaconHudPanel(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _font = ResolveUiFont();

            _rootObject = new GameObject("World500xFastTravelBeaconHudPanel", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            var canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 66;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject modal = CreateBox(
                _rootObject.transform,
                "BeaconWarpModalBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(720f, 460f),
                new Color(0.07f, 0.09f, 0.13f, 0.95f));

            _headerText = CreateText(
                modal.transform,
                "HeaderLabel",
                new Vector2(0.04f, 0.88f), new Vector2(0.96f, 0.96f),
                _font, 15, TextAnchor.MiddleLeft,
                new Color(0.76f, 0.92f, 1f, 1f));

            _statusText = CreateText(
                modal.transform,
                "StatusLabel",
                new Vector2(0.04f, 0.76f), new Vector2(0.96f, 0.87f),
                _font, 12, TextAnchor.MiddleLeft,
                new Color(0.88f, 0.92f, 0.96f, 1f));

            var rowsGo = new GameObject("BeaconRows", typeof(RectTransform));
            rowsGo.transform.SetParent(modal.transform, false);
            var rowsRect = rowsGo.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0.04f, 0.05f);
            rowsRect.anchorMax = new Vector2(0.96f, 0.74f);
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

        public bool TryClickWarp(string markerId)
        {
            if (!IsVisible || _bootstrapper.IsDisposed || string.IsNullOrEmpty(markerId))
            {
                return false;
            }

            if (_bootstrapper.Driver.Facade.Session.Host.Bridge.IsInDangerEncounter)
            {
                Refresh();
                return false;
            }

            if (!_warpButtons.TryGetValue(markerId, out Button btn) ||
                !CanInvoke(btn))
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
            _unlockedBeaconCount = 0;

            RpgWorld500xGrandRuntimeFacade facade = _bootstrapper.Driver.Facade;
            RpgWorld500xExplorationHost host = facade.Session.Host;
            RpgWorldMapId mapId = facade.CurrentMapId;
            Vector2 playerPos = host.PlayerPosition;
            RpgMapExplorationTracker tracker = host.Tracker;
            bool encounterActive = host.Bridge.IsInDangerEncounter;
            if (encounterActive)
            {
                _statusText.text = EncounterBlockedStatus;
            }
            else if (_statusText.text == EncounterBlockedStatus)
            {
                _statusText.text = string.Empty;
            }

            IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(mapId);
            var beacons = new List<RpgMapLandmarkMarker>();
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i].SupportsFastTravel)
                {
                    beacons.Add(markers[i]);
                    if (tracker.IsMarkerDiscovered(markers[i].Id))
                    {
                        _unlockedBeaconCount++;
                    }
                }
            }

            _headerText.text = host.Layout.Spec.DisplayNameZh + " · 区域信标快速传送（已点亮 " +
                               _unlockedBeaconCount + "/" + beacons.Count + "）";

            const float rowHeight = 46f;
            const float spacing = 6f;

            for (int i = 0; i < beacons.Count; i++)
            {
                RpgMapLandmarkMarker beacon = beacons[i];
                bool unlocked = tracker.IsMarkerDiscovered(beacon.Id);
                RpgWorld500xRoutePlan plan = RpgWorld500xFastTravelRoutePlanner.PlanRoute(
                    mapId,
                    playerPos,
                    beacon.Tile,
                    tracker: null);

                int distTiles = Mathf.CeilToInt(plan.DirectWalkTiles);
                string dangerNote = plan.CrossesDangerZone ? " · 直行途经危险区" : string.Empty;
                string stateTag = unlocked ? "[已点亮] " : "[未探索] ";
                string label = stateTag + beacon.TitleZh + "（坐标 " + beacon.Tile.x + "," + beacon.Tile.y +
                               "｜直线 " + distTiles + " 格" + dangerNote + "）";
                string btnLabel = !unlocked ? "未解锁" : (encounterActive ? "战斗中" : "立即传送");

                string capturedId = beacon.Id;
                string capturedTitle = beacon.TitleZh;
                Button btn = SpawnRow(
                    "BeaconRow_" + beacon.Id,
                    i,
                    rowHeight,
                    spacing,
                    label,
                    btnLabel,
                    unlocked && !encounterActive,
                    () => HandleWarpClicked(capturedId, capturedTitle));

                _warpButtons[beacon.Id] = btn;
            }
        }

        private bool CanInvoke(Button button)
        {
            return IsVisible && !_bootstrapper.IsDisposed && button != null &&
                   button.isActiveAndEnabled && button.gameObject.activeInHierarchy && button.IsInteractable();
        }

        private void HandleWarpClicked(string markerId, string markerTitleZh)
        {
            if (!IsVisible || _bootstrapper.IsDisposed)
            {
                return;
            }

            RpgWorld500xGrandRuntimeFacade facade = _bootstrapper.Driver.Facade;
            RpgWorld500xExplorationHost host = facade.Session.Host;

            // A previously rendered button may still receive a click before the next refresh.
            if (host.Bridge.IsInDangerEncounter)
            {
                Refresh();
                return;
            }

            if (host.TryFastTravelToBeacon(markerId, out RpgFastTravelResult _))
            {
                Vector2 newPos = host.PlayerPosition;
                facade.PropSpawner.UpdateFocus(newPos, RpgExpandedWorldScale.DefaultStreamingRadiusChunks);
                facade.DistrictTracker.RecordPlayerTile(newPos, RpgExpandedWorldScale.DefaultStreamingRadiusChunks, out _);
                _bootstrapper.Driver.SyncAfterExternalPositionChange();
                _bootstrapper.StatusHud.SyncFromDriver(_bootstrapper.Driver);
                _statusText.text = "已传送至【" + markerTitleZh + "】（坐标 " +
                                   host.CurrentTile.x + "," + host.CurrentTile.y + "）";
                Refresh();
            }
        }

        private Button SpawnRow(
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
            rowGo.GetComponent<Image>().color = interactable
                ? new Color(0.12f, 0.16f, 0.22f, 0.90f)
                : new Color(0.12f, 0.12f, 0.15f, 0.80f);

            Text text = CreateText(
                rowGo.transform,
                "Label",
                new Vector2(0.02f, 0.08f), new Vector2(0.76f, 0.92f),
                _font, 12, TextAnchor.MiddleLeft,
                Color.white);
            text.text = label;

            Button btn = CreateActionButton(
                rowGo.transform,
                "WarpButton",
                new Vector2(0.78f, 0.14f), new Vector2(0.98f, 0.86f),
                buttonLabel,
                _font,
                interactable,
                onClick);

            _spawnedRows.Add(rowGo);
            return btn;
        }

        private void ClearRows()
        {
            _warpButtons.Clear();
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

        private Button CreateActionButton(
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
                ? new Color(0.18f, 0.34f, 0.46f, 0.96f)
                : new Color(0.16f, 0.17f, 0.20f, 0.70f);

            var btn = go.GetComponent<Button>();
            btn.interactable = interactable;
            if (onClick != null)
            {
                btn.onClick.AddListener(() =>
                {
                    if (CanInvoke(btn))
                    {
                        onClick();
                    }
                });
            }

            Text txt = CreateText(
                go.transform,
                "Text",
                Vector2.zero, Vector2.one,
                font, 12, TextAnchor.MiddleCenter,
                interactable ? new Color(0.88f, 0.96f, 1f, 1f) : new Color(0.56f, 0.58f, 0.62f, 1f));
            txt.text = label;
            return btn;
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
