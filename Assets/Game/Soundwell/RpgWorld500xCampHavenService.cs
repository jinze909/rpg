using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public readonly struct RpgWorld500xCampSupplyQuote
    {
        public RpgWorld500xCampSupplyQuote(
            bool isValidItem,
            string itemId,
            int ownedCount,
            int maxStock,
            int remainingCapacity,
            int baseUnitCost,
            int discountedUnitCost,
            int unitSavings,
            int discountPercent,
            int requestedQuantity,
            int maxAffordableQuantity,
            int totalDiscountedCost,
            bool canFulfillRequested,
            string summaryZh)
        {
            IsValidItem = isValidItem;
            ItemId = itemId ?? string.Empty;
            OwnedCount = ownedCount;
            MaxStock = maxStock;
            RemainingCapacity = remainingCapacity;
            BaseUnitCost = baseUnitCost;
            DiscountedUnitCost = discountedUnitCost;
            UnitSavings = unitSavings;
            DiscountPercent = discountPercent;
            RequestedQuantity = requestedQuantity;
            MaxAffordableQuantity = maxAffordableQuantity;
            TotalDiscountedCost = totalDiscountedCost;
            CanFulfillRequested = canFulfillRequested;
            SummaryZh = summaryZh ?? string.Empty;
        }

        public bool IsValidItem { get; }
        public string ItemId { get; }
        public int OwnedCount { get; }
        public int MaxStock { get; }
        public int RemainingCapacity { get; }
        public int BaseUnitCost { get; }
        public int DiscountedUnitCost { get; }
        public int UnitSavings { get; }
        public int DiscountPercent { get; }
        public int RequestedQuantity { get; }
        public int MaxAffordableQuantity { get; }
        public int TotalDiscountedCost { get; }
        public bool CanFulfillRequested { get; }
        public string SummaryZh { get; }
    }

    public readonly struct RpgWorld500xCampHavenSummary
    {
        public RpgWorld500xCampHavenSummary(
            RpgWorldMapId mapId,
            RpgWorld500xAtmosphereSnapshot atmosphere,
            RpgRegionalReputationSnapshot reputation,
            int saltSilverBalance,
            int newlyClaimedBountyCount,
            string saveSnapshotJson,
            string summaryZh)
        {
            MapId = mapId;
            Atmosphere = atmosphere;
            Reputation = reputation;
            SaltSilverBalance = saltSilverBalance;
            NewlyClaimedBountyCount = newlyClaimedBountyCount;
            SaveSnapshotJson = saveSnapshotJson ?? string.Empty;
            SummaryZh = summaryZh ?? string.Empty;
        }

        public RpgWorldMapId MapId { get; }
        public RpgWorld500xAtmosphereSnapshot Atmosphere { get; }
        public RpgRegionalReputationSnapshot Reputation { get; }
        public int SaltSilverBalance { get; }
        public int NewlyClaimedBountyCount { get; }
        public string SaveSnapshotJson { get; }
        public string SummaryZh { get; }
    }

    /// <summary>
    /// Campfire & safe-haven service hub connecting 500x camp rest, regional
    /// faction reputation sync, reputation-discounted camp supply purchases,
    /// fast-travel route planning, and automatic checksum-verified camp saves.
    /// </summary>
    public sealed class RpgWorld500xCampHavenService
    {
        private readonly Func<string, int> _getInt;
        private readonly Action<string, int> _setInt;
        private readonly RpgWorld500xRegionalReputationService _reputation;
        private readonly Dictionary<string, RpgWorld500xCampSupplyItemDef> _itemsById =
            new Dictionary<string, RpgWorld500xCampSupplyItemDef>(StringComparer.Ordinal);

        public RpgWorld500xRegionalReputationService Reputation => _reputation;

        public RpgWorld500xCampHavenService(
            Func<string, int> getInt = null,
            Action<string, int> setInt = null)
        {
            _getInt = getInt ?? (_ => 0);
            _setInt = setInt ?? ((_, __) => { });
            _reputation = new RpgWorld500xRegionalReputationService(_getInt, _setInt);

            IReadOnlyList<RpgWorld500xCampSupplyItemDef> supplies = RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();
            for (int i = 0; i < supplies.Count; i++)
            {
                _itemsById[supplies[i].Id] = supplies[i];
            }
        }

        public int GetDiscountedUnitCost(RpgWorldMapId mapId, string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !_itemsById.TryGetValue(itemId, out RpgWorld500xCampSupplyItemDef item))
            {
                return 0;
            }

            RpgRegionalReputationRank rank = _reputation.GetSnapshot(mapId).Rank;
            return RpgWorld500xRegionalReputationService.ComputeDiscountedCost(item.SaltSilverCost, rank);
        }

        public int GetMaxAffordableDiscountedQuantity(RpgWorldMapId mapId, string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !_itemsById.TryGetValue(itemId, out RpgWorld500xCampSupplyItemDef item))
            {
                return 0;
            }

            int owned = Math.Max(0, _getInt(item.OwnedCountKey));
            int remainingCapacity = Math.Max(0, item.MaxStock - owned);
            if (remainingCapacity == 0)
            {
                return 0;
            }

            int unitCost = GetDiscountedUnitCost(mapId, itemId);
            if (unitCost <= 0)
            {
                return remainingCapacity;
            }

            int balance = Math.Max(0, _getInt(RpgWorld500xFieldBountyAndSupplyService.WalletSaltSilverKey));
            int affordableBySilver = balance / unitCost;
            return Math.Min(remainingCapacity, affordableBySilver);
        }

        public RpgWorld500xCampSupplyQuote BuildSupplyRestockQuote(
            RpgWorldMapId mapId,
            string itemId,
            int requestedQuantity)
        {
            RpgRegionalReputationSnapshot rep = _reputation.GetSnapshot(mapId);
            if (string.IsNullOrEmpty(itemId) || !_itemsById.TryGetValue(itemId, out RpgWorld500xCampSupplyItemDef item))
            {
                return new RpgWorld500xCampSupplyQuote(
                    isValidItem: false,
                    itemId: itemId ?? string.Empty,
                    ownedCount: 0,
                    maxStock: 0,
                    remainingCapacity: 0,
                    baseUnitCost: 0,
                    discountedUnitCost: 0,
                    unitSavings: 0,
                    discountPercent: rep.CampSupplyDiscountPercent,
                    requestedQuantity: requestedQuantity,
                    maxAffordableQuantity: 0,
                    totalDiscountedCost: 0,
                    canFulfillRequested: false,
                    summaryZh: "【营地补给报价】未知补给项");
            }

            int owned = Math.Max(0, _getInt(item.OwnedCountKey));
            int remainingCapacity = Math.Max(0, item.MaxStock - owned);
            int baseUnitCost = Math.Max(0, item.SaltSilverCost);
            int discountedUnitCost = RpgWorld500xRegionalReputationService.ComputeDiscountedCost(baseUnitCost, rep.Rank);
            int unitSavings = Math.Max(0, baseUnitCost - discountedUnitCost);
            int maxAffordable = GetMaxAffordableDiscountedQuantity(mapId, itemId);

            int safeRequested = Math.Max(0, requestedQuantity);
            long totalCostLong = (long)discountedUnitCost * safeRequested;
            int totalDiscountedCost = totalCostLong > int.MaxValue ? int.MaxValue : (int)totalCostLong;
            bool canFulfill = requestedQuantity > 0 && requestedQuantity <= maxAffordable;

            string summaryZh = "【营地补给报价】" + item.Id +
                               "｜声望折扣 " + rep.CampSupplyDiscountPercent + "%" +
                               "（单价 " + discountedUnitCost + " 盐银，原价 " + baseUnitCost + "）" +
                               "｜可购上限 " + maxAffordable +
                               "（库存余量 " + remainingCapacity + "）";

            return new RpgWorld500xCampSupplyQuote(
                isValidItem: true,
                itemId: item.Id,
                ownedCount: owned,
                maxStock: item.MaxStock,
                remainingCapacity: remainingCapacity,
                baseUnitCost: baseUnitCost,
                discountedUnitCost: discountedUnitCost,
                unitSavings: unitSavings,
                discountPercent: rep.CampSupplyDiscountPercent,
                requestedQuantity: requestedQuantity,
                maxAffordableQuantity: maxAffordable,
                totalDiscountedCost: totalDiscountedCost,
                canFulfillRequested: canFulfill,
                summaryZh: summaryZh);
        }

        public RpgCampSupplyPurchaseStatus TryPurchaseDiscountedSupply(
            RpgWorldMapId mapId,
            string itemId,
            int quantity,
            out int totalCostCharged)
        {
            totalCostCharged = 0;
            if (quantity <= 0)
            {
                return RpgCampSupplyPurchaseStatus.InvalidQuantity;
            }

            if (string.IsNullOrEmpty(itemId) || !_itemsById.TryGetValue(itemId, out RpgWorld500xCampSupplyItemDef item))
            {
                return RpgCampSupplyPurchaseStatus.UnknownItem;
            }

            int owned = Math.Max(0, _getInt(item.OwnedCountKey));
            if (owned >= item.MaxStock || quantity > item.MaxStock - owned)
            {
                return RpgCampSupplyPurchaseStatus.OutOfStock;
            }

            int unitCost = GetDiscountedUnitCost(mapId, itemId);
            long totalCostLong = (long)unitCost * quantity;
            int balance = Math.Max(0, _getInt(RpgWorld500xFieldBountyAndSupplyService.WalletSaltSilverKey));
            if (totalCostLong > balance)
            {
                return RpgCampSupplyPurchaseStatus.InsufficientSaltSilver;
            }

            int totalCost = (int)totalCostLong;
            int remainingBalance = balance - totalCost;
            _setInt(RpgWorld500xFieldBountyAndSupplyService.WalletSaltSilverKey, remainingBalance);
            // Do not grant supplies until the authoritative wallet confirms
            // the exact debit. A disconnected or read-only wallet must fail closed.
            if (_getInt(RpgWorld500xFieldBountyAndSupplyService.WalletSaltSilverKey) != remainingBalance)
            {
                return RpgCampSupplyPurchaseStatus.WalletWriteFailed;
            }

            _setInt(item.OwnedCountKey, owned + quantity);
            totalCostCharged = totalCost;
            return RpgCampSupplyPurchaseStatus.Succeeded;
        }

        public RpgWorld500xCampHavenSummary RestAndSyncCampHaven(RpgWorld500xSceneBootstrapper bootstrapper)
        {
            if (bootstrapper == null)
            {
                throw new ArgumentNullException(nameof(bootstrapper));
            }

            RpgWorld500xGrandRuntimeFacade facade = bootstrapper.Driver.Facade;
            RpgWorldMapId mapId = facade.CurrentMapId;

            RpgWorld500xAtmosphereSnapshot dawn = facade.ActivityDirector.RestAtCampfire();
            RpgWorld500xMapCompletionSummary completion = facade.GetCompletionSummary();

            List<RpgWorld500xFieldBountyDef> newlyClaimed = facade.BountyAndSupply.ClaimCompletedBounties(completion);
            facade.DistrictTracker.ClaimEligibleMilestones(completion.OverallCompletionPercent);

            IReadOnlyList<RpgWorld500xFieldBountyDef> allBounties = RpgWorld500xBountyAndSupplyCatalog.GetBounties(mapId);
            int claimedBounties = 0;
            for (int i = 0; i < allBounties.Count; i++)
            {
                if (facade.BountyAndSupply.IsBountyClaimed(allBounties[i]))
                {
                    claimedBounties++;
                }
            }

            List<RpgWorld500xDangerZoneDossier> dossiers = facade.ThreatCodex.BuildMapDossiers(facade.Session.Host.Layout);
            int studiedDossiers = 0;
            for (int i = 0; i < dossiers.Count; i++)
            {
                if (dossiers[i].IsStudied)
                {
                    studiedDossiers++;
                }
            }

            // Rest, bounty claims and the exported save all belong to this live
            // bootstrapper. A standalone or read-only haven must not sync a
            // separate reputation store while reporting the session's save.
            RpgRegionalReputationSnapshot rep = bootstrapper.CreateCampHavenService()
                .Reputation.SyncMinimumFromCompletion(
                completion,
                claimedBounties,
                studiedDossiers);

            bootstrapper.StatusHud.SyncFromDriver(bootstrapper.Driver);
            string saveJson = bootstrapper.ExportSaveJson();
            int balance = facade.BountyAndSupply.SaltSilverBalance;

            string summaryZh = "营地休整完成 · 第 " + dawn.DayCount + " 日 " + dawn.FormattedClock +
                               " " + dawn.WeatherDisplayName + "｜" + rep.FactionNameZh + "（" +
                               rep.RankNameZh + "，折扣 " + rep.CampSupplyDiscountPercent + "%）｜盐银 " + balance;

            return new RpgWorld500xCampHavenSummary(
                mapId,
                dawn,
                rep,
                balance,
                newlyClaimed.Count,
                saveJson,
                summaryZh);
        }

        public static RpgWorld500xRoutePlan PlanRouteFromCurrentPosition(
            RpgWorld500xSceneBootstrapper bootstrapper,
            Vector2Int destinationTile)
        {
            if (bootstrapper == null)
            {
                throw new ArgumentNullException(nameof(bootstrapper));
            }

            RpgWorld500xGrandRuntimeFacade facade = bootstrapper.Driver.Facade;
            RpgWorldMapId mapId = facade.CurrentMapId;
            RpgWorld500xAtmosphereSnapshot atmo = facade.Atmosphere.GetSnapshot(mapId);

            return RpgWorld500xFastTravelRoutePlanner.PlanRoute(
                mapId,
                facade.Session.Host.PlayerPosition,
                destinationTile,
                facade.Session.Host.Tracker,
                atmo.MovementSpeedMultiplier,
                fastTravelLockedByEncounter: facade.Session.Host.Bridge.IsInDangerEncounter);
        }
    }
}
