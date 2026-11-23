using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xBestiaryHudPanelTests
    {
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
    }
}
