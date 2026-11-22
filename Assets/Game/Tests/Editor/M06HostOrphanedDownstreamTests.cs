using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M06HostOrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM05Arrival(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant,
                "Confirmed hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 10, true, true), read, write), Is.True);
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, read, write), Is.True);
        }

        [Test]
        public void PumpReunionTriggerPromptAndMinimapBeaconAreHiddenWhenOrphanedDownstreamFactsExist()
        {
            var root = new GameObject("M06 host orphaned downstream gate");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M06MineCampExplorationHost host = null;

            string[] orphanKeys =
            {
                M07ExtractionFacts.ExecutedRouteKey,
                M08FirstWorldNoticeFacts.ConfirmedKey,
                M09BerthTableFacts.ConfirmedKey,
                RC05ThirdSquadFacts.ConfirmedKey,
                M10AssemblyFacts.ConfirmedKey,
                M16EpilogueFacts.ConfirmedKey
            };

            try
            {
                host = new M06MineCampExplorationHost(root.transform, read, write);
                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(54, 36)), Is.True);

                foreach (string orphanKey in orphanKeys)
                {
                    values[orphanKey] = 1;
                    host.RefreshHud();

                    Assert.That(host.FindNearbyInteractableTriggerId(), Is.Null, orphanKey);
                    Assert.That(host.PromptBanner.text, Is.Empty, orphanKey);
                    Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(4), orphanKey);
                    Assert.That(host.TryInteractAtCurrentPosition(), Is.False, orphanKey);
                    Assert.That(host.ActiveReunionView, Is.Null, orphanKey);

                    values[orphanKey] = 0;
                }

                host.RefreshHud();
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(5));
                Assert.That(host.FindNearbyInteractableTriggerId(), Is.EqualTo("m06.pump-reunion"));
                Assert.That(host.PromptBanner.text, Is.Not.Empty);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(host.ActiveReunionView, Is.Not.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TornRescuePlanFactsAlsoHidePumpReunionMinimapBeacon()
        {
            var root = new GameObject("M06 host torn rescue plan beacon");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M06MineCampExplorationHost host = null;

            try
            {
                values[M06RescuePlanFacts.PendingSignatureKey] = 1;
                host = new M06MineCampExplorationHost(root.transform, read, write);
                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(54, 36)), Is.True);

                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(4));
                Assert.That(host.FindNearbyInteractableTriggerId(), Is.Null);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False);

                Assert.That(M06RescuePlanFacts.TryResetForNewVoyage(write), Is.True);
                host.RefreshHud();
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(5));
                Assert.That(host.FindNearbyInteractableTriggerId(), Is.EqualTo("m06.pump-reunion"));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
