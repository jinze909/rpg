using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgPixelArtManifestTests
    {
        [Test]
        public void DefaultManifestLoadsAllAuthoredTilesetsAndStoryCastSprites()
        {
            RpgPixelArtManifest manifest = RpgPixelArtManifest.LoadDefault();
            Assert.That(manifest, Is.Not.Null,
                "RpgPixelArtManifest must load from Resources/Soundwell/RpgPixelArtManifest.");

            Assert.That(manifest.SoundwellTiles, Is.Not.Null);
            Assert.That(manifest.SoundwellTiles.Length, Is.EqualTo(16));
            for (int i = 0; i < manifest.SoundwellTiles.Length; i++)
            {
                Sprite s = manifest.SoundwellTiles[i];
                Assert.That(s, Is.Not.Null, "Soundwell tile slice " + i);
                Assert.That(s.texture.isReadable, Is.True, "Soundwell tileset must be readable for chunk blitting.");
                Assert.That(Mathf.RoundToInt(s.textureRect.width), Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
                Assert.That(Mathf.RoundToInt(s.textureRect.height), Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
            }

            Assert.That(manifest.HarborTiles, Is.Not.Null);
            Assert.That(manifest.HarborTiles.Length, Is.EqualTo(16));
            for (int i = 0; i < manifest.HarborTiles.Length; i++)
            {
                Sprite s = manifest.HarborTiles[i];
                Assert.That(s, Is.Not.Null, "Harbor tile slice " + i);
                Assert.That(s.texture.isReadable, Is.True, "Harbor tileset must be readable for chunk blitting.");
                Assert.That(Mathf.RoundToInt(s.textureRect.width), Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
                Assert.That(Mathf.RoundToInt(s.textureRect.height), Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
            }

            Assert.That(manifest.FerryGuideIdle, Is.Not.Null);
            Assert.That(manifest.FerryGuideWalk1, Is.Not.Null);
            Assert.That(manifest.FerryGuideWalk2, Is.Not.Null);
            Assert.That(manifest.FerryGuideBrace, Is.Not.Null);
            Assert.That(manifest.RescueRiggerIdle, Is.Not.Null);
            foreach (Sprite beastFrame in new[]
            {
                manifest.SoundBorrowingBeastIdle,
                manifest.SoundBorrowingBeastCreep1,
                manifest.SoundBorrowingBeastCreep2,
                manifest.SoundBorrowingBeastAlert
            })
            {
                Assert.That(beastFrame, Is.Not.Null);
                Assert.That(beastFrame.pixelsPerUnit, Is.EqualTo(16f));
                Assert.That(beastFrame.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(Mathf.RoundToInt(beastFrame.textureRect.width), Is.EqualTo(16));
                Assert.That(Mathf.RoundToInt(beastFrame.textureRect.height), Is.EqualTo(24));
            }
            Assert.That(manifest.BridgeGuardIdle, Is.Not.Null);
            Assert.That(manifest.BridgeGuardThrust, Is.Not.Null);
            Assert.That(manifest.ShenLiBridgeIdle, Is.Not.Null);
            Assert.That(manifest.ShenLiBraceCable, Is.Not.Null);
            Assert.That(manifest.SeranLetterCarrier, Is.Not.Null);
            Assert.That(manifest.HarborRepairer, Is.Not.Null);
            Assert.That(manifest.OuterHarborFisher, Is.Not.Null);
            Assert.That(manifest.MiningFamilySaltVendor, Is.Not.Null);

            // P0 S00 battle actions (12 slices, 24x24)
            foreach (Sprite s in new[]
            {
                manifest.S00PlayerReady, manifest.S00PlayerDraw,
                manifest.S00PlayerSlash, manifest.S00PlayerRecover,
                manifest.S00PlayerRaise, manifest.S00PlayerHold,
                manifest.S00PlayerParry, manifest.S00PlayerHit,
                manifest.S00SapperReady, manifest.S00SapperRush,
                manifest.S00SapperCut, manifest.S00SapperRepelled
            })
            {
                Assert.That(s, Is.Not.Null);
                Assert.That(Mathf.RoundToInt(s.textureRect.width), Is.EqualTo(24));
                Assert.That(Mathf.RoundToInt(s.textureRect.height), Is.EqualTo(24));
            }

            // P0 universal combat feedback VFX (4 groups x 3 frames, 24x24)
            foreach (Sprite[] group in new[]
            {
                manifest.ParrySparkFrames,
                manifest.BladeRepelTrailFrames,
                manifest.ThreatLockBreakFrames,
                manifest.AcousticPulseFrames
            })
            {
                Assert.That(group, Is.Not.Null);
                Assert.That(group.Length, Is.EqualTo(3));
                for (int i = 0; i < group.Length; i++)
                {
                    Assert.That(group[i], Is.Not.Null);
                    Assert.That(Mathf.RoundToInt(group[i].textureRect.width), Is.EqualTo(24));
                    Assert.That(Mathf.RoundToInt(group[i].textureRect.height), Is.EqualTo(24));
                }
            }

            // P1 M02 bridge evidence props (6 slices, 32x32)
            foreach (Sprite s in new[]
            {
                manifest.M02CableFlatUncharredCut, manifest.M02BridgeKeeperOpenLedger,
                manifest.M02OverwrittenBattleReport, manifest.M02HarborOfficialSeal,
                manifest.M02IdentityDocumentUnsealed, manifest.M02IdentityDocumentSealed
            })
            {
                Assert.That(s, Is.Not.Null);
                Assert.That(s.pixelsPerUnit, Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
                Assert.That(s.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(Mathf.RoundToInt(s.textureRect.width), Is.EqualTo(32));
                Assert.That(Mathf.RoundToInt(s.textureRect.height), Is.EqualTo(32));
            }

            // P1 M03 Lu Jun camp cast (6 slices, 16x24)
            foreach (Sprite s in new[]
            {
                manifest.M03LuJunStanding, manifest.M03LuJunRollCall,
                manifest.M03VeteranNetMender, manifest.M03VeteranForwardLookout,
                manifest.M03VeteranTidalGuide, manifest.M03VeteranStandardBearer
            })
            {
                Assert.That(s, Is.Not.Null);
                Assert.That(s.pixelsPerUnit, Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
                Assert.That(s.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(Mathf.RoundToInt(s.textureRect.width), Is.EqualTo(16));
                Assert.That(Mathf.RoundToInt(s.textureRect.height), Is.EqualTo(24));
            }

            // P1 M03 Lu Jun camp objects (8 slices, 32x32)
            foreach (Sprite s in new[]
            {
                manifest.M03OldStandardRolled, manifest.M03OldStandardRaisedA,
                manifest.M03OldStandardRaisedB, manifest.M03NetDryingRackLoaded,
                manifest.M03NetDryingRackEmpty, manifest.M03TidalChartOpen,
                manifest.M03TidalChartFolded, manifest.M03CampCoals
            })
            {
                Assert.That(s, Is.Not.Null);
                Assert.That(s.pixelsPerUnit, Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
                Assert.That(s.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(Mathf.RoundToInt(s.textureRect.width), Is.EqualTo(32));
                Assert.That(Mathf.RoundToInt(s.textureRect.height), Is.EqualTo(32));
            }

            // P2 M04 fog crossing overlays (12 slices, 32x32)
            foreach (Sprite s in new[]
            {
                manifest.FogBankLowA, manifest.FogBankLowB, manifest.FogBankDense,
                manifest.FogReefHintSubmerged, manifest.FogReefHintExposed, manifest.FogReefWash,
                manifest.FogBowWakeA, manifest.FogBowWakeB,
                manifest.FogSternWakeA, manifest.FogSternWakeB,
                manifest.FogSignalLanternOff, manifest.FogSignalLanternOn
            })
            {
                Assert.That(s, Is.Not.Null);
                Assert.That(s.pixelsPerUnit, Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
                Assert.That(s.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(Mathf.RoundToInt(s.textureRect.width), Is.EqualTo(32));
                Assert.That(Mathf.RoundToInt(s.textureRect.height), Is.EqualTo(32));
            }

            // P3 M07 evacuation route props (12 slices, 16x16)
            foreach (Sprite s in new[]
            {
                manifest.M07OulinHiddenLockClosed, manifest.M07OulinHiddenLockOpen,
                manifest.M07MainPumpLeverRunning, manifest.M07MainPumpLeverCutoff,
                manifest.M07UpperMusterSign, manifest.M07UpperMusterChalk,
                manifest.M07CargoCoverCrateClosed, manifest.M07CargoCoverCrateOpen,
                manifest.M07InspectionPendingPass, manifest.M07InspectionStampedPass,
                manifest.M07BerthTagAvailable, manifest.M07BerthTagBoarded
            })
            {
                Assert.That(s, Is.Not.Null);
                Assert.That(s.pixelsPerUnit, Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
                Assert.That(s.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(Mathf.RoundToInt(s.textureRect.width), Is.EqualTo(16));
                Assert.That(Mathf.RoundToInt(s.textureRect.height), Is.EqualTo(16));
            }

            // P3 M07 evacuation structures (8 slices, 32x32)
            foreach (Sprite s in new[]
            {
                manifest.M07BackupHandPumpIdle, manifest.M07BackupHandPumpWorking,
                manifest.M07SluiceLockedShut, manifest.M07SluiceReleasedOpen,
                manifest.M07BoardingGangwayRaised, manifest.M07BoardingGangwayLowered,
                manifest.M07InspectionBarrierClosed, manifest.M07InspectionBarrierOpen
            })
            {
                Assert.That(s, Is.Not.Null);
                Assert.That(s.pixelsPerUnit, Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
                Assert.That(s.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(Mathf.RoundToInt(s.textureRect.width), Is.EqualTo(32));
                Assert.That(Mathf.RoundToInt(s.textureRect.height), Is.EqualTo(32));
            }
        }

        [Test]
        public void AllThreeLargeMapChunkRenderersUseRealPixelArtSlicesByDefault()
        {
            var root = new GameObject("Real art chunk renderer test");
            RpgLargeMapChunkRenderer soundwell = null;
            RpgLargeMapChunkRenderer harbor = null;
            RpgLargeMapChunkRenderer crossing = null;
            try
            {
                soundwell = RpgLargeMapChunkRenderer.BuildForSoundwell(
                    root.transform, SoundwellLargeMapLayout.Build());
                harbor = RpgLargeMapChunkRenderer.BuildForRedSaltHarbor(
                    root.transform, RedSaltHarborMapLayout.Build());
                crossing = RpgLargeMapChunkRenderer.BuildForWhitewaterCrossing(
                    root.transform, WhitewaterCrossingMapLayout.Build());

                Assert.That(soundwell.UsedRealArtSlices, Is.True);
                Assert.That(harbor.UsedRealArtSlices, Is.True);
                Assert.That(crossing.UsedRealArtSlices, Is.True);
            }
            finally
            {
                soundwell?.Dispose();
                harbor?.Dispose();
                crossing?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
