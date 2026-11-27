using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgWorld500xBestiaryHudPanel covering
    /// hidden/ancestor-CanvasGroup input guards, UnstudiedOnly and ThreatTier filters,
    /// single and batch dossier study silver rewards, codex summary updates, and zero
    /// native Texture2D/Sprite leaks across Soundwell, RedSaltHarbor,
    /// WhitewaterCrossing, and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionBestiaryHudFilterAndBatchStudyE2ETests
    {
        [Test]
        public void AllFourRegions_BestiaryHudPanel_SupportsFiltersCanvasGroupGuardAndBatchStudyWithZeroLeak()
        {
            var root = new GameObject("FourRegionBestiaryHudRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xBestiaryHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                Assert.AreEqual(4, specs.Count);

                for (int r = 0; r < specs.Count; r++)
                {
                    RpgExpandedMapSpec spec = specs[r];
                    var regionRoot = new GameObject("RegionBestiary_" + spec.MapId);
                    regionRoot.transform.SetParent(root.transform, false);

                    try
                    {
                        using (var boot = new RpgWorld500xSceneBootstrapper(
                            initialMapId: spec.MapId,
                            overrideTileSlices: new Sprite[] { null }))
                        using (var panel = new RpgWorld500xBestiaryHudPanel(regionRoot.transform, boot))
                        {
                            // Hidden panel rejects filter and batch-study actions cleanly.
                            Assert.IsFalse(panel.TrySetUnstudiedOnly(true));
                            Assert.IsFalse(panel.TrySetTierFilter(RpgWorld500xThreatTier.HazardBoss));
                            Assert.IsFalse(panel.TryStudyAllUnstudied(out _, out _));

                            Assert.IsTrue(panel.Show(), $"BestiaryHudPanel failed to show on {spec.MapId}.");
                            int total = panel.TotalDossierCount;
                            int initialRemainingSilver = panel.RemainingStudySilver;

                            Assert.GreaterOrEqual(total, 2, $"Expected at least 2 dossiers on {spec.MapId}.");
                            Assert.Greater(initialRemainingSilver, 0, $"Expected positive remaining silver on {spec.MapId}.");
                            Assert.AreEqual(total, panel.LastCodexSummary.TotalDossiers);
                            StringAssert.Contains("待研读盐银 +" + initialRemainingSilver, panel.HeaderText);
                            StringAssert.Contains("【图鉴考据】", panel.CodexSummaryText);
                            Assert.Greater(panel.RecommendedUnstudiedSupplyIds.Count, 0);

                            // Study first dossier and enable UnstudiedOnly filter.
                            List<RpgWorld500xDangerZoneDossier> dossiers =
                                boot.Driver.Facade.ThreatCodex.BuildMapDossiers(boot.Driver.Facade.Session.Host.Layout);
                            Assert.IsTrue(panel.TryClickStudyDossier(dossiers[0].MarkerId));
                            Assert.AreEqual(1, panel.StudiedCount);
                            Assert.IsTrue(panel.TrySetUnstudiedOnly(true));
                            Assert.IsTrue(panel.UnstudiedOnly);
                            Assert.AreEqual(total - 1, panel.DisplayedDossierRowCount);

                            // Filter by threat tier of an unstudied dossier, then clear tier filter.
                            RpgWorld500xThreatTier sampleTier = dossiers[1].Profile.Tier;
                            Assert.IsTrue(panel.TrySetTierFilter(sampleTier));
                            Assert.AreEqual(sampleTier, panel.TierFilter);
                            Assert.Greater(panel.DisplayedDossierRowCount, 0);
                            Assert.LessOrEqual(panel.DisplayedDossierRowCount, total - 1);
                            Assert.IsTrue(panel.TrySetTierFilter(null));
                            Assert.IsNull(panel.TierFilter);
                            Assert.AreEqual(total - 1, panel.DisplayedDossierRowCount);

                            // Ancestor CanvasGroup lock blocks filter changes and batch study cleanly.
                            CanvasGroup group = regionRoot.AddComponent<CanvasGroup>();
                            group.interactable = false;
                            Assert.IsFalse(panel.TrySetUnstudiedOnly(false));
                            Assert.IsFalse(panel.TrySetTierFilter(sampleTier));
                            Assert.IsFalse(panel.TryStudyAllUnstudied(out _, out _));
                            Assert.AreEqual(1, panel.StudiedCount);
                            group.interactable = true;

                            // Unlock and batch-study remaining dossiers.
                            Assert.IsTrue(panel.TryStudyAllUnstudied(out int batchStudied, out int batchSilver));
                            Assert.AreEqual(total - 1, batchStudied);
                            Assert.Greater(batchSilver, 0);
                            Assert.AreEqual(total, panel.StudiedCount);
                            Assert.AreEqual(0, panel.RemainingStudySilver);
                            Assert.AreEqual(0, panel.DisplayedDossierRowCount);
                            Assert.AreEqual(0, panel.RecommendedUnstudiedSupplyIds.Count);
                            StringAssert.Contains("已全部收录", panel.CodexSummaryText);
                            StringAssert.Contains("已批量研读", panel.DetailText);
                            Assert.AreEqual(initialRemainingSilver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                            // Subsequent batch study is a clean no-op.
                            Assert.IsFalse(panel.TryStudyAllUnstudied(out _, out _));
                            Assert.IsTrue(panel.Hide());
                            Assert.IsFalse(panel.IsVisible);
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(regionRoot);
                    }
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore,
                    "Texture2D leak detected after four-region BestiaryHudPanel sweep.");
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore,
                    "Sprite leak detected after four-region BestiaryHudPanel sweep.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
