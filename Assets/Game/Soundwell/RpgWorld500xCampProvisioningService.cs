using System;
using System.Collections.Generic;
using System.Text;

namespace Rpg.Soundwell
{
    public readonly struct RpgWorld500xProvisioningLine
    {
        public RpgWorld500xProvisioningLine(
            string itemId,
            string displayNameZh,
            int quantity,
            int unitCostSaltSilver)
        {
            ItemId = itemId ?? string.Empty;
            DisplayNameZh = displayNameZh ?? string.Empty;
            Quantity = Math.Max(0, quantity);
            UnitCostSaltSilver = Math.Max(0, unitCostSaltSilver);
            long subtotal = (long)Quantity * UnitCostSaltSilver;
            SubtotalSaltSilver = (int)Math.Min(int.MaxValue, subtotal);
        }

        public string ItemId { get; }
        public string DisplayNameZh { get; }
        public int Quantity { get; }
        public int UnitCostSaltSilver { get; }
        public int SubtotalSaltSilver { get; }
    }

    public readonly struct RpgWorld500xProvisioningPlan
    {
        public RpgWorld500xProvisioningPlan(
            RpgWorldMapId originMapId,
            RpgWorldMapId destinationMapId,
            RpgWorld500xTravelItinerary itinerary,
            RpgWorld500xExpeditionReadinessReport readinessBefore,
            IReadOnlyList<RpgWorld500xProvisioningLine> lines,
            int totalRequiredSaltSilver,
            int availableSaltSilver,
            bool canAffordFullPlan,
            string summaryZh)
        {
            OriginMapId = originMapId;
            DestinationMapId = destinationMapId;
            Itinerary = itinerary;
            ReadinessBefore = readinessBefore;
            Lines = lines ?? Array.Empty<RpgWorld500xProvisioningLine>();
            TotalRequiredSaltSilver = totalRequiredSaltSilver;
            AvailableSaltSilver = availableSaltSilver;
            CanAffordFullPlan = canAffordFullPlan;
            SummaryZh = summaryZh ?? string.Empty;
        }

        public RpgWorldMapId OriginMapId { get; }
        public RpgWorldMapId DestinationMapId { get; }
        public RpgWorld500xTravelItinerary Itinerary { get; }
        public RpgWorld500xExpeditionReadinessReport ReadinessBefore { get; }
        public IReadOnlyList<RpgWorld500xProvisioningLine> Lines { get; }
        public int TotalRequiredSaltSilver { get; }
        public int AvailableSaltSilver { get; }
        public bool CanAffordFullPlan { get; }
        public string SummaryZh { get; }
    }

    public readonly struct RpgWorld500xProvisioningReceipt
    {
        public RpgWorld500xProvisioningReceipt(
            bool alreadyPrepared,
            bool fullyProvisioned,
            int beforeScore,
            int afterScore,
            RpgWorld500xReadinessGrade beforeGrade,
            RpgWorld500xReadinessGrade afterGrade,
            int totalSpentSaltSilver,
            int remainingSaltSilver,
            IReadOnlyList<RpgWorld500xProvisioningLine> purchasedLines,
            IReadOnlyList<string> skippedItemIds,
            string summaryZh)
        {
            AlreadyPrepared = alreadyPrepared;
            FullyProvisioned = fullyProvisioned;
            BeforeScore = beforeScore;
            AfterScore = afterScore;
            BeforeGrade = beforeGrade;
            AfterGrade = afterGrade;
            TotalSpentSaltSilver = totalSpentSaltSilver;
            RemainingSaltSilver = remainingSaltSilver;
            PurchasedLines = purchasedLines ?? Array.Empty<RpgWorld500xProvisioningLine>();
            SkippedItemIds = skippedItemIds ?? Array.Empty<string>();
            SummaryZh = summaryZh ?? string.Empty;
        }

        public bool AlreadyPrepared { get; }
        public bool FullyProvisioned { get; }
        public int BeforeScore { get; }
        public int AfterScore { get; }
        public RpgWorld500xReadinessGrade BeforeGrade { get; }
        public RpgWorld500xReadinessGrade AfterGrade { get; }
        public int TotalSpentSaltSilver { get; }
        public int RemainingSaltSilver { get; }
        public IReadOnlyList<RpgWorld500xProvisioningLine> PurchasedLines { get; }
        public IReadOnlyList<string> SkippedItemIds { get; }
        public string SummaryZh { get; }
    }

