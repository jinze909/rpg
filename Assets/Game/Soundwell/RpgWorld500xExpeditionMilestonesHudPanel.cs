using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Interactive UGUI Cross-Map Expedition Milestones &amp; 4-Region World Completion
    /// panel bound to RpgWorld500xSceneBootstrapper and RpgWorld500xExpeditionMilestoneService.
    /// Displays global 500x world progress across all 4 maps, read-only milestone summary
    /// with next target and claimable Salt-Silver preview, optional unclaimed-only filtering,
    /// and one-click claiming of eligible Salt-Silver milestone bonuses under a zero-leak lifecycle.
    /// </summary>
    public sealed class RpgWorld500xExpeditionMilestonesHudPanel : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly Dictionary<string, int> _localClaimOverlay =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly RpgWorld500xExpeditionMilestoneService _milestoneService;
        private readonly GameObject _rootObject;
        private readonly Canvas _canvas;
        private readonly Transform _rowContainer;
        private readonly Text _headerText;
        private readonly Text _summaryText;
        private readonly Text _statusText;
        private readonly Button _claimAllButton;
        private readonly Font _font;
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private RpgWorld500xMilestoneSummary _lastMilestoneSummary;
        private bool _unclaimedOnly;
        private int _claimableCount;
        private int _claimedCount;
        private bool _visible;
        private bool _disposed;

        public bool IsVisible => !IsDisposed && _visible;
        public bool IsDisposed => _disposed || _rootObject == null;
        public bool UnclaimedOnly => _unclaimedOnly;
        public RpgWorld500xMilestoneSummary LastMilestoneSummary => _lastMilestoneSummary;
        public int ClaimableSaltSilver => _lastMilestoneSummary.UnclaimedSaltSilverReward;
        public string MilestoneSummaryLineZh => _lastMilestoneSummary.SummaryLineZh;
        public int DisplayedMilestoneRowCount => _spawnedRows.Count;
        public int ClaimableCount => _claimableCount;
        public int ClaimedCount => _claimedCount;
        public string HeaderText => _headerText != null ? _headerText.text : string.Empty;
        public string SummaryText => _summaryText != null ? _summaryText.text : string.Empty;
        public string StatusText => _statusText != null ? _statusText.text : string.Empty;

        public RpgWorld500xExpeditionMilestonesHudPanel(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper,
            Func<string, int> getInt = null,
            Action<string, int> setInt = null)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _font = ResolveUiFont();

            Action<RpgWorld500xExpeditionMilestoneDef> onUnlocked = def =>
            {
                if (!_bootstrapper.IsDisposed)
                {
                    _bootstrapper.Driver.Facade.BountyAndSupply.AddSaltSilver(def.SaltSilverReward);
                }
            };

            if (getInt == null && setInt == null)
            {
                // Read and write live save state; never shadow a restored claim flag.
                _milestoneService = _bootstrapper.CreateExpeditionMilestoneService(onUnlocked);
            }
            else if (getInt != null && setInt != null)
            {
                // A fully supplied external store is likewise authoritative.
                _milestoneService = new RpgWorld500xExpeditionMilestoneService(getInt, setInt, onUnlocked);
            }
            else
            {
                // Preserve the standalone partial-delegate fallback for callers
                // without a complete writable/readable store pair.
                Func<string, int> reader = k =>
                {
                    if (_localClaimOverlay.TryGetValue(k, out int localVal))
                    {
                        return localVal;
                    }

                    if (getInt != null)
                    {
                        return getInt(k);
                    }

                    IReadOnlyDictionary<string, int> store = _bootstrapper.Store;
                    return (store != null && store.TryGetValue(k, out int v)) ? v : 0;
                };

                Action<string, int> writer = (k, v) =>
                {
                    _localClaimOverlay[k] = v;
                    setInt?.Invoke(k, v);
                };

                _milestoneService = new RpgWorld500xExpeditionMilestoneService(reader, writer, onUnlocked);
            }

            _rootObject = new GameObject("World500xExpeditionMilestonesHudPanel", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            _canvas = _rootObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 69;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject modal = CreateBox(
                _rootObject.transform,
                "MilestonesModalBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 500f),
                new Color(0.08f, 0.09f, 0.12f, 0.95f));

            _headerText = CreateText(
                modal.transform,
                "HeaderLabel",
                new Vector2(0.04f, 0.89f), new Vector2(0.70f, 0.96f),
                _font, 15, TextAnchor.MiddleLeft,
                new Color(0.98f, 0.86f, 0.56f, 1f));

            _claimAllButton = CreateActionButton(
                modal.transform,
                "ClaimAllButton",
                new Vector2(0.72f, 0.89f), new Vector2(0.96f, 0.96f),
                "一键领取奖励",
                _font,
                interactable: false,
                HandleClaimAllClicked);

            _summaryText = CreateText(
                modal.transform,
                "SummaryLabel",
                new Vector2(0.04f, 0.77f), new Vector2(0.96f, 0.88f),
                _font, 12, TextAnchor.MiddleLeft,
                new Color(0.84f, 0.92f, 0.96f, 1f));

            _statusText = CreateText(
                modal.transform,
                "StatusLabel",
                new Vector2(0.04f, 0.69f), new Vector2(0.96f, 0.76f),
                _font, 12, TextAnchor.MiddleLeft,
                new Color(0.96f, 0.90f, 0.68f, 1f));

            var rowsGo = new GameObject("MilestoneRows", typeof(RectTransform));
            rowsGo.transform.SetParent(modal.transform, false);
            var rowsRect = rowsGo.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0.04f, 0.04f);
            rowsRect.anchorMax = new Vector2(0.96f, 0.67f);
            rowsRect.offsetMin = Vector2.zero;
            rowsRect.offsetMax = Vector2.zero;
            _rowContainer = rowsGo.transform;

            _visible = false;
            _rootObject.SetActive(false);
            Refresh();
        }

        public bool Show()
        {
            if (IsDisposed || _bootstrapper.IsDisposed)
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

        public bool TrySetUnclaimedOnly(bool unclaimedOnly)
        {
            if (!IsPanelInteractable())
            {
                return false;
            }

            _unclaimedOnly = unclaimedOnly;
            Refresh();
            return true;
        }

        public bool TryClickClaimAllEligible()
        {
            if (!CanClaimFromButton())
            {
                return false;
            }

            _claimAllButton.onClick.Invoke();
            return true;
        }

        public void Refresh()
        {
            if (IsDisposed || _bootstrapper.IsDisposed)
            {
                return;
            }

            ClearRows();
            _claimableCount = 0;
            _claimedCount = 0;

            _lastMilestoneSummary = _milestoneService.BuildMilestoneSummary();
            List<RpgWorld500xMilestoneStatus> statuses =
                _milestoneService.BuildStatusList(out RpgWorld500xCrossMapProgressSnapshot progress);

            for (int i = 0; i < statuses.Count; i++)
            {
                if (statuses[i].IsClaimed)
                {
                    _claimedCount++;
                }
                else if (statuses[i].IsSatisfied)
                {
                    _claimableCount++;
                }
            }

            _headerText.text = "四境远征总录（总完成度 " + progress.OverallWorldCompletionPercent +
                               "%｜里程碑 " + _claimedCount + "/" + statuses.Count + "）";

            _summaryText.text = "补给箱 " + progress.LootedCaches + "/" + progress.TotalCaches +
                                "｜捷径 " + progress.UnlockedShortcuts + "/" + progress.TotalShortcuts +
                                "｜野外传闻 " + progress.HeardRumors + "/" + progress.TotalFieldNpcs +
                                "｜风物轶事 " + progress.InspectedStorySpots + "/" + progress.TotalStorySpots + "\n" +
                                "区域悬赏 " + progress.ClaimedBounties + "/" + progress.TotalBounties +
                                "｜堪舆里程碑 " + progress.ClaimedCartographyMilestones + "/" + progress.TotalCartographyMilestones +
                                "｜生死托付阵营 " + progress.SwornFactions + "/" + progress.TotalFactions + "\n" +
                                _lastMilestoneSummary.SummaryLineZh;

            UpdateClaimButtonState();

            const float rowHeight = 32f;
            const float spacing = 5f;
            int rowIndex = 0;

            for (int i = 0; i < statuses.Count; i++)
            {
                RpgWorld500xMilestoneStatus st = statuses[i];
                if (_unclaimedOnly && st.IsClaimed)
                {
                    continue;
                }

                RpgWorld500xExpeditionMilestoneDef d = st.Definition;
                string badge = st.IsClaimed ? "[已领取] " : (st.IsSatisfied ? "[可领取] " : "[进行中] ");
                string line = badge + d.TitleZh + "（" + st.CurrentCount + "/" + d.TargetCount +
                              "）｜" + d.DescriptionZh + "｜奖：盐银 +" + d.SaltSilverReward;

                Color bg = st.IsClaimed
                    ? new Color(0.16f, 0.22f, 0.16f, 0.90f)
                    : (st.IsSatisfied ? new Color(0.26f, 0.23f, 0.14f, 0.94f) : new Color(0.12f, 0.14f, 0.19f, 0.86f));

                SpawnInfoRow("MilestoneRow_" + d.Id, rowIndex++, rowHeight, spacing, line, bg);
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
            DestroySafe(_rootObject);
        }

        private bool CanReceiveInput()
        {
            return IsVisible && _canvas != null && _canvas.isActiveAndEnabled && !_bootstrapper.IsDisposed;
        }

        private bool IsPanelInteractable()
        {
            if (!CanReceiveInput() || !_rootObject.activeInHierarchy)
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

        private bool CanClaimFromButton()
        {
            return CanReceiveInput() && _claimAllButton != null &&
                _claimAllButton.isActiveAndEnabled && _claimAllButton.gameObject.activeInHierarchy &&
                _claimAllButton.IsInteractable();
        }

        private void HandleClaimAllClicked()
        {
            // UnityEvent.Invoke bypasses Selectable's normal input guards.
            if (!CanClaimFromButton())
            {
                return;
            }

            List<RpgWorld500xExpeditionMilestoneDef> claimed = _milestoneService.EvaluateAndClaimAll(out _);
            if (claimed.Count > 0)
            {
                int totalSilver = 0;
                for (int i = 0; i < claimed.Count; i++)
                {
                    totalSilver += claimed[i].SaltSilverReward;
                }

                _bootstrapper.StatusHud.SyncFromDriver(_bootstrapper.Driver);
                _statusText.text = "已领取 " + claimed.Count + " 项远征里程碑，获得盐银 +" + totalSilver +
                                   "（当前余额 " + _bootstrapper.Driver.Facade.BountyAndSupply.SaltSilverBalance + "）";
            }

            Refresh();
        }

        private void UpdateClaimButtonState()
        {
            if (_claimAllButton == null)
            {
                return;
            }

            bool canClaim = _claimableCount > 0;
            _claimAllButton.interactable = canClaim;
            var img = _claimAllButton.GetComponent<Image>();
            if (img != null)
            {
                img.color = canClaim
                    ? new Color(0.34f, 0.28f, 0.14f, 0.96f)
                    : new Color(0.16f, 0.17f, 0.20f, 0.70f);
            }

            Text txt = _claimAllButton.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = canClaim
                    ? "领取奖励（" + _claimableCount + "｜+" + _lastMilestoneSummary.UnclaimedSaltSilverReward + "盐银）"
                    : "暂无可领";
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
                _font, 11, TextAnchor.MiddleLeft,
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
                ? new Color(0.34f, 0.28f, 0.14f, 0.96f)
                : new Color(0.16f, 0.17f, 0.20f, 0.70f);

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
                font, 12, TextAnchor.MiddleCenter,
                Color.white);
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