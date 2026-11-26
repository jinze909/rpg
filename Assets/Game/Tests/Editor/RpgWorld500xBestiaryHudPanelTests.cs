using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xBestiaryHudPanelTests
    {
        [Test]
        public void BestiaryHudPanel_RejectsHiddenDisabledRetiredAndDisposedStudyActions()
        {
            var root = new GameObject("BestiaryStudyLifetimeRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    var facade = boot.Driver.Facade;
                    var dossiers = facade.ThreatCodex.BuildMapDossiers(facade.Session.Host.Layout);
                    Assert.Greater(dossiers.Count, 0);
                    string markerId = dossiers[0].MarkerId;
                    int bonus = dossiers[0].Profile.CodexStudySilverBonus;
                    int initialSilver = facade.BountyAndSupply.SaltSilverBalance;
                    UnityEngine.UI.Button StudyButton() => root.transform
                        .Find("World500xBestiaryHudPanel/BestiaryModalBox/DossierRows/DossierRow_" +
                              markerId + "/StudyButton").GetComponent<UnityEngine.UI.Button>();
                    void AssertNotStudied()
                    {
                        Assert.AreEqual(initialSilver, facade.BountyAndSupply.SaltSilverBalance);
                        Assert.IsFalse(facade.ThreatCodex.BuildMapDossiers(facade.Session.Host.Layout)[0].IsStudied);
                    }

                    using (var panel = new RpgWorld500xBestiaryHudPanel(root.transform, boot))
                    {
                        StudyButton().onClick.Invoke();
                        Assert.IsFalse(panel.TryClickStudyDossier(markerId));
                        AssertNotStudied();

                        Assert.IsTrue(panel.Show());
                        var retired = StudyButton().onClick;
                        Assert.IsTrue(panel.Hide());
                        retired.Invoke();
                        Assert.IsFalse(panel.TryClickStudyDossier(markerId));
                        AssertNotStudied();

                        Assert.IsTrue(panel.Show());
                        retired.Invoke();
                        var current = StudyButton();
                        current.interactable = false;
                        current.onClick.Invoke();
                        Assert.IsFalse(panel.TryClickStudyDossier(markerId));
                        AssertNotStudied();

                        current.interactable = true;
                        panel.Refresh();
                        current.onClick.Invoke();
                        AssertNotStudied();

                        var disposed = StudyButton().onClick;
                        panel.Dispose();
                        disposed.Invoke();
                        Assert.IsFalse(panel.TryClickStudyDossier(markerId));
                        AssertNotStudied();
                    }

                    using (var reopened = new RpgWorld500xBestiaryHudPanel(root.transform, boot))
                    {
                        Assert.IsTrue(reopened.Show());
                        Assert.IsTrue(reopened.TryClickStudyDossier(markerId));
                        Assert.IsTrue(facade.ThreatCodex.BuildMapDossiers(facade.Session.Host.Layout)[0].IsStudied);
                        Assert.AreEqual(initialSilver + bonus, facade.BountyAndSupply.SaltSilverBalance);
                        Assert.IsFalse(reopened.TryClickStudyDossier(markerId));
                        Assert.AreEqual(initialSilver + bonus, facade.BountyAndSupply.SaltSilverBalance);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BestiaryHudPanel_StudiesDangerZoneDossiersCreditsSilverAndDisposesWithZeroLeak()
        {
            var root = new GameObject("World500xBestiaryHudPanelTestRoot");
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

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xBestiaryHudPanel(root.transform, boot))
                {
                    Assert.IsTrue(panel.Show());
                    List<RpgWorld500xDangerZoneDossier> dossiers =
                        boot.Driver.Facade.ThreatCodex.BuildMapDossiers(boot.Driver.Facade.Session.Host.Layout);
                    Assert.AreEqual(dossiers.Count, panel.DisplayedDossierRowCount);
                    Assert.Greater(dossiers.Count, 0);

                    string firstId = dossiers[0].MarkerId;
                    int bonus = dossiers[0].Profile.CodexStudySilverBonus;

                    Assert.IsTrue(panel.TryClickStudyDossier(firstId));
                    Assert.IsTrue(panel.DetailText.Contains("战术建议"));
                    Assert.GreaterOrEqual(boot.Driver.Facade.BountyAndSupply.SaltSilverBalance, bonus);

                    // Studying same dossier twice is disabled/idempotent.
                    Assert.IsFalse(panel.TryClickStudyDossier(firstId));

                    Assert.IsTrue(panel.Hide());
                    Assert.IsFalse(panel.IsVisible);
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BestiaryHudPanel_SupportsUnstudiedFilterRemainingSilverAndBatchStudyRespectingCanvasGroupLock()
        {
            var root = new GameObject("BestiaryFilterBatchRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xBestiaryHudPanel(root.transform, boot))
                {
                    // Hidden panel rejects filter and batch-study actions cleanly.
                    Assert.IsFalse(panel.TrySetUnstudiedOnly(true));
                    Assert.IsFalse(panel.TryStudyAllUnstudied(out _, out _));

                    Assert.IsTrue(panel.Show());
                    int total = panel.TotalDossierCount;
                    Assert.Greater(total, 1);
                    Assert.AreEqual(0, panel.StudiedCount);
                    int initialRemainingSilver = panel.RemainingStudySilver;
                    Assert.Greater(initialRemainingSilver, 0);
                    StringAssert.Contains("待研读盐银 +" + initialRemainingSilver, panel.HeaderText);

                    // Study first dossier and enable UnstudiedOnly filter -> DisplayedDossierRowCount drops by 1.
                    List<RpgWorld500xDangerZoneDossier> dossiers =
                        boot.Driver.Facade.ThreatCodex.BuildMapDossiers(boot.Driver.Facade.Session.Host.Layout);
                    int firstBonus = dossiers[0].Profile.CodexStudySilverBonus;
                    Assert.IsTrue(panel.TryClickStudyDossier(dossiers[0].MarkerId));
                    Assert.AreEqual(1, panel.StudiedCount);
                    Assert.AreEqual(initialRemainingSilver - firstBonus, panel.RemainingStudySilver);

                    Assert.IsTrue(panel.TrySetUnstudiedOnly(true));
                    Assert.IsTrue(panel.UnstudiedOnly);
                    Assert.AreEqual(total - 1, panel.DisplayedDossierRowCount);

                    // Ancestor CanvasGroup lock blocks filter changes and batch study cleanly.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TrySetUnstudiedOnly(false));
                    Assert.IsFalse(panel.TryStudyAllUnstudied(out _, out _));
                    Assert.AreEqual(1, panel.StudiedCount);
                    group.interactable = true;

                    // Batch study finishes all remaining dossiers idempotently.
                    Assert.IsTrue(panel.TryStudyAllUnstudied(out int batchCount, out int batchSilver));
                    Assert.AreEqual(total - 1, batchCount);
                    Assert.AreEqual(initialRemainingSilver - firstBonus, batchSilver);
                    Assert.AreEqual(total, panel.StudiedCount);
                    Assert.AreEqual(0, panel.RemainingStudySilver);
                    Assert.AreEqual(0, panel.DisplayedDossierRowCount);
                    StringAssert.Contains("已批量研读", panel.DetailText);
                    Assert.AreEqual(initialRemainingSilver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                    // Second batch study is idempotent and returns false.
                    Assert.IsFalse(panel.TryStudyAllUnstudied(out _, out _));

                    // Clearing UnstudiedOnly restores all rows.
                    Assert.IsTrue(panel.TrySetUnstudiedOnly(false));
                    Assert.AreEqual(total, panel.DisplayedDossierRowCount);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}