    /// <summary>
    /// Synthesizes live Expedition Readiness, multi-hop Fast Travel Itinerary risk,
    /// and Camp Merchant supply stock into a prioritized one-click camp provisioning
    /// plan and budget-safe auto-restock execution across all four 500x regions.
    /// </summary>
    public static class RpgWorld500xCampProvisioningService
    {
        public static RpgWorld500xProvisioningPlan BuildProvisioningPlan(
            RpgWorld500xSceneBootstrapper bootstrapper,
            RpgWorldMapId destinationMapId,
            float elapsedSeconds = 0f,
            RpgWorld500xFieldSupplyUseService supplyUseService = null)
        {
            if (bootstrapper == null || bootstrapper.IsDisposed)
            {
                throw new ArgumentNullException(nameof(bootstrapper));
            }

            RpgWorld500xGrandRuntimeFacade facade = bootstrapper.Driver.Facade;
            RpgWorldMapId originMapId = facade.CurrentMapId;
            RpgWorld500xFieldBountyAndSupplyService shop = facade.BountyAndSupply;

            RpgWorld500xExpeditionReadinessReport readiness =
                RpgWorld500xExpeditionReadinessService.EvaluateReadiness(
                    bootstrapper, elapsedSeconds, supplyUseService);
            RpgWorld500xTravelItinerary itinerary =
                RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(
                    originMapId, destinationMapId, facade.Atmosphere);

            IReadOnlyList<RpgWorld500xCampSupplyItemDef> catalog =
                RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();
            var catalogById = new Dictionary<string, RpgWorld500xCampSupplyItemDef>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Count; i++)
            {
                catalogById[catalog[i].Id] = catalog[i];
            }

            var priorityOrder = new List<string>(6);
            var targetOwnedById = new Dictionary<string, int>(StringComparer.Ordinal);

            void EnsureTarget(string itemId, int targetOwned)
            {
                if (!catalogById.ContainsKey(itemId) || targetOwned <= 0)
                {
                    return;
                }

                if (!targetOwnedById.TryGetValue(itemId, out int existing))
                {
                    priorityOrder.Add(itemId);
                    targetOwnedById[itemId] = targetOwned;
                }
                else if (targetOwned > existing)
                {
                    targetOwnedById[itemId] = targetOwned;
                }
            }

            for (int i = 0; i < readiness.RecommendedSupplyIds.Count; i++)
            {
                EnsureTarget(readiness.RecommendedSupplyIds[i], 1);
            }

            if (itinerary.HopCount >= 2 || itinerary.RecommendCampfireStop)
            {
                EnsureTarget("rations", 2);
            }

            if (itinerary.HighestRiskLevel == RpgWorld500xTravelRiskLevel.Hazardous)
            {
                bool lampBuffActive = supplyUseService != null &&
                    supplyUseService.GetRemainingBuffSeconds("lamp_oil", elapsedSeconds) > 0f;
                bool flareBuffActive = supplyUseService != null &&
                    supplyUseService.GetRemainingBuffSeconds("flare", elapsedSeconds) > 0f;

                if (!lampBuffActive)
                {
                    EnsureTarget("lamp_oil", 1);
                }

                if (!flareBuffActive)
                {
                    EnsureTarget("flare", 1);
                }
            }

            var lines = new List<RpgWorld500xProvisioningLine>(priorityOrder.Count);
            long totalCost = 0;

            for (int i = 0; i < priorityOrder.Count; i++)
            {
                string itemId = priorityOrder[i];
                RpgWorld500xCampSupplyItemDef def = catalogById[itemId];
                int owned = shop.GetOwnedSupplyCount(itemId);
                int remainingStock = shop.GetRemainingStock(itemId);
                int needed = Math.Min(remainingStock, Math.Max(0, targetOwnedById[itemId] - owned));
                if (needed <= 0)
                {
                    continue;
                }

                var line = new RpgWorld500xProvisioningLine(
                    def.Id, def.DisplayNameZh, needed, def.SaltSilverCost);
                lines.Add(line);
                totalCost += line.SubtotalSaltSilver;
            }

            int clampedTotal = (int)Math.Min(int.MaxValue, totalCost);
            int available = shop.SaltSilverBalance;
            bool canAffordFull = available >= clampedTotal;

            var sb = new StringBuilder(192);
            sb.Append("【营地整备方案】");
            if (lines.Count == 0)
            {
                sb.Append("当前行囊已满足出征要求，无需补购（结余盐银 ")
                  .Append(available).Append("）。");
            }
            else
            {
                sb.Append("建议补购 ").Append(lines.Count).Append(" 类物资，总计 ")
                  .Append(clampedTotal).Append(" 盐银（当前持有 ")
                  .Append(available).Append("，")
                  .Append(canAffordFull ? "可全额备齐" : "盐银不足，可按优先级部分补购")
                  .Append("）。");
            }

            return new RpgWorld500xProvisioningPlan(
                originMapId,
                destinationMapId,
                itinerary,
                readiness,
                lines,
                clampedTotal,
                available,
                canAffordFull,
                sb.ToString());
        }

