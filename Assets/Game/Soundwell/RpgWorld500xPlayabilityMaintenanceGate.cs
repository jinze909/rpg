using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rpg.Soundwell
{
    public readonly struct RpgWorld500xPlayabilityGateReport
    {
        public RpgWorld500xPlayabilityGateReport(
            bool passed,
            int totalChecks,
            int passedChecks,
            IReadOnlyList<string> issues,
            string formattedReportZh)
        {
            Passed = passed;
            TotalChecks = totalChecks;
            PassedChecks = passedChecks;
            Issues = issues ?? Array.Empty<string>();
            FormattedReportZh = formattedReportZh ?? string.Empty;
        }

        public bool Passed { get; }
        public int TotalChecks { get; }
        public int PassedChecks { get; }
        public IReadOnlyList<string> Issues { get; }
        public string FormattedReportZh { get; }
    }

    /// <summary>
    /// Automated studio maintenance &amp; playability gate auditing the full 500x
    /// four-region expansion stack (World Gazetteer totals, DangerZone tactical
    /// profiles &amp; Lock-Break reward keys, Camp Supply safety &amp; timed buff
    /// evaluation, Weather Travel Advisory risk matrix, Expedition Readiness
    /// scoring, Campfire Journal Digest synthesis, 4x4 multi-hop Fast Travel
    /// Itinerary reachability, 16-panel MenuRouter + PlayMode hotkey coverage,
    /// QuickMenuDockHud 16-button launch &amp; active-highlight sync, and
    /// CampProvisioningService cross-region auto-provisioning).
    /// </summary>
    public static class RpgWorld500xPlayabilityMaintenanceGate
    {
        private static readonly KeyCode[] ExpectedModalHotkeys =
        {
            KeyCode.J, KeyCode.M, KeyCode.B, KeyCode.K,
            KeyCode.T, KeyCode.N, KeyCode.O, KeyCode.L,
            KeyCode.U, KeyCode.C, KeyCode.H, KeyCode.V,
            KeyCode.P, KeyCode.X, KeyCode.G, KeyCode.Y
        };

        public static RpgWorld500xPlayabilityGateReport RunFullAudit(Sprite[] overrideTileSlices = null)
        {
            var issues = new List<string>();
            int totalChecks = 0;
            int passedChecks = 0;

            void Check(bool condition, string failureMessage)
            {
                totalChecks++;
                if (condition)
                {
                    passedChecks++;
                }
                else
                {
                    issues.Add(failureMessage);
                }
            }

            // 1. Cross-Map Gazetteer & World Catalog Integrity
            List<RpgWorld500xRegionGazetteerCard> cards = RpgWorld500xWorldGazetteerService.BuildWorldCards();
            Check(cards.Count == 4, "Expected 4 region gazetteer cards, found " + cards.Count);

            int totalCaches = 0;
            int totalShortcuts = 0;
            int totalBounties = 0;
            int totalAmbients = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                RpgWorld500xRegionGazetteerCard c = cards[i];
                totalCaches += c.TotalCaches;
                totalShortcuts += c.TotalShortcuts;
                totalBounties += c.TotalBounties;
                totalAmbients += c.AmbientZoneCount;
                Check(!string.IsNullOrWhiteSpace(c.FactionNameZh), "Missing FactionNameZh for " + c.MapId);
                Check(c.ConnectedRouteCount > 0, "Region has zero connected atlas routes: " + c.MapId);
            }

            Check(totalCaches == 42, "Expected 42 total hidden caches, found " + totalCaches);
            Check(totalShortcuts == 16, "Expected 16 total unlockable shortcuts, found " + totalShortcuts);
            Check(totalBounties == 12, "Expected 12 total regional bounties, found " + totalBounties);
            Check(totalAmbients == 12, "Expected 12 total ambient zones, found " + totalAmbients);

            // 2. Camp Supply Catalog & Field Supply Use Safety
            IReadOnlyList<RpgWorld500xCampSupplyItemDef> supplies = RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();
            Check(supplies.Count == 6, "Expected 6 camp supplies, found " + supplies.Count);
            var supplyStore = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < supplies.Count; i++)
            {
                RpgWorld500xCampSupplyItemDef item = supplies[i];
                Check(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(item.OwnedCountKey),
                    "Unsafe supply OwnedCountKey: " + item.OwnedCountKey);
                Check(item.SaltSilverCost > 0 && item.MaxStock > 0,
                    "Invalid supply cost/stock for " + item.Id);
                supplyStore[item.OwnedCountKey] = 1;
            }

            var supplyUse = new RpgWorld500xFieldSupplyUseService(
                k => supplyStore.TryGetValue(k, out int v) ? v : 0,
                (k, v) => supplyStore[k] = v);
            RpgWorld500xFieldSupplyUseResult lampRes = supplyUse.TryConsumeSupply("lamp_oil", 5f);
            Check(lampRes.Succeeded && supplyUse.EvaluateActiveModifiers(10f).ActiveBuffCount == 1,
                "FieldSupplyUseService failed to activate lamp_oil timed buff.");

            // 3. Per-Map Live Bootstrapper Audit (DangerZone Brief, Readiness, Weather Advisory, Campfire Digest)
            Sprite[] slices = overrideTileSlices ?? new Sprite[] { null };
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            for (int s = 0; s < specs.Count; s++)
            {
                RpgWorldMapId mapId = specs[s].MapId;
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: mapId,
                    overrideTileSlices: slices))
                {
                    RpgWorld500xRegionThreatBrief brief =
                        RpgWorld500xDangerZoneTacticalBriefService.BuildRegionBrief(boot, elapsedSeconds: 5f);
                    Check(brief.TotalDangerZones > 0, "Map has zero DangerZones: " + mapId);
                    for (int e = 0; e < brief.Entries.Count; e++)
                    {
                        RpgWorld500xDangerZoneTacticalEntry entry = brief.Entries[e];
                        string clearKey = RpgWorld500xDangerVictoryRewardService.FirstClearKey(entry.MarkerId);
                        Check(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(clearKey),
                            "Unsafe DangerZone clear key: " + clearKey);
                        Check(entry.EstimatedLockBreakSilver > entry.EstimatedStandardSilver,
                            "Lock-Break reward did not exceed standard reward for " + entry.MarkerId);
                    }

                    boot.Driver.Facade.Atmosphere.SetWeather(mapId, RpgWorld500xWeatherKind.Clear);
                    RpgWorld500xExpeditionDigest digest =
                        RpgWorld500xCampfireJournalDigestService.BuildDigest(boot, elapsedSeconds: 10f, supplyUseService: supplyUse);
                    Check(digest.MapId == mapId && !string.IsNullOrWhiteSpace(digest.FormattedDigestZh),
                        "Campfire digest failed on " + mapId);

                    boot.Driver.Facade.Atmosphere.SetWeather(mapId, RpgWorld500xWeatherKind.GaleStorm);
                    RpgWorld500xExpeditionReadinessReport stormReadiness =
                        RpgWorld500xExpeditionReadinessService.EvaluateReadiness(boot, elapsedSeconds: 10f, supplyUseService: supplyUse);
                    Check(stormReadiness.TravelRiskLevel == RpgWorld500xTravelRiskLevel.Hazardous &&
                          stormReadiness.Score >= 0 && stormReadiness.Score <= 100 &&
                          !string.IsNullOrWhiteSpace(stormReadiness.ChecklistSummaryZh),
                        "Expedition readiness failed under GaleStorm on " + mapId);
                }
            }

            // 4. All-Pairs 4x4 Multi-Hop Fast Travel Itinerary Reachability
            for (int a = 0; a < specs.Count; a++)
            {
                for (int b = 0; b < specs.Count; b++)
                {
                    RpgWorld500xTravelItinerary itinerary =
                        RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(specs[a].MapId, specs[b].MapId);
                    bool expectedValid = itinerary.IsReachable &&
                        (a == b
                            ? itinerary.HopCount == 0 && itinerary.TotalTravelMinutesInGame == 0
                            : itinerary.HopCount > 0 && itinerary.TotalTravelMinutesInGame > 0);
                    Check(expectedValid, "Invalid itinerary from " + specs[a].MapId + " to " + specs[b].MapId);
                }
            }

            // 5. 16-Panel MenuRouter & PlayMode Hardware Hotkey Coverage
            var mappedModals = new HashSet<RpgWorld500xModalKind>();
            for (int i = 0; i < ExpectedModalHotkeys.Length; i++)
            {
                bool resolved = RpgWorld500xPlayModeController.TryResolveModalHotkey(
                    ExpectedModalHotkeys[i], out RpgWorld500xModalKind kind);
                Check(resolved && kind != RpgWorld500xModalKind.None && mappedModals.Add(kind),
                    "Invalid or duplicate modal hotkey mapping for " + ExpectedModalHotkeys[i]);
            }

            Check(mappedModals.Count == 16, "Expected 16 unique modal hotkeys, found " + mappedModals.Count);

            var uiRoot = new GameObject("World500xPlayabilityGateUiRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: slices))
                using (var router = new RpgWorld500xMenuRouter(uiRoot.transform, boot, boot.CreateCampHavenService()))
                {
                    foreach (RpgWorld500xModalKind modal in mappedModals)
                    {
                        bool opened = router.OpenModal(modal) && router.ActiveModal == modal;
                        Check(opened, "MenuRouter failed to open modal " + modal);
                    }

                    router.CloseAll();
                    Check(router.ActiveModal == RpgWorld500xModalKind.None, "MenuRouter failed to close all modals.");

                    // 6. QuickMenuDockHud 16-Button Launch, Active Highlight Sync & Toggle-Close
                    using (var dock = new RpgWorld500xQuickMenuDockHud(uiRoot.transform, router))
                    {
                        Check(dock.DisplayedButtonCount == 16,
                            "QuickMenuDockHud expected 16 modal buttons, found " + dock.DisplayedButtonCount);

                        RpgWorld500xModalKind lastModal = RpgWorld500xModalKind.None;
                        foreach (RpgWorld500xModalKind modal in mappedModals)
                        {
                            bool clicked = dock.TryClickModalButton(modal) &&
                                router.ActiveModal == modal &&
                                dock.ActiveHighlightedModal == modal;
                            Check(clicked, "QuickMenuDockHud failed to open/highlight modal " + modal);
                            lastModal = modal;
                        }

                        bool toggledClosed = lastModal != RpgWorld500xModalKind.None &&
                            dock.TryClickModalButton(lastModal) &&
                            router.ActiveModal == RpgWorld500xModalKind.None &&
                            dock.ActiveHighlightedModal == RpgWorld500xModalKind.None;
                        Check(toggledClosed, "QuickMenuDockHud failed to toggle-close active modal " + lastModal);
                    }
                }

                Check(Application.isPlaying || uiRoot.transform.childCount == 0,
                    "MenuRouter/QuickMenuDockHud left leaked UI child objects: " + uiRoot.transform.childCount);
            }
            finally
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(uiRoot);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(uiRoot);
                }
            }

            // 7. Cross-Region CampProvisioningService Audit (Strict Budget Guard, Full Stocking & Idempotency)
            using (var provBoot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: slices))
            {
                RpgWorld500xGrandRuntimeFacade provFacade = provBoot.Driver.Facade;
                provFacade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                provFacade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
                provFacade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.GaleStorm);

                RpgWorld500xProvisioningPlan plan = RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                    provBoot, RpgWorldMapId.MineCamp, elapsedSeconds: 5f);
                Check(plan.Itinerary.HopCount == 2 && plan.TotalRequiredSaltSilver > 0 && !plan.CanAffordFullPlan,
                    "CampProvisioningService failed to plan 2-hop hazardous Soundwell->MineCamp route.");

                RpgWorld500xProvisioningReceipt denied = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                    provBoot, RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false, elapsedSeconds: 5f);
                Check(!denied.FullyProvisioned && denied.TotalSpentSaltSilver == 0,
                    "CampProvisioningService spent currency when under budget in strict mode.");

                provFacade.BountyAndSupply.AddSaltSilver(300);
                RpgWorld500xProvisioningReceipt full = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                    provBoot, RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false, elapsedSeconds: 5f);
                Check(full.FullyProvisioned && full.TotalSpentSaltSilver == plan.TotalRequiredSaltSilver &&
                      full.AfterScore > full.BeforeScore && full.AfterGrade == RpgWorld500xReadinessGrade.WellPrepared &&
                      provFacade.BountyAndSupply.GetOwnedSupplyCount("rations") >= 2,
                    "CampProvisioningService failed full provisioning execution for Soundwell->MineCamp.");

                RpgWorld500xProvisioningReceipt repeat = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                    provBoot, RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: true, elapsedSeconds: 5f);
                Check(repeat.AlreadyPrepared && repeat.TotalSpentSaltSilver == 0,
                    "CampProvisioningService was not idempotent on immediate repeat execution.");
            }

            bool passed = issues.Count == 0;
            var sb = new StringBuilder(192);
            sb.Append("【500x 可玩性巡检门禁】")
              .Append(passed ? "通过" : "未通过")
              .Append("（").Append(passedChecks).Append('/').Append(totalChecks).Append(" 项通过）");
            if (!passed)
            {
                sb.Append("｜问题数：").Append(issues.Count);
            }

            return new RpgWorld500xPlayabilityGateReport(
                passed,
                totalChecks,
                passedChecks,
                issues,
                sb.ToString());
        }
    }
}
