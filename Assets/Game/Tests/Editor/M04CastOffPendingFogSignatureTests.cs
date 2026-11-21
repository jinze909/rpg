using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04CastOffPendingFogSignatureTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedReadyDeparture(Dictionary<string, int> values)
        {
            Action<string, int> write = (k, v) => values[k] = v;
            var encounter = new M03LuJunEncounter(M02BridgeEvidence.None, true);
            encounter.Begin();
            encounter.ObserveRollCall();
            encounter.Ask(M03LuJunOpening.NeedAShip);
            encounter.ChooseVoyage(M03VoyageChoice.QiQueMerchant);
            Assert.That(M03LuJunFacts.TryRecord(encounter, write), Is.True);

            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant,
                "Confirmed hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
        }

        [Test]
        public void PendingFogSignatureBlocksCanBeginFreshFogLegAndOpeningContinuationCastOff()
        {
            var values = new Dictionary<string, int>();
            SeedReadyDeparture(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M04DepartureFacts.CanBeginFreshFogLeg(read), Is.True);
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read, out _), Is.True);

            // Simulate a fog write interrupted right after persisting PendingSignatureKey.
            values[FogListeningFacts.PendingSignatureKey] = 12345;
            Assert.That(M04DepartureFacts.CanBeginFreshFogLeg(read), Is.False);
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(
                read, out M04DepartureRecord? blocked), Is.False);
            Assert.That(blocked.HasValue, Is.False);

            Assert.That(FogListeningFacts.TryResetForNewLeg(write), Is.True);
            Assert.That(M04DepartureFacts.CanBeginFreshFogLeg(read), Is.True);
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read, out _), Is.True);
        }

        [Test]
        public void PendingFogSignatureDisablesLiveCastOffViewWithoutMarkingShipSailed()
        {
            var root = new GameObject("M04 pending fog signature cast-off guard");
            var values = new Dictionary<string, int>();
            SeedReadyDeparture(values);
            Func<string, int> read = Reader(values);
            int transitions = 0;
            int sailWrites = 0;
            Action<string, int> write = (k, v) =>
            {
                if (k == M04DepartureFacts.SailedKey && v == 1)
                    sailWrites++;
                values[k] = v;
            };
            M04CastOffView view = null;
            try
            {
                view = new M04CastOffView(root.transform, read, write, () => transitions++);
                Assert.That(view.CastOffButton.interactable, Is.True);

                values[FogListeningFacts.PendingSignatureKey] = 1;
                view.Refresh();
                Assert.That(view.CastOffButton.interactable, Is.False);
                view.CastOffButton.onClick.Invoke();
                Assert.That(read(M04DepartureFacts.SailedKey), Is.Zero);
                Assert.That(sailWrites, Is.Zero);
                Assert.That(transitions, Is.Zero);

                Assert.That(FogListeningFacts.TryResetForNewLeg(write), Is.True);
                view.Refresh();
                Assert.That(view.CastOffButton.interactable, Is.True);
                view.CastOffButton.onClick.Invoke();
                Assert.That(sailWrites, Is.EqualTo(1));
                Assert.That(transitions, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
