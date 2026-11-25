using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFieldSupplyUseServiceTests
    {
        [Test]
        public void TryConsumeSupply_DecrementsStockTriggersHealStacksTimedBuffsAndExpiresCleanly()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => store[k] = v;

            // Purchase supplies via shared FieldBountyAndSupplyService so stock keys match 100%.
            var shop = new RpgWorld500xFieldBountyAndSupplyService(read, write);
            shop.AddSaltSilver(300);
            Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("gauze", 1));
            Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("damp_cloth", 2));
            Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("lamp_oil", 1));

            int healedTotal = 0;
            var useSvc = new RpgWorld500xFieldSupplyUseService(read, write, (_, pct) => healedTotal += pct);

            // Reject invalid inputs and unowned items.
            Assert.AreEqual(RpgFieldSupplyUseStatus.UnknownItem, useSvc.TryConsumeSupply("unknown", 10f).Status);
            Assert.AreEqual(RpgFieldSupplyUseStatus.NotOwned, useSvc.TryConsumeSupply("flare", 10f).Status);
            Assert.AreEqual(RpgFieldSupplyUseStatus.InvalidTimestamp, useSvc.TryConsumeSupply("gauze", float.NaN).Status);

            // Consume gauze -> heals 25%, stock drops to 0.
            RpgWorld500xFieldSupplyUseResult gauzeRes = useSvc.TryConsumeSupply("gauze", 10f);
            Assert.IsTrue(gauzeRes.Succeeded);
            Assert.AreEqual(0, gauzeRes.RemainingOwnedCount);
            Assert.AreEqual(25, healedTotal);
            Assert.AreEqual(0, shop.GetOwnedSupplyCount("gauze"));

            // Consume damp_cloth (60s) + lamp_oil (90s) at t=10s -> active buffs apply modifiers.
            Assert.IsTrue(useSvc.TryConsumeSupply("damp_cloth", 10f).Succeeded);
            Assert.IsTrue(useSvc.TryConsumeSupply("lamp_oil", 10f).Succeeded);
            RpgWorld500xActiveSupplyBuffModifiers active = useSvc.EvaluateActiveModifiers(20f);
            Assert.AreEqual(2, active.ActiveBuffCount);
            Assert.Less(active.PatrolNoiseMultiplier, 0.70f);
            Assert.Greater(active.LanternHaloMultiplier, 1.30f);
            Assert.IsTrue(active.ActiveBuffSummaryZh.Contains("湿布消声"));
            Assert.IsTrue(active.ActiveBuffSummaryZh.Contains("灯油照明"));

            // Consuming a second damp_cloth extends remaining duration from expiry (70s -> 130s).
            Assert.IsTrue(useSvc.TryConsumeSupply("damp_cloth", 20f).Succeeded);
            Assert.AreEqual(110f, useSvc.GetRemainingBuffSeconds("damp_cloth", 20f), 0.01f);

            // After all buffs expire (t=200s), modifiers return to Neutral.
            RpgWorld500xActiveSupplyBuffModifiers expired = useSvc.EvaluateActiveModifiers(200f);
            Assert.AreEqual(0, expired.ActiveBuffCount);
            Assert.AreEqual(1f, expired.PatrolNoiseMultiplier, 0.001f);
            Assert.AreEqual(1f, expired.LanternHaloMultiplier, 0.001f);
        }

        [Test]
        public void ExportAndRestoreRemainingBuffSeconds_PreservesActiveBuffsAcrossSessionClockAndFiltersInvalidEntries()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => store[k] = v;

            var shop = new RpgWorld500xFieldBountyAndSupplyService(read, write);
            shop.AddSaltSilver(300);
            Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("lamp_oil", 1));
            Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("flare", 1));

            var sourceSvc = new RpgWorld500xFieldSupplyUseService(read, write);
            Assert.IsTrue(sourceSvc.TryConsumeSupply("lamp_oil", 10f).Succeeded);
            Assert.IsTrue(sourceSvc.TryConsumeSupply("flare", 10f).Succeeded);

            // At t=25s, lamp_oil has 75s left and flare has 30s left.
            Dictionary<string, float> snapshot = sourceSvc.ExportRemainingBuffSeconds(25f);
            Assert.AreEqual(2, snapshot.Count);
            Assert.AreEqual(75f, snapshot["lamp_oil"], 0.01f);
            Assert.AreEqual(30f, snapshot["flare"], 0.01f);

            // Inject invalid entries and restore onto a fresh service at a new session clock t=500s.
            snapshot["unknown_item"] = 50f;
            snapshot["damp_cloth"] = -10f;
            snapshot["iron_clamps"] = float.NaN;

            var restoredSvc = new RpgWorld500xFieldSupplyUseService(read, write);
            int restoredCount = restoredSvc.RestoreRemainingBuffSeconds(snapshot, nowSeconds: 500f);
            Assert.AreEqual(2, restoredCount);
            Assert.AreEqual(75f, restoredSvc.GetRemainingBuffSeconds("lamp_oil", 500f), 0.01f);
            Assert.AreEqual(30f, restoredSvc.GetRemainingBuffSeconds("flare", 500f), 0.01f);
            Assert.AreEqual(0f, restoredSvc.GetRemainingBuffSeconds("damp_cloth", 500f), 0.01f);
            Assert.AreEqual(2, restoredSvc.EvaluateActiveModifiers(500f).ActiveBuffCount);
        }
    }
}
