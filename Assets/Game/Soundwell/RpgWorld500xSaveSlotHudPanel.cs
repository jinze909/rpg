using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Interactive UGUI Multi-Slot Save/Load panel bound to RpgWorld500xSceneBootstrapper
    /// and RpgWorld500xSaveSlotManager. Displays AutoSave + 3 manual expedition slots
    /// with region, clock, Salt-Silver, timestamp, corruption badges, and one-click
    /// Save / Load / Delete buttons under a zero-leak IDisposable lifecycle.
    /// </summary>
    public sealed class RpgWorld500xSaveSlotHudPanel : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly RpgWorld500xSaveSlotManager _slotManager;
        private readonly GameObject _rootObject;
        private readonly Transform _rowContainer;
        private readonly Text _headerText;
        private readonly Text _statusText;
        private readonly Font _font;
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private readonly Dictionary<RpgWorld500xSaveSlotId, Button> _saveButtons =
            new Dictionary<RpgWorld500xSaveSlotId, Button>();
        private readonly Dictionary<RpgWorld500xSaveSlotId, Button> _loadButtons =
            new Dictionary<RpgWorld500xSaveSlotId, Button>();
        private readonly Dictionary<RpgWorld500xSaveSlotId, Button> _deleteButtons =
            new Dictionary<RpgWorld500xSaveSlotId, Button>();
        private readonly Dictionary<RpgWorld500xSaveSlotId, string> _slotRevisions =
            new Dictionary<RpgWorld500xSaveSlotId, string>();
        private bool _visible;
        private bool _disposed;

        public bool IsVisible => !IsDisposed && _visible;
        public bool IsDisposed => _disposed || _rootObject == null;
        public int DisplayedSlotRowCount => _spawnedRows.Count;
        public string HeaderText => _headerText != null ? _headerText.text : string.Empty;
        public string StatusText => _statusText != null ? _statusText.text : string.Empty;

        public RpgWorld500xSaveSlotHudPanel(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper,
            RpgWorld500xSaveSlotManager slotManager = null)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _slotManager = slotManager ?? new RpgWorld500xSaveSlotManager();
            _font = ResolveUiFont();

            _rootObject = new GameObject("World500xSaveSlotHudPanel", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            var canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 67;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject modal = CreateBox(
                _rootObject.transform,
                "SaveSlotModalBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 440f),
                new Color(0.08f, 0.09f, 0.12f, 0.95f));

            _headerText = CreateText(
                modal.transform,
                "HeaderLabel",
                new Vector2(0.04f, 0.88f), new Vector2(0.96f, 0.96f),
                _font, 15, TextAnchor.MiddleLeft,
                new Color(0.96f, 0.88f, 0.62f, 1f));

            _statusText = CreateText(
                modal.transform,
                "StatusLabel",
                new Vector2(0.04f, 0.76f), new Vector2(0.96f, 0.87f),
                _font, 12, TextAnchor.MiddleLeft,
                new Color(0.84f, 0.92f, 0.96f, 1f));

            var rowsGo = new GameObject("SaveSlotRows", typeof(RectTransform));
            rowsGo.transform.SetParent(modal.transform, false);
            var rowsRect = rowsGo.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0.04f, 0.06f);
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

        public bool TryClickSave(RpgWorld500xSaveSlotId slotId)
        {
            return TryInvokeButton(_saveButtons, slotId);
        }

        public bool TryClickLoad(RpgWorld500xSaveSlotId slotId)
        {
            return TryInvokeButton(_loadButtons, slotId);
        }

        public bool TryClickDelete(RpgWorld500xSaveSlotId slotId)
        {
            return TryInvokeButton(_deleteButtons, slotId);
        }

        public void Refresh()
        {
            if (IsDisposed || _bootstrapper.IsDisposed)
            {
                return;
            }

            ClearRows();

            List<RpgWorld500xSaveSlotSummary> slots = _slotManager.ListAllSlots();
            int validCount = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsOccupied && !slots[i].IsCorrupted)
                {
                    validCount++;
                }
            }

            _headerText.text = "500x 大世界远征存档簿（有效档位 " + validCount + "/" + slots.Count + "）";

            const float rowHeight = 64f;
            const float spacing = 8f;

            for (int i = 0; i < slots.Count; i++)
            {
                RpgWorld500xSaveSlotSummary s = slots[i];
                string line;
                if (!s.IsOccupied)
                {
                    line = "[" + s.SlotDisplayNameZh + "] 空档位（可随时写入当前远征进度）";
                }
                else if (s.IsCorrupted)
                {
                    line = "[" + s.SlotDisplayNameZh + "] [损坏已隔离] " + s.DiagnosticMessageZh;
                }
                else
                {
                    string custom = string.IsNullOrEmpty(s.CustomLabelZh) ? string.Empty : " · " + s.CustomLabelZh;
                    line = "[" + s.SlotDisplayNameZh + "] " + s.MapDisplayNameZh +
                           "｜第 " + s.DayCount + " 日 " + s.FormattedClock +
                           "｜盐银 " + s.SaltSilverBalance + custom +
                           "\n记录时间：" + RpgWorld500xSaveSlotManager.FormatSavedAtUtcForDisplay(s.SavedAtUtcIso) + "（世界标记 " + s.WorldFlagCount + " 项）";
                }

                RpgWorld500xSaveSlotId capturedId = s.SlotId;
                _slotRevisions[capturedId] = _slotManager.ReadSlotRevision(capturedId);
                SpawnSlotRow(
                    "SaveSlotRow_" + s.SlotId,
                    i,
                    rowHeight,
                    spacing,
                    line,
                    canSave: true,
                    canLoad: s.IsOccupied && !s.IsCorrupted,
                    canDelete: s.IsOccupied,
                    isCorrupted: s.IsCorrupted,
                    capturedId);
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

        private void HandleSaveClicked(RpgWorld500xSaveSlotId slotId)
        {
            if (_slotManager.TrySaveSlot(_bootstrapper, slotId, out RpgWorld500xSaveSlotSummary saved, out string err))
            {
                _statusText.text = "已保存至【" + saved.SlotDisplayNameZh + "】· " +
                                   saved.MapDisplayNameZh + "（盐银 " + saved.SaltSilverBalance + "）";
            }
            else
            {
                _statusText.text = "保存失败：" + err;
            }

            Refresh();
        }

        private void HandleLoadClicked(RpgWorld500xSaveSlotId slotId)
        {
            if (_slotManager.TryLoadSlot(_bootstrapper, slotId, out RpgWorld500xSaveSlotSummary loaded, out string err))
            {
                _statusText.text = "已读取【" + loaded.SlotDisplayNameZh + "】· 返回 " +
                                   loaded.MapDisplayNameZh + "（盐银 " + loaded.SaltSilverBalance + "）";
            }
            else
            {
                _statusText.text = "读取失败：" + err;
            }

            Refresh();
        }

        private void HandleDeleteClicked(RpgWorld500xSaveSlotId slotId)
        {
            string nameZh = RpgWorld500xSaveSlotManager.GetSlotDisplayNameZh(slotId);
            bool deleted = _slotManager.DeleteSlot(slotId);
            _statusText.text = deleted ? "已清空【" + nameZh + "】" : "【" + nameZh + "】本为空档位";
            Refresh();
        }

        private void SpawnSlotRow(
            string name,
            int index,
            float height,
            float spacing,
            string label,
            bool canSave,
            bool canLoad,
            bool canDelete,
            bool isCorrupted,
            RpgWorld500xSaveSlotId slotId)
        {
            var rowGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            rowGo.transform.SetParent(_rowContainer, false);
            var rect = rowGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -(index * (height + spacing)));
            rect.sizeDelta = new Vector2(0f, height);
            rowGo.GetComponent<Image>().color = isCorrupted
                ? new Color(0.26f, 0.12f, 0.13f, 0.90f)
                : new Color(0.13f, 0.15f, 0.20f, 0.90f);

            Text text = CreateText(
                rowGo.transform,
                "Label",
                new Vector2(0.02f, 0.06f), new Vector2(0.65f, 0.94f),
                _font, 12, TextAnchor.MiddleLeft,
                Color.white);
            text.text = label;

            string revision = _slotRevisions[slotId];
            _saveButtons[slotId] = CreateActionButton(
                rowGo.transform, "SaveButton",
                new Vector2(0.67f, 0.16f), new Vector2(0.77f, 0.84f),
                "保存", _font, canSave, () =>
                {
                    if (ValidateSlotRevision(slotId, revision))
                        HandleSaveClicked(slotId);
                });

            _loadButtons[slotId] = CreateActionButton(
                rowGo.transform, "LoadButton",
                new Vector2(0.78f, 0.16f), new Vector2(0.88f, 0.84f),
                "读取", _font, canLoad, () =>
                {
                    if (ValidateSlotRevision(slotId, revision))
                        HandleLoadClicked(slotId);
                });

            _deleteButtons[slotId] = CreateActionButton(
                rowGo.transform, "DeleteButton",
                new Vector2(0.89f, 0.16f), new Vector2(0.98f, 0.84f),
                "删除", _font, canDelete, () =>
                {
                    if (ValidateSlotRevision(slotId, revision))
                        HandleDeleteClicked(slotId);
                });

            _spawnedRows.Add(rowGo);
        }

        private bool TryInvokeButton(Dictionary<RpgWorld500xSaveSlotId, Button> map, RpgWorld500xSaveSlotId slotId)
        {
            if (!map.TryGetValue(slotId, out Button btn) || !CanInvoke(btn))
            {
                return false;
            }

            if (!_slotRevisions.TryGetValue(slotId, out string revision) ||
                !ValidateSlotRevision(slotId, revision))
                return false;

            btn.onClick.Invoke();
            return true;
        }

        private bool ValidateSlotRevision(RpgWorld500xSaveSlotId slotId, string expected)
        {
            if (string.Equals(expected, _slotManager.ReadSlotRevision(slotId), StringComparison.Ordinal))
                return true;

            _statusText.text = "档位已更新，请确认后重试";
            Refresh();
            return false;
        }

        private bool CanInvoke(Button button)
        {
            return IsVisible && !_bootstrapper.IsDisposed && button != null &&
                   button.isActiveAndEnabled && button.IsInteractable();
        }

        private void ClearRows()
        {
            _saveButtons.Clear();
            _loadButtons.Clear();
            _deleteButtons.Clear();
            _slotRevisions.Clear();
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
                ? new Color(0.22f, 0.30f, 0.40f, 0.95f)
                : new Color(0.16f, 0.17f, 0.20f, 0.68f);

            var btn = go.GetComponent<Button>();
            btn.interactable = interactable;
            if (onClick != null)
            {
                btn.onClick.AddListener(() =>
                {
                    // A queued click from a retired row must never overwrite,
                    // restore or delete a different revision of a save slot.
                    if (CanInvoke(btn))
                        onClick();
                });
            }

            Text txt = CreateText(
                go.transform,
                "Text",
                Vector2.zero, Vector2.one,
                font, 12, TextAnchor.MiddleCenter,
                interactable ? new Color(0.94f, 0.96f, 0.98f, 1f) : new Color(0.54f, 0.56f, 0.60f, 1f));
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
