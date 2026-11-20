using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgStoryArtResolverTests
    {
        [Test]
        public void DefaultManifestResolvesAllM02M03M04M07StorySpritesWithPointFiltering()
        {
            RpgPixelArtManifest manifest = RpgPixelArtManifest.LoadDefault();
            Assert.That(manifest, Is.Not.Null);

            // M02
            AssertPixelSprite(RpgStoryArtResolver.ResolveM02EvidenceSprite(M02BridgeEvidence.CleanCableCut),
                manifest.M02CableFlatUncharredCut);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM02EvidenceSprite(M02BridgeEvidence.WarReportDraft),
                manifest.M02OverwrittenBattleReport);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM02LedgerSprite(), manifest.M02BridgeKeeperOpenLedger);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM02DocumentSprite(true), manifest.M02IdentityDocumentSealed);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM02DocumentSprite(false), manifest.M02IdentityDocumentUnsealed);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM02OfficialSeal(), manifest.M02HarborOfficialSeal);
            Assert.That(RpgStoryArtResolver.ResolveM02EvidenceSprite(M02BridgeEvidence.None), Is.Null);

            // M03
            AssertPixelSprite(RpgStoryArtResolver.ResolveM03LuJunSprite(false), manifest.M03LuJunStanding);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM03LuJunSprite(true), manifest.M03LuJunRollCall);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM03VeteranSprite(0), manifest.M03VeteranNetMender);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM03VeteranSprite(1), manifest.M03VeteranForwardLookout);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM03VeteranSprite(2), manifest.M03VeteranTidalGuide);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM03VeteranSprite(3), manifest.M03VeteranStandardBearer);
            Assert.That(RpgStoryArtResolver.ResolveM03VeteranSprite(99), Is.Null);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM03StandardSprite(false, 0), manifest.M03OldStandardRolled);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM03StandardSprite(true, 0), manifest.M03OldStandardRaisedA);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM03StandardSprite(true, -1), manifest.M03OldStandardRaisedB);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM03NetRackSprite(true), manifest.M03NetDryingRackLoaded);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM03NetRackSprite(false), manifest.M03NetDryingRackEmpty);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM03TidalChartSprite(true), manifest.M03TidalChartOpen);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM03TidalChartSprite(false), manifest.M03TidalChartFolded);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM03CampCoalsSprite(), manifest.M03CampCoals);

            // M04
            AssertPixelSprite(RpgStoryArtResolver.ResolveM04LanternSprite(true), manifest.FogSignalLanternOn);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM04LanternSprite(false), manifest.FogSignalLanternOff);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM04FogLayerSprite(true, 1), manifest.FogBankDense);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM04FogLayerSprite(false, 0), manifest.FogBankLowA);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM04FogLayerSprite(false, 1), manifest.FogBankLowB);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM04ReefSprite(false, true), manifest.FogReefWash);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM04ReefSprite(true, false), manifest.FogReefHintExposed);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM04ReefSprite(false, false), manifest.FogReefHintSubmerged);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM04WakeSprite(true, 0), manifest.FogBowWakeA);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM04WakeSprite(true, 1), manifest.FogBowWakeB);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM04WakeSprite(false, 0), manifest.FogSternWakeA);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM04WakeSprite(false, 1), manifest.FogSternWakeB);

            // M07
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07HiddenLockSprite(true), manifest.M07OulinHiddenLockOpen);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07HiddenLockSprite(false), manifest.M07OulinHiddenLockClosed);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07MainPumpLeverSprite(true), manifest.M07MainPumpLeverCutoff);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07MainPumpLeverSprite(false), manifest.M07MainPumpLeverRunning);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07MusterMarkSprite(true), manifest.M07UpperMusterChalk);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07MusterMarkSprite(false), manifest.M07UpperMusterSign);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07CargoCrateSprite(true), manifest.M07CargoCoverCrateOpen);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07CargoCrateSprite(false), manifest.M07CargoCoverCrateClosed);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07PassSprite(true), manifest.M07InspectionStampedPass);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07PassSprite(false), manifest.M07InspectionPendingPass);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07BerthTagSprite(true), manifest.M07BerthTagBoarded);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07BerthTagSprite(false), manifest.M07BerthTagAvailable);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07PumpSprite(true), manifest.M07BackupHandPumpWorking);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07PumpSprite(false), manifest.M07BackupHandPumpIdle);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07SluiceSprite(true), manifest.M07SluiceReleasedOpen);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07SluiceSprite(false), manifest.M07SluiceLockedShut);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07GangwaySprite(true), manifest.M07BoardingGangwayLowered);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07GangwaySprite(false), manifest.M07BoardingGangwayRaised);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07BarrierSprite(true), manifest.M07InspectionBarrierOpen);
            AssertPixelSprite(RpgStoryArtResolver.ResolveM07BarrierSprite(false), manifest.M07InspectionBarrierClosed);
        }

        [Test]
        public void CreateOrUpdatePixelIconEnforcesPixelFidelityNullGuardAndIdempotency()
        {
            var root = new GameObject("Story art icon test", typeof(RectTransform));
            var emptyManifest = ScriptableObject.CreateInstance<RpgPixelArtManifest>();
            try
            {
                var parent = (RectTransform)root.transform;
                Sprite seal = RpgStoryArtResolver.ResolveM02OfficialSeal();

                Assert.That(RpgStoryArtResolver.CreateOrUpdatePixelIcon(null, "MissingParent",
                    Vector2.zero, new Vector2(64f, 64f), seal), Is.Null);
                Assert.That(RpgStoryArtResolver.ResolveM02OfficialSeal(emptyManifest), Is.Null);

                Image icon = RpgStoryArtResolver.CreateOrUpdatePixelIcon(parent, "OfficialSeal",
                    new Vector2(12.4f, -8.6f), new Vector2(64f, 64f), seal);
                Assert.That(icon, Is.Not.Null);
                Assert.That(icon.sprite, Is.EqualTo(seal));
                Assert.That(icon.preserveAspect, Is.True);
                Assert.That(icon.raycastTarget, Is.False);
                Assert.That(icon.enabled, Is.True);
                Assert.That(icon.gameObject.activeSelf, Is.True);
                Assert.That(icon.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(12f, -9f)));
                Assert.That(parent.childCount, Is.EqualTo(1));

                // Re-mounting with null hides the Image and reuses the exact same child (no white box or duplicates).
                Image updated = RpgStoryArtResolver.CreateOrUpdatePixelIcon(parent, "OfficialSeal",
                    new Vector2(16f, -12f), new Vector2(64f, 64f), null);
                Assert.That(updated, Is.SameAs(icon));
                Assert.That(parent.childCount, Is.EqualTo(1));
                Assert.That(updated.sprite, Is.Null);
                Assert.That(updated.enabled, Is.False);
                Assert.That(updated.gameObject.activeSelf, Is.False);

                // Re-applying a valid sprite restores visibility cleanly.
                RpgStoryArtResolver.ApplyPixelSprite(updated, seal);
                Assert.That(updated.sprite, Is.EqualTo(seal));
                Assert.That(updated.enabled, Is.True);
                Assert.That(updated.gameObject.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(emptyManifest);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AssertPixelSprite(Sprite actual, Sprite expected)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(actual.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(actual.pixelsPerUnit, Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
        }
    }
}
