using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionCampProvisioningE2ETests
    {
        [Test]
        public void AllFourRegions_AutoProvisioningRejectsUnconfirmedWritesAndRetriesCleanlyWithZeroLeak()
        {
            // Warm up one-time runtime caches before steady-state leak baseline.
            using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                    warmupBoot, RpgWorldMapId.Soundwell, elapsedSeconds: 1f);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            int verifiedRegions = 0;
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: spec.MapId,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    var store = new Dictionary<string, int>(StringComparer.Ordinal);
                    bool acceptWrites = true;
                    boot.Driver.Initialize(
                        overrideMap: spec.MapId,
                        getInt: k => store.TryGetValue(k, out int value) ? value : 0,
                        setInt: (k, v) =>
                        {
                            if (acceptWrites)
                            {
                                store[k] = v;
                            }
                        },
                        overrideTileSlices: new Sprite[] { null });

                    var facade = boot.Driver.Facade;
                    facade.Atmosphere.SetWeather(spec.MapId, RpgWorld500xWeatherKind.GaleStorm);
                    facade.BountyAndSupply.AddSaltSilver(300);

                    var plan = RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                        boot, spec.MapId, elapsedSeconds: 5f);
                    Assert.IsTrue(plan.CanAffordFullPlan, "Expected affordable plan in " + spec.MapId);
                    Assert.Greater(plan.Lines.Count, 0, "Expected non-empty provisioning lines in " + spec.MapId);

                    // Silently rejected persistence writes must not report completion or debit salt silver.
                    acceptWrites = false;
                    var rejected = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                        boot, spec.MapId, allowPartialIfUnderBudget: false, elapsedSeconds: 5f);
                    Assert.IsFalse(rejected.AlreadyPrepared);
                    Assert.IsFalse(rejected.FullyProvisioned);
                    Assert.AreEqual(0, rejected.TotalSpentSaltSilver);
                    Assert.AreEqual(300, rejected.RemainingSaltSilver);
                    Assert.IsEmpty(rejected.PurchasedLines);
                    Assert.AreEqual(plan.Lines.Count, rejected.SkippedItemIds.Count);
                    Assert.AreEqual(rejected.BeforeScore, rejected.AfterScore);
                    StringAssert.Contains("采购未完整确认", rejected.SummaryZh);

                    // Once writes succeed, retrying completes full provisioning and raises readiness score.
                    acceptWrites = true;
                    var retry = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                        boot, spec.MapId, allowPartialIfUnderBudget: false, elapsedSeconds: 5f);
                    Assert.IsTrue(retry.FullyProvisioned);
                    Assert.AreEqual(plan.TotalRequiredSaltSilver, retry.TotalSpentSaltSilver);
                    Assert.AreEqual(300 - retry.TotalSpentSaltSilver, retry.RemainingSaltSilver);
                    Assert.IsEmpty(retry.SkippedItemIds);
                    Assert.Greater(retry.AfterScore, retry.BeforeScore);

                    verifiedRegions++;
                }
            }

            Assert.AreEqual(4, verifiedRegions);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
