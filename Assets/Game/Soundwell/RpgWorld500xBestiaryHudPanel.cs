using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Interactive UGUI DangerZone Bestiary &amp; Tactical Prep panel bound to
    /// RpgWorld500xSceneBootstrapper. Lists all or unstudied DangerZones on the active
    /// 500x map with threat tier, weakness hints, recommended camp supplies (+ owned count),
    /// remaining study Salt-Silver totals, fast-travel route guidance, and one-click or
    /// batch dossier study unlock.
    /// </summary>
    public sealed class RpgWorld500xBestiaryHudPanel : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly GameObject _rootObject;
        private readonly Transform _rowContainer;
        private readonly Text _headerText;
        private readonly Text _detailText;
        private readonly Font _font;
        private readonly Dictionary<string, string> _supplyNameById = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private readonly Dictionary<string, Button> _studyButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private bool _unstudiedOnly;
        private int _studiedCount;
        private int _totalDossierCount;
        private int _remainingStudySilver;
        private bool _visible;
        private bool _disposed;

        public bool IsVisible => !IsDisposed && _visible;
        public bool IsDisposed => _disposed || _rootObject == null;
        public bool UnstudiedOnly => _unstudiedOnly;
        public int StudiedCount => _studiedCount;
        public int TotalDossierCount => _totalDossierCount;
        public int RemainingStudySilver => _remainingStudySilver;
        public int DisplayedDossierRowCount => _spawnedRows.Count;
        public string HeaderText => _headerText != null ? _headerText.text : string.Empty;
        public string DetailText => _detailText != null ? _detailText.text : string.Empty;

        public RpgWorld500xBestiaryHudPanel(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _font = ResolveUiFont();

            IReadOnlyList<RpgWorld500xCampSupplyItemDef> supplies = RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();
            for (int i = 0; i < supplies.Count; i++)
            {
                _supplyNameById[supplies[i].Id] = supplies[i].DisplayNameZh;
            }

            _rootObject = new GameObject("World500xBestiaryHudPanel", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            var canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 64;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject modal = CreateBox(
                _rootObject.transform,
                "BestiaryModalBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(720f, 460f),
                new Color(0.08f, 0.08f, 0.11f, 0.95f));

            _headerText = CreateText(
                modal.transform,
                "HeaderLabel",
                new Vector2(0.04f, 0.88f), new Vector2(0.96f, 0.96f),
                _font, 15, TextAnchor.MiddleLeft,
                new Color(0.98f, 0.82f, 0.62f, 1f));

            _detailText = CreateText(
                modal.transform,
                "DetailLabel",
                new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.86f),
                _font, 12, TextAnchor.MiddleLeft,
                new Color(0.86f, 0.90f, 0.94f, 1f));

            var rowsGo = new GameObject("DossierRows", typeof(RectTransform));
            rowsGo.transform.SetParent(modal.transform, false);
            var rowsRect = rowsGo.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0.04f, 0.05f);
            rowsRect.anchorMax = new Vector2(0.96f, 0.70f);
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

        public bool TrySetUnstudiedOnly(bool unstudiedOnly)
        {
            if (!IsPanelInteractable())
            {
                return false;
            }

            _unstudiedOnly = unstudiedOnly;
            Refresh();
            return true;
        }

        public bool TryClickStudyDossier(string markerId)
        {
            if (string.IsNullOrEmpty(markerId) ||
                !_studyButtons.TryGetValue(markerId, out Button btn) ||
                !CanInvoke(btn))
            {
                return false;
            }

            btn.onClick.Invoke();
            return true;
        }

        /// <summary>
        /// Idempotently studies all remaining unstudied DangerZone dossiers on the
        /// active map when the panel is visible and not locked by an ancestor CanvasGroup.
        /// </summary>
        public bool TryStudyAllUnstudied(out int studiedCount, out int totalSilverAwarded)
        {
            studiedCount = 0;
            totalSilverAwarded = 0;
            if (!IsPanelInteractable())
            {
                return false;
            }

            RpgWorld500xGrandRuntimeFacade facade = _bootstrapper.Driver.Facade;
            RpgWorldMapId mapId = facade.CurrentMapId;
            List<RpgWorld500xDangerZoneDossier> dossiers =
                facade.ThreatCodex.BuildMapDossiers(facade.Session.Host.Layout);

            for (int i = 0; i < dossiers.Count; i++)
            {
                RpgWorld500xDangerZoneDossier d = dossiers[i];
                if (d.IsStudied)
                {
                    continue;
                }

                if (facade.ThreatCodex.TryRecordDangerZoneStudied(mapId, d.MarkerId, out RpgWorld500xDangerZoneDossier studied))
                {
                    studiedCount++;
                    totalSilverAwarded += studied.Profile.CodexStudySilverBonus;
                }
            }

            if (studiedCount == 0)
            {
                return false;
            }

            _detailText.text = "已批量研读 " + studiedCount + " 份威胁图鉴，入账盐银 +" + totalSilverAwarded + "！";
            _bootstrapper.StatusHud.SyncFromDriver(_bootstrapper.Driver);
            Refresh();
            return true;
        }

        public void Refresh()
        {
            if (IsDisposed || _bootstrapper.IsDisposed)
            {
                return;
            }

            ClearRows();

            RpgWorld500xGrandRuntimeFacade facade = _bootstrapper.Driver.Facade;
            RpgWorld500xMapLayout layout = facade.Session.Host.Layout;
            List<RpgWorld500xDangerZoneDossier> dossiers = facade.ThreatCodex.BuildMapDossiers(layout);

            _studiedCount = 0;
            _totalDossierCount = dossiers.Count;
            _remainingStudySilver = 0;
            for (int i = 0; i < dossiers.Count; i++)
            {
                if (dossiers[i].IsStudied)
                {
                    _studiedCount++;
                }
                else
                {
                    _remainingStudySilver += Math.Max(0, dossiers[i].Profile.CodexStudySilverBonus);
                }
            }

            _headerText.text = layout.Spec.DisplayNameZh + " · 危险区威胁图鉴（已收录 " +
                               _studiedCount + "/" + dossiers.Count +
                               "｜待研读盐银 +" + _remainingStudySilver + "）";

            const float rowHeight = 52f;
            const float spacing = 8f;
            int rowIndex = 0;
            for (int i = 0; i < dossiers.Count; i++)
            {
                RpgWorld500xDangerZoneDossier d = dossiers[i];
                if (_unstudiedOnly && d.IsStudied)
                {
                    continue;
                }

                string supplyName = _supplyNameById.TryGetValue(d.Profile.RecommendedSupplyId, out string nameZh)
                    ? nameZh
                    : d.Profile.RecommendedSupplyId;
                int ownedSupply = facade.BountyAndSupply.GetOwnedSupplyCount(d.Profile.RecommendedSupplyId);

                string label = "[" + GetTierLabelZh(d.Profile.Tier) + "] " + d.Profile.EnemyNameZh +
                               "｜弱点：" + d.Profile.WeaknessHintZh +
                               "｜推荐补给：" + supplyName + "（持有 " + ownedSupply + "）";
                string btnLabel = d.IsStudied ? "已收录" : "研读（+" + d.Profile.CodexStudySilverBonus + "盐银）";

                string capturedMarkerId = d.MarkerId;
                Button btn = SpawnRow(
                    "DossierRow_" + d.MarkerId,
                    rowIndex++,
                    rowHeight,
                    spacing,
                    label,
                    btnLabel,
                    !d.IsStudied,
                    () => HandleStudyClicked(capturedMarkerId));

                _studyButtons[d.MarkerId] = btn;
            }
        }

        private void HandleStudyClicked(string markerId)
        {
            RpgWorld500xGrandRuntimeFacade facade = _bootstrapper.Driver.Facade;
            RpgWorldMapId mapId = facade.CurrentMapId;

            if (facade.ThreatCodex.TryRecordDangerZoneStudied(mapId, markerId, out RpgWorld500xDangerZoneDossier studied))
            {
                string routeSummary = string.Empty;
                if (RpgWorldLandmarkCatalog.TryGetMarker(mapId, markerId, out RpgMapLandmarkMarker marker))
                {
                    RpgWorld500xRoutePlan plan = RpgWorld500xCampHavenService.PlanRouteFromCurrentPosition(
                        _bootstrapper, marker.Tile);
                    routeSummary = "｜路线：" + plan.SummaryZh;
                }

                _detailText.text = "战术建议：" + studied.Profile.CounterplayNoteZh + routeSummary;
                _bootstrapper.StatusHud.SyncFromDriver(_bootstrapper.Driver);
                Refresh();
            }
        }

        private static string GetTierLabelZh(RpgWorld500xThreatTier tier)
        {
            switch (tier)
            {
                case RpgWorld500xThreatTier.HazardBoss: return "危险头目";
                case RpgWorld500xThreatTier.ElitePatrol: return "精英巡逻";
                case RpgWorld500xThreatTier.Skirmisher:
                default: return "游击哨兵";
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
            rowGo.GetComponent<Image>().color = new Color(0.13f, 0.14f, 0.18f, 0.88f);

            Text text = CreateText(
                rowGo.transform,
                "Label",
                new Vector2(0.02f, 0.08f), new Vector2(0.74f, 0.92f),
                _font, 12, TextAnchor.MiddleLeft,
                Color.white);
            text.text = label;

            Button btn = CreateActionButton(
                rowGo.transform,
                "StudyButton",
                new Vector2(0.76f, 0.14f), new Vector2(0.98f, 0.86f),
                buttonLabel,
                _font,
                interactable,
                onClick);

            _spawnedRows.Add(rowGo);
            return btn;
        }

        private bool IsPanelInteractable()
        {
            if (!IsVisible || _bootstrapper.IsDisposed || !_rootObject.activeInHierarchy)
            {
                return false;
            }

            CanvasGroup[] groups = _rootObject.GetComponentsInParent<CanvasGroup>(false);
            for (int i = 0; i < groups.Length; i++)
            {
                if (!groups[i].interactable)
                {
                    return false;
                }

                if (groups[i].ignoreParentGroups)
                {
                    break;
                }
            }

            return true;
        }

        private bool CanInvoke(Button button)
        {
            return IsVisible && !_bootstrapper.IsDisposed && button != null &&
                button.isActiveAndEnabled && button.gameObject.activeInHierarchy && button.IsInteractable();
        }

        private void ClearRows()
        {
            _studyButtons.Clear();
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
                ? new Color(0.28f, 0.22f, 0.20f, 0.95f)
                : new Color(0.16f, 0.18f, 0.21f, 0.70f);

            var btn = go.GetComponent<Button>();
            btn.interactable = interactable;
            if (onClick != null)
            {
                btn.onClick.AddListener(() =>
                {
                    // A retired or hidden dossier row must not grant a study reward.
                    if (CanInvoke(btn))
                        onClick();
                });
            }

            Text txt = CreateText(
                go.transform,
                "Text",
                Vector2.zero, Vector2.one,
                font, 12, TextAnchor.MiddleCenter,
                interactable ? new Color(0.98f, 0.88f, 0.56f, 1f) : new Color(0.58f, 0.60f, 0.64f, 1f));
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