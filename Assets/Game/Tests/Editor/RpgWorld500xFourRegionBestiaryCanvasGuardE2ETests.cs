using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionBestiaryCanvasGuardE2ETests
    {
        [Test]
        public void AllFourRegions_BestiaryRejectsDisabledCanvasLockedAndHiddenInputUntilRestored()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var root = new GameObject("BestiaryCanvasGuard_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xBestiaryHudPanel(root.transform, boot))
                    {
                        var facade = boot.Driver.Facade;
                        var dossiers = facade.ThreatCodex.BuildMapDossiers(facade.Session.Host.Layout);
                        Assert.Greater(dossiers.Count, 1, spec.MapId.ToString());
                        string markerId = dossiers[0].MarkerId;
                        int bonus = dossiers[0].Profile.CodexStudySilverBonus;
                        var firstTier = dossiers[0].Profile.Tier;

                        Assert.IsTrue(panel.Show(), spec.MapId.ToString());
                        var canvas = root.transform.Find("World500xBestiaryHudPanel").GetComponent<Canvas>();
                        Assert.IsNotNull(canvas, spec.MapId.ToString());
                        var studyBtn = canvas.transform
                            .Find("BestiaryModalBox/DossierRows/DossierRow_" + markerId + "/StudyButton")
                            .GetComponent<Button>();
                        Assert.IsNotNull(studyBtn, spec.MapId.ToString());

                        int initialSilver = facade.BountyAndSupply.SaltSilverBalance;
                        int remaining = panel.RemainingStudySilver;
                        int total = panel.TotalDossierCount;
                        string header = panel.HeaderText;
                        string detail = panel.DetailText;
                        Assert.Greater(remaining, 0, spec.MapId.ToString());

                        // 1. Disabled owning Canvas rejects raw retained button callbacks and public APIs.
                        canvas.enabled = false;
                        Assert.IsTrue(studyBtn.IsInteractable(), spec.MapId.ToString());
                        AssertBlocked(panel, facade, studyBtn, markerId, firstTier,
                            true, total, remaining, initialSilver, header, detail);
                        canvas.enabled = true;

                        // 2. Ancestor CanvasGroup lock rejects raw retained button callbacks and public APIs.
                        var group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;
                        AssertBlocked(panel, facade, studyBtn, markerId, firstTier,
                            true, total, remaining, initialSilver, header, detail);
                        group.interactable = true;

                        // 3. Hidden panel rejects raw retained button callbacks and public APIs.
                        Assert.IsTrue(panel.Hide(), spec.MapId.ToString());
                        AssertBlocked(panel, facade, studyBtn, markerId, firstTier,
                            false, total, remaining, initialSilver, header, detail);
                        Assert.IsTrue(panel.Show(), spec.MapId.ToString());

                        // 4. Restored panel supports single study, filtering, batch study, and idempotent re-click.
                        var restoredBtn = canvas.transform
                            .Find("BestiaryModalBox/DossierRows/DossierRow_" + markerId + "/StudyButton")
                            .GetComponent<Button>();
                        restoredBtn.onClick.Invoke();
                        Assert.AreEqual(1, panel.StudiedCount, spec.MapId.ToString());
                        Assert.AreEqual(initialSilver + bonus, facade.BountyAndSupply.SaltSilverBalance, spec.MapId.ToString());

                        Assert.IsTrue(panel.TrySetUnstudiedOnly(true), spec.MapId.ToString());
                        Assert.AreEqual(total - 1, panel.DisplayedDossierRowCount, spec.MapId.ToString());
                        Assert.IsTrue(panel.TrySetTierFilter(dossiers[1].Profile.Tier), spec.MapId.ToString());
                        Assert.IsTrue(panel.TrySetTierFilter(null), spec.MapId.ToString());

                        Assert.IsTrue(panel.TryStudyAllUnstudied(out int count, out int awarded), spec.MapId.ToString());
                        Assert.AreEqual(total - 1, count, spec.MapId.ToString());
                        Assert.AreEqual(remaining - bonus, awarded, spec.MapId.ToString());
                        Assert.AreEqual(total, panel.StudiedCount, spec.MapId.ToString());
                        Assert.AreEqual(0, panel.RemainingStudySilver, spec.MapId.ToString());
                        Assert.AreEqual(0, panel.DisplayedDossierRowCount, spec.MapId.ToString());
                        Assert.AreEqual(initialSilver + remaining, facade.BountyAndSupply.SaltSilverBalance, spec.MapId.ToString());

                        Assert.IsFalse(panel.TryStudyAllUnstudied(out _, out _), spec.MapId.ToString());
                        restoredBtn.onClick.Invoke();
                        Assert.AreEqual(initialSilver + remaining, facade.BountyAndSupply.SaltSilverBalance, spec.MapId.ToString());
                    }

                    Assert.AreEqual(0, root.transform.childCount, spec.MapId.ToString());
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void AssertBlocked(
            RpgWorld500xBestiaryHudPanel panel,
            RpgWorld500xGrandRuntimeFacade facade,
            Button studyBtn,
            string markerId,
            RpgWorld500xThreatTier tier,
            bool expectedVisible,
            int expectedTotal,
            int expectedRemaining,
            int expectedSilver,
            string expectedHeader,
            string expectedDetail)
        {
            studyBtn.onClick.Invoke();
            Assert.IsFalse(panel.TryClickStudyDossier(markerId));
            Assert.IsFalse(panel.TryStudyAllUnstudied(out int blockedCount, out int blockedSilver));
            Assert.AreEqual(0, blockedCount);
            Assert.AreEqual(0, blockedSilver);
            Assert.IsFalse(panel.TrySetUnstudiedOnly(true));
            Assert.IsFalse(panel.TrySetTierFilter(tier));

            Assert.AreEqual(expectedVisible, panel.IsVisible);
            Assert.IsFalse(panel.UnstudiedOnly);
            Assert.IsNull(panel.TierFilter);
            Assert.AreEqual(0, panel.StudiedCount);
            Assert.AreEqual(expectedTotal, panel.DisplayedDossierRowCount);
            Assert.AreEqual(expectedRemaining, panel.RemainingStudySilver);
            Assert.AreEqual(expectedHeader, panel.HeaderText);
            Assert.AreEqual(expectedDetail, panel.DetailText);
            Assert.AreEqual(expectedSilver, facade.BountyAndSupply.SaltSilverBalance);
            foreach (var dossier in facade.ThreatCodex.BuildMapDossiers(facade.Session.Host.Layout))
                Assert.IsFalse(dossier.IsStudied);
        }
    }
}
