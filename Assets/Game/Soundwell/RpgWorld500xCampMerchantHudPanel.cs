using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public enum RpgCampMerchantTab : byte
    {
        Supplies = 0,
        Bounties = 1
    }

    /// <summary>
    /// Interactive UGUI Camp Merchant & Regional Bounty Board panel bound to
    /// RpgWorld500xSceneBootstrapper and RpgWorld500xCampHavenService.
    /// Supports switching between reputation-discounted camp supplies and regional
    /// exploration bounties, with live wallet/HUD refresh and zero-leak disposal.
    /// </summary>
    public sealed class RpgWorld500xCampMerchantHudPanel : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly RpgWorld500xCampHavenService _campHaven;
        private readonly GameObject _rootObject;
        private readonly Transform _rowContainer;
        private readonly Text _headerText;
        private readonly Text _feedbackText;
        private readonly Button _suppliesTabButton;
        private readonly Button _bountiesTabButton;
        private readonly Font _font;
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private readonly Dictionary<string, Button> _buyButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private Button _claimBountiesButton;
        private RpgCampMerchantTab _currentTab = RpgCampMerchantTab.Supplies;
        private bool _visible;
        private bool _disposed;

        public RpgCampMerchantTab CurrentTab => _currentTab;
        public bool IsVisible => !IsDisposed && _visible;
        public bool IsDisposed => _disposed || _rootObject == null;
        public int DisplayedRowCount => _spawnedRows.Count;
        public string HeaderText => _headerText != null ? _headerText.text : string.Empty;
        public string FeedbackText => _feedbackText != null ? _feedbackText.text : string.Empty;

        public RpgWorld500xCampMerchantHudPanel(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper,
            RpgWorld500xCampHavenService campHaven)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _campHaven = campHaven ?? throw new ArgumentNullException(nameof(campHaven));
            _font = ResolveUiFont();

            _rootObject = new GameObject("World500xCampMerchantHudPanel", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            var canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 62;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject modal = CreateBox(
                _rootObject.transform,
                "MerchantModalBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(700f, 460f),
                new Color(0.08f, 0.09f, 0.12f, 0.95f));

            _headerText = CreateText(
                modal.transform,
                "HeaderLabel",
                new Vector2(0.04f, 0.88f), new Vector2(0.96f, 0.97f),
                _font, 15, TextAnchor.MiddleLeft,
                new Color(0.98f, 0.90f, 0.66f, 1f));

            _suppliesTabButton = CreateActionButton(
                modal.transform,
                "SuppliesTabButton",
                new Vector2(0.04f, 0.78f), new Vector2(0.32f, 0.86f),
                "营地补给兑换",
                _font,
                true,
                () => SetTab(RpgCampMerchantTab.Supplies));

            _bountiesTabButton = CreateActionButton(
                modal.transform,
                "BountiesTabButton",
                new Vector2(0.34f, 0.78f), new Vector2(0.62f, 0.86f),
                "区域悬赏契约",
                _font,
                true,
                () => SetTab(RpgCampMerchantTab.Bounties));

            _feedbackText = CreateText(
                modal.transform,
                "FeedbackLabel",
                new Vector2(0.04f, 0.69f), new Vector2(0.96f, 0.77f),
                _font, 13, TextAnchor.MiddleLeft,
                new Color(0.84f, 0.91f, 0.94f, 1f));

            var rowsGo = new GameObject("MerchantRows", typeof(RectTransform));
            rowsGo.transform.SetParent(modal.transform, false);
            var rowsRect = rowsGo.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0.04f, 0.05f);
            rowsRect.anchorMax = new Vector2(0.96f, 0.67f);
            rowsRect.offsetMin = Vector2.zero;
            rowsRect.offsetMax = Vector2.zero;
            _rowContainer = rowsGo.transform;

            _visible = false;
            _rootObject.SetActive(false);
            Refresh();
        }

        public bool Show(RpgCampMerchantTab? tab = null)
        {
            if (IsDisposed)
            {
                return false;
            }

            if (tab.HasValue)
            {
                _currentTab = tab.Value;
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

        public bool SetTab(RpgCampMerchantTab tab)
        {
            if (IsDisposed || !Enum.IsDefined(typeof(RpgCampMerchantTab), tab))
            {
                return false;
            }

            _currentTab = tab;
            Refresh();
            return true;
        }

        public bool TryClickBuySupply(string itemId)
        {
            if (IsDisposed || string.IsNullOrEmpty(itemId) ||
                !_buyButtons.TryGetValue(itemId, out Button btn) ||
                btn == null || !btn.interactable)
            {
                return false;
            }

            btn.onClick.Invoke();
            return true;
        }

        public bool TryClickClaimBounties()
        {
            if (IsDisposed || _claimBountiesButton == null || !_claimBountiesButton.interactable)
            {
                return false;
            }

            _claimBountiesButton.onClick.Invoke();
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
            RpgWorldMapId mapId = facade.CurrentMapId;
            RpgRegionalReputationSnapshot rep = _campHaven.Reputation.GetSnapshot(mapId);
            int balance = facade.BountyAndSupply.SaltSilverBalance;

            _headerText.text = rep.FactionNameZh + "（" + rep.RankNameZh + " · 折扣 " +
                               rep.CampSupplyDiscountPercent + "%）｜盐银余额 " + balance;

            if (_currentTab == RpgCampMerchantTab.Supplies)
            {
                BuildSupplyRows(mapId, facade.BountyAndSupply, balance);
            }
            else
            {
                BuildBountyRows(mapId, facade);
            }
        }

        private void BuildSupplyRows(
            RpgWorldMapId mapId,
            RpgWorld500xFieldBountyAndSupplyService supplyService,
            int balance)
        {
            IReadOnlyList<RpgWorld500xCampSupplyItemDef> supplies = RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();
            const float rowHeight = 40f;
            const float spacing = 6f;

            for (int i = 0; i < supplies.Count; i++)
            {
                RpgWorld500xCampSupplyItemDef item = supplies[i];
                int discountedCost = _campHaven.GetDiscountedUnitCost(mapId, item.Id);
                int remaining = supplyService.GetRemainingStock(item.Id);
                bool canBuy = remaining > 0 && balance >= discountedCost;

                string label = item.DisplayNameZh + " — " + item.EffectSummaryZh +
                               "（单价 " + discountedCost + " 盐银 · 余 " + remaining + "）";
                string btnLabel = remaining <= 0 ? "已售罄" : (canBuy ? "兑换×1" : "盐银不足");

                string capturedId = item.Id;
                string capturedName = item.DisplayNameZh;
                Button btn = SpawnRow(
                    "SupplyRow_" + item.Id,
                    i,
                    rowHeight,
                    spacing,
                    label,
                    btnLabel,
                    canBuy,
                    () => HandleBuySupplyClicked(capturedId, capturedName));

                _buyButtons[item.Id] = btn;
            }
        }

        private void BuildBountyRows(RpgWorldMapId mapId, RpgWorld500xGrandRuntimeFacade facade)
        {
            RpgWorld500xMapCompletionSummary summary = facade.GetCompletionSummary();
            IReadOnlyList<RpgWorld500xFieldBountyDef> bounties = RpgWorld500xBountyAndSupplyCatalog.GetBounties(mapId);
            const float rowHeight = 52f;
            const float spacing = 8f;
            bool anyReady = false;

            for (int i = 0; i < bounties.Count; i++)
            {
                RpgWorld500xFieldBountyDef b = bounties[i];
                bool claimed = facade.BountyAndSupply.IsBountyClaimed(b);
                int progress = RpgWorld500xFieldBountyAndSupplyService.EvaluateProgressCount(b, summary);
                bool ready = !claimed && progress >= b.TargetCount;
                if (ready)
                {
                    anyReady = true;
                }

                string label = b.TitleZh + " (" + Math.Min(progress, b.TargetCount) + "/" + b.TargetCount +
                               ")｜奖励 盐银+" + b.SaltSilverReward + " — " + b.DescriptionZh;
                string statusText = claimed ? "已领取" : (ready ? "可领取" : "进行中");

                SpawnRow(
                    "BountyRow_" + b.Id,
                    i,
                    rowHeight,
                    spacing,
                    label,
                    statusText,
                    false,
                    null);
            }

            _claimBountiesButton = SpawnRow(
                "ClaimAllBountiesRow",
                bounties.Count,
                44f,
                spacing,
                "一次性结算本区域全部已完成悬赏契约",
                anyReady ? "领取已完成悬赏" : "暂无可领",
                anyReady,
                HandleClaimBountiesClicked);
        }

        private void HandleBuySupplyClicked(string itemId, string itemNameZh)
        {
            RpgWorldMapId mapId = _bootstrapper.Driver.Facade.CurrentMapId;
            RpgCampSupplyPurchaseStatus status =
                _campHaven.TryPurchaseDiscountedSupply(mapId, itemId, 1, out int charged);
            if (status == RpgCampSupplyPurchaseStatus.Succeeded)
            {
                _feedbackText.text = "已兑换【" + itemNameZh + "】×1（消耗盐银 " + charged + "）";
                _bootstrapper.StatusHud.SyncFromDriver(_bootstrapper.Driver);
                Refresh();
            }
            else
            {
                _feedbackText.text = "兑换失败：" + status;
            }
        }

        private void HandleClaimBountiesClicked()
        {
            RpgWorld500xGrandRuntimeFacade facade = _bootstrapper.Driver.Facade;
            RpgWorld500xMapCompletionSummary summary = facade.GetCompletionSummary();
            List<RpgWorld500xFieldBountyDef> claimed = facade.BountyAndSupply.ClaimCompletedBounties(summary);
            if (claimed.Count > 0)
            {
                int totalSilver = 0;
                for (int i = 0; i < claimed.Count; i++)
                {
                    totalSilver += claimed[i].SaltSilverReward;
                }

                _feedbackText.text = "已结算 " + claimed.Count + " 项悬赏，获得盐银 +" + totalSilver;
                _bootstrapper.StatusHud.SyncFromDriver(_bootstrapper.Driver);
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
            rowGo.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.20f, 0.88f);

            Text text = CreateText(
                rowGo.transform,
                "Label",
                new Vector2(0.02f, 0.08f), new Vector2(0.75f, 0.92f),
                _font, 12, TextAnchor.MiddleLeft,
                Color.white);
            text.text = label;

            Button btn = CreateActionButton(
                rowGo.transform,
                "ActionButton",
                new Vector2(0.77f, 0.14f), new Vector2(0.98f, 0.86f),
                buttonLabel,
                _font,
                interactable,
                onClick);

            _spawnedRows.Add(rowGo);
            return btn;
        }

        private void ClearRows()
        {
            _buyButtons.Clear();
            _claimBountiesButton = null;
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
                font, 12, TextAnchor.MiddleCenter,
                interactable ? new Color(0.98f, 0.90f, 0.56f, 1f) : new Color(0.58f, 0.60f, 0.64f, 1f));
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
