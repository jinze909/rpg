using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M07ExtractionNightHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM06(
            Dictionary<string, int> values,
            M06RescuePlan plan)
        {
            Action<string, int> write = (key, value) => values[key] = value;
            Func<string, int> read = Reader(values);

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
            foreach (M06FamilyLetterRecipient recipient in new[]
            {
                M06FamilyLetterRecipient.TaoJi,
                M06FamilyLetterRecipient.LedgerMiner,
                M06FamilyLetterRecipient.PendingMiner
            })
                Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                    recipient, read, write), Is.True);
            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(
                    plan, true, M06LetterDeliveryOutcome.DeliveredWithPending, true),
                read, write), Is.True);
        }

        private static void AdvanceToDockRollCall(M07ExtractionNightView view)
        {
            view.ExecutePlanButton.onClick.Invoke();
            view.AdvanceButton.onClick.Invoke();
            view.ArrestRusButton.onClick.Invoke();
            view.AdvanceButton.onClick.Invoke();
        }

        [Test]
        public void ExternalViewDisposalAllowsCleanReopenAndRollCallSettlement()
        {
            var root = new GameObject("M07 host external dispose");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.MinerShutdown);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M07ExtractionNightHost host = null;
            try
            {
                host = new M07ExtractionNightHost(
                    root.transform, () => true, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var firstView = host.ActiveView;
                Assert.That(firstView, Is.Not.Null);

                firstView.Dispose();
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);

                Assert.That(host.TryOpen(), Is.True);
                var reopenedView = host.ActiveView;
                Assert.That(reopenedView, Is.Not.Null);
                Assert.That(reopenedView, Is.Not.SameAs(firstView));

                AdvanceToDockRollCall(reopenedView);
                reopenedView.LiangStayButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedViewCanvasTreatsViewAsDisposedAndAllowsCleanReopen()
        {
            var root = new GameObject("M07 host canvas destroy");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.OfficialHandover);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M07ExtractionNightHost host = null;
            try
            {
                host = new M07ExtractionNightHost(
                    root.transform, () => true, () => false, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var firstView = host.ActiveView;
                Assert.That(firstView, Is.Not.Null);

                UnityEngine.Object.DestroyImmediate(firstView.Panel.parent.gameObject);
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);

                Assert.That(host.TryOpen(), Is.True);
                var reopenedView = host.ActiveView;
                Assert.That(reopenedView, Is.Not.Null);

                AdvanceToDockRollCall(reopenedView);
                reopenedView.LiangPersuadeButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NestedOpenDuringReadinessCannotClaimOuterExtraction()
        {
            AssertNestedOpenCannotClaimOuterExtraction(true);
        }

        [Test]
        public void NestedOpenDuringFactReadCannotClaimOuterExtraction()
        {
            AssertNestedOpenCannotClaimOuterExtraction(false);
        }

        private static void AssertNestedOpenCannotClaimOuterExtraction(bool fromReadiness)
        {
            var root = new GameObject("M07 reentrant extraction opening");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.MinerShutdown);
            var read = Reader(values);
            M07ExtractionNightHost host = null;
            bool armed = true;
            int nestedAttempts = 0;
            int writes = 0;
            void AttemptNestedOpen()
            {
                if (!armed) return;
                armed = false;
                nestedAttempts++;
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Null);
            }

            try
            {
                host = new M07ExtractionNightHost(root.transform, () =>
                {
                    if (fromReadiness) AttemptNestedOpen();
                    return true;
                }, () => true, key =>
                {
                    if (!fromReadiness) AttemptNestedOpen();
                    return read(key);
                }, (key, value) =>
                {
                    writes++;
                    values[key] = value;
                });
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(nestedAttempts, Is.EqualTo(1));
                Assert.That(writes, Is.Zero);
                var view = host.ActiveView;
                Assert.That(view, Is.Not.Null);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.SameAs(view));
                AdvanceToDockRollCall(view);
                view.LiangStayButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(read(M07ExtractionFacts.ConfirmedKey), Is.EqualTo(1));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ThrowingReadinessCallbackReleasesExtractionOpeningReservation()
        {
            var root = new GameObject("M07 interrupted extraction opening");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.MinerShutdown);
            M07ExtractionNightHost host = null;
            bool throwOnce = true;
            try
            {
                host = new M07ExtractionNightHost(root.transform, () =>
                {
                    if (throwOnce)
                    {
                        throwOnce = false;
                        throw new InvalidOperationException("Interrupted readiness callback");
                    }
                    return true;
                }, () => true, Reader(values), (key, value) => values[key] = value);
                Assert.Throws<InvalidOperationException>(() => host.TryOpen());
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.IsCompleted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedParentFailsClosedOnTryOpenWithoutThrowing()
        {
            var root = new GameObject("M07 host parent destroy");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.CargoCover);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M07ExtractionNightHost host = null;
            try
            {
                host = new M07ExtractionNightHost(
                    root.transform, () => true, () => true, read, write);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.IsCompleted, Is.False);
            }
            finally
            {
                host?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
