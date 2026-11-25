using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xGazetteerEntryKind : byte
    {
        SupplyCache = 0,
        Shortcut = 1,
        Bounty = 2,
        AmbientZone = 3
    }

    public readonly struct RpgWorld500xGazetteerSearchHit
    {
        public RpgWorld500xGazetteerSearchHit(
            RpgWorldMapId mapId,
            string mapDisplayNameZh,
            RpgWorld500xGazetteerEntryKind kind,
            string id,
            string titleZh,
            string detailZh,
            bool isCompleted)
        {
            MapId = mapId;
            MapDisplayNameZh = mapDisplayNameZh ?? string.Empty;
            Kind = kind;
            Id = id ?? string.Empty;
            TitleZh = titleZh ?? string.Empty;
            DetailZh = detailZh ?? string.Empty;
            IsCompleted = isCompleted;
        }

        public RpgWorldMapId MapId { get; }
        public string MapDisplayNameZh { get; }
        public RpgWorld500xGazetteerEntryKind Kind { get; }
        public string Id { get; }
        public string TitleZh { get; }
        public string DetailZh { get; }
        public bool IsCompleted { get; }
    }

    public readonly struct RpgWorld500xRegionGazetteerCard
    {
        public RpgWorld500xRegionGazetteerCard(
            RpgWorldMapId mapId,
            string mapDisplayNameZh,
            string factionNameZh,
            string reputationRankZh,
            int campDiscountPercent,
            int lootedCaches,
            int totalCaches,
            int unlockedShortcuts,
            int totalShortcuts,
            int claimedBounties,
            int totalBounties,
            int ambientZoneCount,
            int connectedRouteCount,
            int completionPercent,
            string formattedCardZh,
            int routeHopCount = 0,
            bool isHazardousRoute = false,
            int provisioningCostSaltSilver = 0,
            bool canAffordProvisioning = true,
            bool hasLiveProvisioningData = false)
        {
            MapId = mapId;
            MapDisplayNameZh = mapDisplayNameZh ?? string.Empty;
            FactionNameZh = factionNameZh ?? string.Empty;
            ReputationRankZh = reputationRankZh ?? string.Empty;
            CampDiscountPercent = campDiscountPercent;
            LootedCaches = lootedCaches;
            TotalCaches = totalCaches;
            UnlockedShortcuts = unlockedShortcuts;
            TotalShortcuts = totalShortcuts;
            ClaimedBounties = claimedBounties;
            TotalBounties = totalBounties;
            AmbientZoneCount = ambientZoneCount;
            ConnectedRouteCount = connectedRouteCount;
            CompletionPercent = completionPercent;
            FormattedCardZh = formattedCardZh ?? string.Empty;
            RouteHopCount = Math.Max(0, routeHopCount);
            IsHazardousRoute = isHazardousRoute;
            ProvisioningCostSaltSilver = Math.Max(0, provisioningCostSaltSilver);
            CanAffordProvisioning = canAffordProvisioning;
            HasLiveProvisioningData = hasLiveProvisioningData;
        }

        public RpgWorldMapId MapId { get; }
        public string MapDisplayNameZh { get; }
        public string FactionNameZh { get; }
        public string ReputationRankZh { get; }
        public int CampDiscountPercent { get; }
        public int LootedCaches { get; }
        public int TotalCaches { get; }
        public int UnlockedShortcuts { get; }
        public int TotalShortcuts { get; }
        public int ClaimedBounties { get; }
        public int TotalBounties { get; }
        public int AmbientZoneCount { get; }
        public int ConnectedRouteCount { get; }
        public int CompletionPercent { get; }
        public string FormattedCardZh { get; }
        public int RouteHopCount { get; }
        public bool IsHazardousRoute { get; }
        public int ProvisioningCostSaltSilver { get; }
        public bool CanAffordProvisioning { get; }
        public bool HasLiveProvisioningData { get; }
    }

    /// <summary>
    /// Cross-map 500x World Gazetteer &amp; Search Directory across all four playable
    /// regions (Soundwell, RedSaltHarbor, WhitewaterCrossing, MineCamp). Summarizes
    /// live cache/shortcut/bounty/ambient coverage, faction standing, and optional live
    /// route-risk &amp; camp-provisioning readiness per region, plus deterministic Chinese
    /// keyword lookup across world catalogs.
    /// </summary>
    public static class RpgWorld500xWorldGazetteerService
    {
        public static List<RpgWorld500xRegionGazetteerCard> BuildWorldCards(
            Func<string, int> getInt = null)
        {
            Func<string, int> read = getInt ?? (_ => 0);
            var repService = new RpgWorld500xRegionalReputationService(read);
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            var cards = new List<RpgWorld500xRegionGazetteerCard>(specs.Count);

            for (int i = 0; i < specs.Count; i++)
            {
                RpgExpandedMapSpec spec = specs[i];
                RpgWorldMapId mapId = spec.MapId;

                IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(mapId);
                int looted = 0;
                for (int c = 0; c < caches.Count; c++)
                {
                    if (read(caches[c].MakinomKey) == 1)
                    {
                        looted++;
                    }
                }

                IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(mapId);
                int unlocked = 0;
                for (int s = 0; s < shortcuts.Count; s++)
                {
                    if (read(shortcuts[s].MakinomKey) == 1)
                    {
                        unlocked++;
                    }
                }

                IReadOnlyList<RpgWorld500xFieldBountyDef> bounties = RpgWorld500xBountyAndSupplyCatalog.GetBounties(mapId);
                int claimed = 0;
                for (int b = 0; b < bounties.Count; b++)
                {
                    if (read(bounties[b].MakinomKey) == 1)
                    {
                        claimed++;
                    }
                }

                IReadOnlyList<RpgAmbientMicroEventZoneDef> ambients = RpgWorld500xLivingWorldCatalog.GetAmbientZones(mapId);
                List<RpgWorld500xRegionRouteDef> routes = RpgWorld500xWorldAtlasPortalService.GetConnectedRegions(mapId);
                RpgRegionalReputationSnapshot rep = repService.GetSnapshot(mapId);

                int totalTrackable = caches.Count + shortcuts.Count + bounties.Count;
                int completedTrackable = looted + unlocked + claimed;
                int pct = totalTrackable > 0
                    ? Mathf.Clamp(Mathf.RoundToInt((completedTrackable * 100f) / totalTrackable), 0, 100)
                    : 0;

                var sb = new StringBuilder(160);
                sb.Append("【").Append(spec.DisplayNameZh).Append("】")
                  .Append(rep.FactionNameZh).Append("「").Append(rep.RankNameZh).Append("」")
                  .Append("｜补给箱 ").Append(looted).Append('/').Append(caches.Count)
                  .Append("｜捷径 ").Append(unlocked).Append('/').Append(shortcuts.Count)
                  .Append("｜悬赏 ").Append(claimed).Append('/').Append(bounties.Count)
                  .Append("｜完成度 ").Append(pct).Append('%');

                cards.Add(new RpgWorld500xRegionGazetteerCard(
                    mapId,
                    spec.DisplayNameZh,
                    rep.FactionNameZh,
                    rep.RankNameZh,
                    rep.CampSupplyDiscountPercent,
                    looted,
                    caches.Count,
                    unlocked,
                    shortcuts.Count,
                    claimed,
                    bounties.Count,
                    ambients.Count,
                    routes.Count,
                    pct,
                    sb.ToString()));
            }

            return cards;
        }

        public static List<RpgWorld500xRegionGazetteerCard> BuildLiveWorldCards(
            RpgWorld500xSceneBootstrapper bootstrapper,
            Func<string, int> getInt = null,
            float elapsedSeconds = 0f,
            RpgWorld500xFieldSupplyUseService supplyUseService = null)
        {
            if (bootstrapper == null || bootstrapper.IsDisposed)
            {
                throw new ArgumentNullException(nameof(bootstrapper));
            }

            RpgWorldMapId originMapId = bootstrapper.Driver.Facade.CurrentMapId;
            List<RpgWorld500xRegionGazetteerCard> baseCards = BuildWorldCards(getInt);
            var enriched = new List<RpgWorld500xRegionGazetteerCard>(baseCards.Count);

            for (int i = 0; i < baseCards.Count; i++)
            {
                RpgWorld500xRegionGazetteerCard card = baseCards[i];
                RpgWorld500xProvisioningPlan plan = RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                    bootstrapper, card.MapId, elapsedSeconds, supplyUseService);

                int hopCount = plan.Itinerary.HopCount;
                bool hazardous = plan.Itinerary.HighestRiskLevel == RpgWorld500xTravelRiskLevel.Hazardous;
                int restockCost = plan.TotalRequiredSaltSilver;
                bool canAfford = plan.CanAffordFullPlan;

                var sb = new StringBuilder(card.FormattedCardZh.Length + 64);
                sb.Append(card.FormattedCardZh);
                if (card.MapId == originMapId)
                {
                    sb.Append("｜本境");
                }
                else
                {
                    sb.Append("｜").Append(hopCount).Append(hazardous ? "跳险途" : "跳可达");
                }

                if (plan.Lines.Count == 0)
                {
                    sb.Append("｜行囊已齐备");
                }
                else
                {
                    sb.Append("｜待补 ").Append(restockCost).Append(" 盐银")
                      .Append(canAfford ? "（可备齐）" : "（盐银不足）");
                }

                enriched.Add(new RpgWorld500xRegionGazetteerCard(
                    card.MapId,
                    card.MapDisplayNameZh,
                    card.FactionNameZh,
                    card.ReputationRankZh,
                    card.CampDiscountPercent,
                    card.LootedCaches,
                    card.TotalCaches,
                    card.UnlockedShortcuts,
                    card.TotalShortcuts,
                    card.ClaimedBounties,
                    card.TotalBounties,
                    card.AmbientZoneCount,
                    card.ConnectedRouteCount,
                    card.CompletionPercent,
                    sb.ToString(),
                    routeHopCount: hopCount,
                    isHazardousRoute: hazardous,
                    provisioningCostSaltSilver: restockCost,
                    canAffordProvisioning: canAfford,
                    hasLiveProvisioningData: true));
            }

            return enriched;
        }

        public static List<RpgWorld500xGazetteerSearchHit> SearchGazetteer(
            string keywordZh,
            Func<string, int> getInt = null,
            RpgWorldMapId? filterMapId = null)
        {
            var hits = new List<RpgWorld500xGazetteerSearchHit>();
            if (string.IsNullOrWhiteSpace(keywordZh))
            {
                return hits;
            }

            string needle = keywordZh.Trim();
            Func<string, int> read = getInt ?? (_ => 0);
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;

            for (int i = 0; i < specs.Count; i++)
            {
                RpgExpandedMapSpec spec = specs[i];
                if (filterMapId.HasValue && spec.MapId != filterMapId.Value)
                {
                    continue;
                }

                RpgWorldMapId mapId = spec.MapId;

                IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(mapId);
                for (int c = 0; c < caches.Count; c++)
                {
                    RpgSupplyCacheDef item = caches[c];
                    if (ContainsIgnoreCase(item.TitleZh, needle) || ContainsIgnoreCase(item.Id, needle))
                    {
                        hits.Add(new RpgWorld500xGazetteerSearchHit(
                            mapId,
                            spec.DisplayNameZh,
                            RpgWorld500xGazetteerEntryKind.SupplyCache,
                            item.Id,
                            item.TitleZh,
                            "盐银 +" + item.SaltSilverReward + "｜坐标(" + item.Tile.x + "," + item.Tile.y + ")",
                            read(item.MakinomKey) == 1));
                    }
                }

                IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(mapId);
                for (int s = 0; s < shortcuts.Count; s++)
                {
                    RpgWorldShortcutDef sc = shortcuts[s];
                    if (ContainsIgnoreCase(sc.TitleZh, needle) ||
                        ContainsIgnoreCase(sc.UnlockedToastZh, needle) ||
                        ContainsIgnoreCase(sc.Id, needle))
                    {
                        hits.Add(new RpgWorld500xGazetteerSearchHit(
                            mapId,
                            spec.DisplayNameZh,
                            RpgWorld500xGazetteerEntryKind.Shortcut,
                            sc.Id,
                            sc.TitleZh,
                            "触发点(" + sc.TriggerTile.x + "," + sc.TriggerTile.y + ")",
                            read(sc.MakinomKey) == 1));
                    }
                }

                IReadOnlyList<RpgWorld500xFieldBountyDef> bounties = RpgWorld500xBountyAndSupplyCatalog.GetBounties(mapId);
                for (int b = 0; b < bounties.Count; b++)
                {
                    RpgWorld500xFieldBountyDef def = bounties[b];
                    if (ContainsIgnoreCase(def.TitleZh, needle) ||
                        ContainsIgnoreCase(def.DescriptionZh, needle) ||
                        ContainsIgnoreCase(def.Id, needle))
                    {
                        hits.Add(new RpgWorld500xGazetteerSearchHit(
                            mapId,
                            spec.DisplayNameZh,
                            RpgWorld500xGazetteerEntryKind.Bounty,
                            def.Id,
                            def.TitleZh,
                            def.DescriptionZh + "（盐银 +" + def.SaltSilverReward + "）",
                            read(def.MakinomKey) == 1));
                    }
                }

                IReadOnlyList<RpgAmbientMicroEventZoneDef> ambients = RpgWorld500xLivingWorldCatalog.GetAmbientZones(mapId);
                for (int a = 0; a < ambients.Count; a++)
                {
                    RpgAmbientMicroEventZoneDef zone = ambients[a];
                    if (ContainsIgnoreCase(zone.CaptionZh, needle) || ContainsIgnoreCase(zone.Id, needle))
                    {
                        hits.Add(new RpgWorld500xGazetteerSearchHit(
                            mapId,
                            spec.DisplayNameZh,
                            RpgWorld500xGazetteerEntryKind.AmbientZone,
                            zone.Id,
                            zone.CaptionZh,
                            "环境微事件区",
                            false));
                    }
                }
            }

            return hits;
        }

        private static bool ContainsIgnoreCase(string source, string needle)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
