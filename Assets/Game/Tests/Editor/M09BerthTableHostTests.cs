using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M09BerthTableHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> data)
        {
            return key => data.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedExtraction(Dictionary<string, int> data)
        {
            Func<string, int> read = Reader(data);
            Action<string, int> write = (k, v) => data[k] = v;
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
            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(M06RescuePlan.OfficialHandover, true,
                    M06LetterDeliveryOutcome.NotCarried, true), read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRecord(
                new M07ExtractionRecord(
                    M07ExecutedRoute.OfficialHandover,
                    M07PumpShutdownState.KeptRunning,
                    M07RouteCostFlags.HarborSupplyDebt,
                    boardedFirstBatchCount: 10, lowerLevelEvacuatedCount: 0,
                    M07RusFate.Arrested, M07LedgerState.Preserved,
                    M07LiangChoice.StayOnIsland,
                    M07YidaChoice.HandToInspector, taoJiRescued: false),
                read, write), Is.True);
        }

        private static M09BerthTableHost NewHost(
            Transform parent,
            Dictionary<string, int> data,
            Func<bool> inScene = null,
            Func<bool> shenPresent = null,
            Func<M09RuiyaOutcome?> ruiya = null,
            Action<string, int> writer = null)
        {
            return new M09BerthTableHost(
                parent, inScene ?? (() => true),
                shenPresent ?? (() => true),
                () => false, () => 18, () => 23,
                ruiya ?? (() => M09RuiyaOutcome.Deferred),
                Reader(data), writer ?? ((k, v) => data[k] = v));
        }

        [Test]
        public void OldBerthTableCannotSettleAnIdenticallyReplayedVoyage()
        {
            var root = new GameObject("M09 replayed arrival identity");
            var values = new Dictionary<string, int>();
            SeedVerifiedExtraction(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M09BerthTableHost oldHost = null;
            M09BerthTableHost newHost = null;
            try
            {
                int previousArrival = read(M05LandingFacts.ArrivalSequenceKey);
                oldHost = NewHost(root.transform, values);
                Assert.That(oldHost.TryOpen(), Is.True);
                oldHost.ActiveView.HanQiaoButton.onClick.Invoke();

                // The first negotiation stays on screen while M03 starts a
                // new voyage; no old table may settle before arrival.
                Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write),
                    Is.True);
                oldHost.ActiveView.SettleButton.onClick.Invoke();
                Assert.That(read(M09BerthTableFacts.ConfirmedKey), Is.Zero);

                // New route, crew, rescued people and table numbers are identical.
                // Only the monotonic M05 arrival identity distinguishes visits.
                SeedVerifiedExtraction(values);
                Assert.That(read(M05LandingFacts.ArrivalSequenceKey),
                    Is.GreaterThan(previousArrival));
                oldHost.ActiveView.SettleButton.onClick.Invoke();
                Assert.That(read(M09BerthTableFacts.ConfirmedKey), Is.Zero);
                Assert.That(oldHost.ActiveView, Is.Not.Null);

                oldHost.Dispose();
                oldHost = null;
                newHost = NewHost(root.transform, values);
                Assert.That(newHost.TryOpen(), Is.True);
                newHost.ActiveView.HanQiaoButton.onClick.Invoke();
                newHost.ActiveView.SettleButton.onClick.Invoke();
                Assert.That(newHost.HasRecordedTable, Is.True);
                Assert.That(newHost.ActiveView, Is.Null);
            }
            finally
            {
                oldHost?.Dispose();
                newHost?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OnlyVerifiedExtractionAndUnconfirmedM09OpenTheTable()
        {
            var root = new GameObject("M09 host prerequisites");
            var values = new Dictionary<string, int>();
            M09BerthTableHost host = null;
            try
            {
                host = NewHost(root.transform, values);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(values, Is.Empty);
                SeedVerifiedExtraction(values);
                values[M09BerthTableFacts.ConfirmedKey] = 2;
                Assert.That(host.TryOpen(), Is.False);
                values[M09BerthTableFacts.ConfirmedKey] = 0;
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.TryOpen(), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TornBerthPayloadCannotReopenFreshNegotiationUntilReset()
        {
            var root = new GameObject("M09 torn berth table");
            var values = new Dictionary<string, int>();
            SeedVerifiedExtraction(values);
            M09BerthTableHost host = null;
            try
            {
                host = NewHost(root.transform, values);
                foreach (string orphanKey in new[]
                {
                    M09BerthTableFacts.PendingSignatureKey,
                    M09BerthTableFacts.TotalBerthsKey,
                    M09BerthTableFacts.PendingNamesKey,
                    M09BerthTableFacts.ExchangesKey,
                    M09BerthTableFacts.RuiyaOutcomeKey,
                    M09BerthTableFacts.QiDebtDisclosedKey,
                    M09BerthTableFacts.ShenReconciledKey,
                    M09BerthTableFacts.ShipwrightFeePaidKey
                })
                {
                    values[orphanKey] = 1;
                    Assert.That(host.TryOpen(), Is.False,
                        "Torn berth payload must not start a new manifest: " + orphanKey);
                    Assert.That(host.ActiveView, Is.Null);
                    Assert.That(M09BerthTableFacts.TryResetForNewVoyage(
                        (key, value) => values[key] = value), Is.True);
                }

                Assert.That(host.TryOpen(), Is.True,
                    "A clean new voyage can still negotiate the second-batch table.");
                Assert.That(host.ActiveView, Is.Not.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NegotiatedCapacityAndKnownOutcomePersistExactlyOnce()
        {
            var root = new GameObject("M09 host settlement");
            var values = new Dictionary<string, int>();
            SeedVerifiedExtraction(values);
            M09BerthTableHost host = null;
            M09BerthTableHost repeat = null;
            try
            {
                host = NewHost(root.transform, values,
                    ruiya: () => M09RuiyaOutcome.Rescued);
                Assert.That(host.TryOpen(), Is.True);
                host.ActiveView.QiQueButton.onClick.Invoke();
                host.ActiveView.ShipwrightButton.onClick.Invoke();
                host.ActiveView.ReconcileButton.onClick.Invoke();
                Assert.That(host.Session.CurrentShortfall, Is.Zero);
                host.ActiveView.SettleButton.onClick.Invoke();
                Assert.That(host.HasRecordedTable, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(M09BerthTableFacts.TryRead(
                    Reader(values), out M09BerthTableRecord? saved), Is.True);
                Assert.That(saved.Value.TotalBerths, Is.EqualTo(23));
                Assert.That(saved.Value.RuiyaOutcome, Is.EqualTo(M09RuiyaOutcome.Rescued));
                Assert.That(saved.Value.ShenReconciled, Is.True);
                repeat = NewHost(root.transform, values);
                Assert.That(repeat.TryOpen(), Is.False);
            }
            finally
            {
                repeat?.Dispose();
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UnknownRuiyaOrChangedLiveSceneCannotSilentlySettle()
        {
            var root = new GameObject("M09 host live guards");
            var values = new Dictionary<string, int>();
            SeedVerifiedExtraction(values);
            bool inScene = true, present = true;
            M09RuiyaOutcome? outcome = null;
            M09BerthTableHost host = null;
            try
            {
                host = NewHost(root.transform, values,
                    () => inScene, () => present, () => outcome);
                Assert.That(host.TryOpen(), Is.True);
                host.ActiveView.HanQiaoButton.onClick.Invoke();
                host.ActiveView.SettleButton.onClick.Invoke();
                Assert.That(host.HasRecordedTable, Is.False);
                outcome = M09RuiyaOutcome.Deferred;
                present = false;
                host.ActiveView.SettleButton.onClick.Invoke();
                Assert.That(host.HasRecordedTable, Is.False);
                present = true;
                inScene = false;
                host.ActiveView.SettleButton.onClick.Invoke();
                Assert.That(host.HasRecordedTable, Is.False);
                inScene = true;
                host.ActiveView.SettleButton.onClick.Invoke();
                Assert.That(host.HasRecordedTable, Is.True);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedAndAcknowledgedAfterCommitWritesRecoverWithoutDuplicates()
        {
            var root = new GameObject("M09 host interrupted save");
            var values = new Dictionary<string, int>();
            SeedVerifiedExtraction(values);
            int failBeforeCommit = 0, committed = 0;
            Action<string, int> unstable = (key, value) =>
            {
                if (key == M09BerthTableFacts.RuiyaOutcomeKey &&
                    failBeforeCommit++ == 0)
                    throw new InvalidOperationException("Write interrupted");
                values[key] = value;
                if (key == M09BerthTableFacts.ConfirmedKey &&
                    value == 1 && ++committed == 1)
                    throw new InvalidOperationException("Commit ack lost");
            };
            M09BerthTableHost host = null;
            try
            {
                host = NewHost(root.transform, values, writer: unstable);
                Assert.That(host.TryOpen(), Is.True);
                var button = host.ActiveView.SettleButton;
                Assert.Throws<InvalidOperationException>(() => button.onClick.Invoke());
                Assert.That(host.HasRecordedTable, Is.False);
                Assert.Throws<InvalidOperationException>(() => button.onClick.Invoke());
                Assert.That(host.HasRecordedTable, Is.True);
                Assert.That(committed, Is.EqualTo(1));
                button.onClick.Invoke();
                Assert.That(committed, Is.EqualTo(1));
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposedHostNeverSavesFromFormerView()
        {
            var root = new GameObject("M09 host disposed");
            var values = new Dictionary<string, int>();
            SeedVerifiedExtraction(values);
            M09BerthTableHost host = null;
            try
            {
                host = NewHost(root.transform, values);
                Assert.That(host.TryOpen(), Is.True);
                var button = host.ActiveView.SettleButton;
                host.Dispose();
                button.onClick.Invoke();
                Assert.That(host.HasRecordedTable, Is.False);
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
