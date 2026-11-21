using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M06MineCampWayfindingMinimapTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int v) ? v : 0;
        }

        private static void SeedVerifiedArrival(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var manifest = new M04DepartureManifest(M03VoyageChoice.QiQueMerchant,
                "Verified civilian hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead,
                    FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 10, true, true), read, write), Is.True);
        }

        [Test]
        public void MineCampMapTracksActualPlayerAndOnlyObservedReconBeacons()
        {
            var root = new GameObject("M06 mine camp minimap");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M06MineCampExplorationHost host = null;
            try
            {
                host = new M06MineCampExplorationHost(root.transform, read, write);
                Assert.That(host.MinimapHud.BeaconCount, Is.EqualTo(5),
                    "Four playable reconnaissance leads and the actual reunion, not the unimplemented Oulin clue.");
                Assert.That(host.MinimapHud.MinimapTexture.filterMode,
                    Is.EqualTo(FilterMode.Point));
                Assert.That(host.MinimapHud.Panel.gameObject.activeSelf, Is.False,
                    "Objectives cannot appear before verified landing.");
                Assert.That(root.GetComponentInChildren<GraphicRaycaster>(true),
                    Is.Null, "Visual-only minimap must not intercept input.");

                SeedVerifiedArrival(values);
                host.RefreshHud();
                Assert.That(host.MinimapHud.Panel.gameObject.activeSelf, Is.True);
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(5));

                Vector2 start = host.MinimapHud.PlayerMarkerRect.anchoredPosition;
                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(42, 12)), Is.True);
                Assert.That(host.MinimapHud.PlayerMarkerRect.anchoredPosition.x,
                    Is.GreaterThan(start.x));

                Assert.That(M06MineCampReconFacts.TryRecord(
                    M06MineCampLead.EscortSchedule, read, write), Is.True);
                host.RefreshHud();
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(4));
                Assert.That(host.MinimapHud.BeaconCount, Is.EqualTo(5));

                // A replay invalidates last voyage's landing and all its
                // beacons, without inventing new observed facts.
                Assert.That(M05LandingFacts.TryResetForNewVisit(write), Is.True);
                host.RefreshHud();
                Assert.That(host.MinimapHud.Panel.gameObject.activeSelf, Is.False);
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(5));

                host.Dispose();
                Assert.That(host.MinimapHud.IsDisposed, Is.True);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MineCampMinimapFactoryUsesTheAuthoredLayoutAndRejectsNull()
        {
            var root = new GameObject("M06 minimap factory");
            RpgLargeMapMinimapAndBeaconHud hud = null;
            try
            {
                var layout = M06MineCampMapLayout.Build();
                Assert.Throws<ArgumentNullException>(() =>
                    RpgLargeMapMinimapAndBeaconHud.BuildForMineCamp(null, layout));
                Assert.Throws<ArgumentNullException>(() =>
                    RpgLargeMapMinimapAndBeaconHud.BuildForMineCamp(root.transform, null));
                hud = RpgLargeMapMinimapAndBeaconHud.BuildForMineCamp(root.transform, layout);
                Assert.That(hud.BeaconCount, Is.EqualTo(5));
                Assert.That(hud.MinimapTexture.width, Is.EqualTo(96));
                Assert.That(hud.MinimapTexture.height, Is.EqualTo(64));
                Assert.That(hud.MinimapTexture.filterMode, Is.EqualTo(FilterMode.Point));
            }
            finally
            {
                hud?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
