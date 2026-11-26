using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgFieldSupplyUseStatus : byte
    {
        Succeeded = 0,
        UnknownItem = 1,
        NotOwned = 2,
        InvalidTimestamp = 3
    }

    public readonly struct RpgWorld500xActiveSupplyBuffModifiers
    {
        public RpgWorld500xActiveSupplyBuffModifiers(
            float lanternHaloMultiplier,
            float patrolNoiseMultiplier,
            float patrolDetectionMultiplier,
            float shortcutRepairSpeedMultiplier,
            int activeBuffCount,
            string activeBuffSummaryZh)
        {
            LanternHaloMultiplier = lanternHaloMultiplier;
            PatrolNoiseMultiplier = patrolNoiseMultiplier;
            PatrolDetectionMultiplier = patrolDetectionMultiplier;
            ShortcutRepairSpeedMultiplier = shortcutRepairSpeedMultiplier;
            ActiveBuffCount = activeBuffCount;
            ActiveBuffSummaryZh = activeBuffSummaryZh ?? string.Empty;
        }

        public float LanternHaloMultiplier { get; }
        public float PatrolNoiseMultiplier { get; }
        public float PatrolDetectionMultiplier { get; }
        public float ShortcutRepairSpeedMultiplier { get; }
        public int ActiveBuffCount { get; }
        public string ActiveBuffSummaryZh { get; }

        public static RpgWorld500xActiveSupplyBuffModifiers Neutral =>
            new RpgWorld500xActiveSupplyBuffModifiers(1f, 1f, 1f, 1f, 0, string.Empty);
    }

    public readonly struct RpgWorld500xFieldSupplyUseResult
    {
        public RpgWorld500xFieldSupplyUseResult(
            RpgFieldSupplyUseStatus status,
            string itemId,
            string itemNameZh,
            int remainingOwnedCount,
            float buffDurationSeconds,
            int partyHealPercent,
            string feedbackBannerZh)
        {
            Status = status;
            ItemId = itemId ?? string.Empty;
            ItemNameZh = itemNameZh ?? string.Empty;
            RemainingOwnedCount = remainingOwnedCount;
            BuffDurationSeconds = buffDurationSeconds;
            PartyHealPercent = partyHealPercent;
            FeedbackBannerZh = feedbackBannerZh ?? string.Empty;
        }

        public RpgFieldSupplyUseStatus Status { get; }
        public string ItemId { get; }
        public string ItemNameZh { get; }
        public int RemainingOwnedCount { get; }
        public float BuffDurationSeconds { get; }
        public int PartyHealPercent { get; }
        public string FeedbackBannerZh { get; }
        public bool Succeeded => Status == RpgFieldSupplyUseStatus.Succeeded;
    }

    /// <summary>
    /// Consumes owned camp supplies (gauze, rations, lamp_oil, damp_cloth, flare,
    /// iron_clamps) in the 500x field, decrementing persisted OwnedCountKey stock
    /// and activating deterministic timed exploration buffs or party recovery.
    /// </summary>
    public sealed class RpgWorld500xFieldSupplyUseService
    {
        private const float MaxRestoredBuffSeconds = 3600f;

        private readonly Func<string, int> _getInt;
        private readonly Action<string, int> _setInt;
        private readonly Action<string, int> _onPartyHealPercent;
        private readonly Dictionary<string, RpgWorld500xCampSupplyItemDef> _itemsById =
            new Dictionary<string, RpgWorld500xCampSupplyItemDef>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _buffExpiryByItemId =
            new Dictionary<string, float>(StringComparer.Ordinal);

        public RpgWorld500xFieldSupplyUseService(
            Func<string, int> getInt = null,
            Action<string, int> setInt = null,
            Action<string, int> onPartyHealPercent = null)
        {
            _getInt = getInt ?? (_ => 0);
            _setInt = setInt ?? ((_, __) => { });
            _onPartyHealPercent = onPartyHealPercent;

            IReadOnlyList<RpgWorld500xCampSupplyItemDef> supplies =
                RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();
            for (int i = 0; i < supplies.Count; i++)
            {
                _itemsById[supplies[i].Id] = supplies[i];
            }
        }

        public int GetOwnedCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !_itemsById.TryGetValue(itemId, out RpgWorld500xCampSupplyItemDef item))
            {
                return 0;
            }

            return Math.Max(0, _getInt(item.OwnedCountKey));
        }

        public float GetRemainingBuffSeconds(string itemId, float nowSeconds)
        {
            if (string.IsNullOrEmpty(itemId) || !IsFinite(nowSeconds) || nowSeconds < 0f ||
                !_buffExpiryByItemId.TryGetValue(itemId, out float expiry))
            {
                return 0f;
            }

            return Mathf.Max(0f, expiry - nowSeconds);
        }

        public RpgWorld500xFieldSupplyUseResult TryConsumeSupply(string itemId, float nowSeconds)
        {
            if (!IsFinite(nowSeconds) || nowSeconds < 0f)
            {
                return new RpgWorld500xFieldSupplyUseResult(
                    RpgFieldSupplyUseStatus.InvalidTimestamp, itemId, string.Empty, 0, 0f, 0, "无效时间戳");
            }

            if (string.IsNullOrEmpty(itemId) || !_itemsById.TryGetValue(itemId, out RpgWorld500xCampSupplyItemDef item))
            {
                return new RpgWorld500xFieldSupplyUseResult(
                    RpgFieldSupplyUseStatus.UnknownItem, itemId, string.Empty, 0, 0f, 0, "未知补给品");
            }

            int owned = GetOwnedCount(itemId);
            if (owned <= 0)
            {
                return new RpgWorld500xFieldSupplyUseResult(
                    RpgFieldSupplyUseStatus.NotOwned, item.Id, item.DisplayNameZh, 0, 0f, 0,
                    "未持有【" + item.DisplayNameZh + "】");
            }

            int remaining = owned - 1;
            _setInt(item.OwnedCountKey, remaining);

            ResolveEffect(item.Id, out float durationSeconds, out int healPercent, out string effectCaptionZh);
            if (healPercent > 0)
            {
                _onPartyHealPercent?.Invoke(item.Id, healPercent);
            }

            if (durationSeconds > 0f)
            {
                float baseStart = _buffExpiryByItemId.TryGetValue(item.Id, out float prevExpiry) && prevExpiry > nowSeconds
                    ? prevExpiry
                    : nowSeconds;
                _buffExpiryByItemId[item.Id] = baseStart + durationSeconds;
            }

            string banner = "已使用【" + item.DisplayNameZh + "】：" + effectCaptionZh +
                            "（剩余 " + remaining + "）";

            return new RpgWorld500xFieldSupplyUseResult(
                RpgFieldSupplyUseStatus.Succeeded,
                item.Id,
                item.DisplayNameZh,
                remaining,
                durationSeconds,
                healPercent,
                banner);
        }

        public RpgWorld500xActiveSupplyBuffModifiers EvaluateActiveModifiers(float nowSeconds)
        {
            if (!IsFinite(nowSeconds) || nowSeconds < 0f)
            {
                return RpgWorld500xActiveSupplyBuffModifiers.Neutral;
            }

            float lantern = 1f;
            float noise = 1f;
            float detection = 1f;
            float repair = 1f;
            int activeCount = 0;
            var sb = new StringBuilder(96);

            if (GetRemainingBuffSeconds("lamp_oil", nowSeconds) > 0f)
            {
                lantern *= 1.35f;
                activeCount++;
                AppendTag(sb, "灯油照明", GetRemainingBuffSeconds("lamp_oil", nowSeconds));
            }

            if (GetRemainingBuffSeconds("damp_cloth", nowSeconds) > 0f)
            {
                noise *= 0.65f;
                activeCount++;
                AppendTag(sb, "湿布消声", GetRemainingBuffSeconds("damp_cloth", nowSeconds));
            }

            if (GetRemainingBuffSeconds("flare", nowSeconds) > 0f)
            {
                detection *= 0.78f;
                lantern *= 1.15f;
                activeCount++;
                AppendTag(sb, "信号弹压制", GetRemainingBuffSeconds("flare", nowSeconds));
            }

            if (GetRemainingBuffSeconds("iron_clamps", nowSeconds) > 0f)
            {
                repair *= 1.50f;
                activeCount++;
                AppendTag(sb, "铁件抢修", GetRemainingBuffSeconds("iron_clamps", nowSeconds));
            }

            return new RpgWorld500xActiveSupplyBuffModifiers(
                Mathf.Clamp(lantern, 1f, 1.80f),
                Mathf.Clamp(noise, 0.50f, 1f),
                Mathf.Clamp(detection, 0.65f, 1f),
                Mathf.Clamp(repair, 1f, 2f),
                activeCount,
                sb.ToString());
        }

        public Dictionary<string, float> ExportRemainingBuffSeconds(float nowSeconds)
        {
            var snapshot = new Dictionary<string, float>(StringComparer.Ordinal);
            if (!IsFinite(nowSeconds) || nowSeconds < 0f)
            {
                return snapshot;
            }

            foreach (KeyValuePair<string, float> kv in _buffExpiryByItemId)
            {
                float remaining = kv.Value - nowSeconds;
                if (remaining > 0f && IsFinite(remaining))
                {
                    snapshot[kv.Key] = remaining;
                }
            }

            return snapshot;
        }

        public int RestoreRemainingBuffSeconds(
            IReadOnlyDictionary<string, float> remainingByItemId,
            float nowSeconds,
            bool clearExisting = true)
        {
            // Reject an invalid session clock before changing live buff state.
            if (!IsFinite(nowSeconds) || nowSeconds < 0f)
            {
                return 0;
            }

            if (clearExisting)
            {
                _buffExpiryByItemId.Clear();
            }

            if (remainingByItemId == null)
            {
                return 0;
            }

            int restored = 0;
            foreach (KeyValuePair<string, float> kv in remainingByItemId)
            {
                if (string.IsNullOrEmpty(kv.Key) || !_itemsById.ContainsKey(kv.Key))
                {
                    continue;
                }

                if (!IsFinite(kv.Value) || kv.Value <= 0f)
                {
                    continue;
                }

                // Immediate healing supplies never have a timed field effect.
                ResolveEffect(kv.Key, out float durationSeconds, out _, out _);
                if (durationSeconds <= 0f)
                {
                    continue;
                }

                float clamped = Mathf.Min(kv.Value, MaxRestoredBuffSeconds);
                _buffExpiryByItemId[kv.Key] = nowSeconds + clamped;
                restored++;
            }

            return restored;
        }

        public void ClearActiveBuffs()
        {
            _buffExpiryByItemId.Clear();
        }

        private static void ResolveEffect(
            string itemId,
            out float durationSeconds,
            out int healPercent,
            out string captionZh)
        {
            switch (itemId)
            {
                case "gauze":
                    durationSeconds = 0f;
                    healPercent = 25;
                    captionZh = "包扎止血，小队回复 25% 生命";
                    break;
                case "rations":
                    durationSeconds = 0f;
                    healPercent = 35;
                    captionZh = "补充干粮，小队回复 35% 体力与生命";
                    break;
                case "lamp_oil":
                    durationSeconds = 90f;
                    healPercent = 0;
                    captionZh = "提灯照明范围扩大（90 秒）";
                    break;
                case "damp_cloth":
                    durationSeconds = 60f;
                    healPercent = 0;
                    captionZh = "脚步与回声噪音降低（60 秒）";
                    break;
                case "flare":
                    durationSeconds = 45f;
                    healPercent = 0;
                    captionZh = "照亮盲区并压制巡逻侦测（45 秒）";
                    break;
                case "iron_clamps":
                    durationSeconds = 120f;
                    healPercent = 0;
                    captionZh = "捷径与吊桥检修加速（120 秒）";
                    break;
                default:
                    durationSeconds = 0f;
                    healPercent = 0;
                    captionZh = "生效";
                    break;
            }
        }

        private static void AppendTag(StringBuilder sb, string label, float seconds)
        {
            if (sb.Length > 0)
            {
                sb.Append("｜");
            }

            sb.Append(label).Append(' ').Append(Mathf.CeilToInt(seconds)).Append('s');
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