        public static RpgWorld500xProvisioningReceipt ExecuteAutoProvisioning(
            RpgWorld500xSceneBootstrapper bootstrapper,
            RpgWorldMapId destinationMapId,
            bool allowPartialIfUnderBudget = true,
            float elapsedSeconds = 0f,
            RpgWorld500xFieldSupplyUseService supplyUseService = null)
        {
            RpgWorld500xProvisioningPlan plan = BuildProvisioningPlan(
                bootstrapper, destinationMapId, elapsedSeconds, supplyUseService);
            RpgWorld500xFieldBountyAndSupplyService shop = bootstrapper.Driver.Facade.BountyAndSupply;

            if (plan.Lines.Count == 0)
            {
                return new RpgWorld500xProvisioningReceipt(
                    alreadyPrepared: true,
                    fullyProvisioned: true,
                    beforeScore: plan.ReadinessBefore.Score,
                    afterScore: plan.ReadinessBefore.Score,
                    beforeGrade: plan.ReadinessBefore.Grade,
                    afterGrade: plan.ReadinessBefore.Grade,
                    totalSpentSaltSilver: 0,
                    remainingSaltSilver: shop.SaltSilverBalance,
                    purchasedLines: Array.Empty<RpgWorld500xProvisioningLine>(),
                    skippedItemIds: Array.Empty<string>(),
                    summaryZh: "【一键整备】行囊已齐全，未产生额外花费。");
            }

            if (!allowPartialIfUnderBudget && !plan.CanAffordFullPlan)
            {
                var allSkipped = new List<string>(plan.Lines.Count);
                for (int i = 0; i < plan.Lines.Count; i++)
                {
                    allSkipped.Add(plan.Lines[i].ItemId);
                }

                return new RpgWorld500xProvisioningReceipt(
                    alreadyPrepared: false,
                    fullyProvisioned: false,
                    beforeScore: plan.ReadinessBefore.Score,
                    afterScore: plan.ReadinessBefore.Score,
                    beforeGrade: plan.ReadinessBefore.Grade,
                    afterGrade: plan.ReadinessBefore.Grade,
                    totalSpentSaltSilver: 0,
                    remainingSaltSilver: shop.SaltSilverBalance,
                    purchasedLines: Array.Empty<RpgWorld500xProvisioningLine>(),
                    skippedItemIds: allSkipped,
                    summaryZh: "【一键整备】盐银不足以全额采购（需 " +
                        plan.TotalRequiredSaltSilver + "，持有 " + shop.SaltSilverBalance + "），已取消扣款。");
            }

            var purchased = new List<RpgWorld500xProvisioningLine>(plan.Lines.Count);
            var skipped = new List<string>();
            long spent = 0;

            for (int i = 0; i < plan.Lines.Count; i++)
            {
                RpgWorld500xProvisioningLine line = plan.Lines[i];
                int affordableQty = line.UnitCostSaltSilver > 0
                    ? Math.Min(line.Quantity, shop.SaltSilverBalance / line.UnitCostSaltSilver)
                    : 0;

                if (affordableQty > 0 &&
                    shop.TryPurchaseSupply(line.ItemId, affordableQty) == RpgCampSupplyPurchaseStatus.Succeeded)
                {
                    var bought = new RpgWorld500xProvisioningLine(
                        line.ItemId, line.DisplayNameZh, affordableQty, line.UnitCostSaltSilver);
                    purchased.Add(bought);
                    spent += bought.SubtotalSaltSilver;
                }

                if (affordableQty < line.Quantity)
                {
                    skipped.Add(line.ItemId);
                }
            }

            RpgWorld500xExpeditionReadinessReport readinessAfter =
                RpgWorld500xExpeditionReadinessService.EvaluateReadiness(
                    bootstrapper, elapsedSeconds, supplyUseService);

            bool fullyProvisioned = skipped.Count == 0;
            int clampedSpent = (int)Math.Min(int.MaxValue, spent);
            string summary = "【一键整备】" +
                (fullyProvisioned ? "全额备齐" : "按预算部分备齐") +
                "｜支出 " + clampedSpent + " 盐银，结余 " + shop.SaltSilverBalance +
                "｜整备评分 " + plan.ReadinessBefore.Score + " → " + readinessAfter.Score +
                "（" + readinessAfter.GradeLabelZh + "）。";

            return new RpgWorld500xProvisioningReceipt(
                alreadyPrepared: false,
                fullyProvisioned: fullyProvisioned,
                beforeScore: plan.ReadinessBefore.Score,
                afterScore: readinessAfter.Score,
                beforeGrade: plan.ReadinessBefore.Grade,
                afterGrade: readinessAfter.Grade,
                totalSpentSaltSilver: clampedSpent,
                remainingSaltSilver: shop.SaltSilverBalance,
                purchasedLines: purchased,
                skippedItemIds: skipped,
                summaryZh: summary);
        }
    }
}
