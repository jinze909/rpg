using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M14FinalPassageViewHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM13Departure(
            Dictionary<string, int> values,
            M13ApproachRoute route,
            bool safetyPrepComplete,
            bool qiWarning = false,
            bool highRiskConsent = false,
            bool threeBellSignal = false)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            Assert.That(M13DepartureRiskFacts.TryRecordDeparture(new M13DepartureRecord(
                route, safetyPrepComplete, qiWarning, highRiskConsent, threeBellSignal),
                read, write), Is.True);
        }

        [Test]
        public void HostCannotOpenWithoutM13DeparturePresenceOrCleanConfirmedKey()
        {
            var root = new GameObject("M14 host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool atStrait = false;
            M14FinalPassageHost host = null;
            try
            {
                host = new M14FinalPassageHost(
                    root.transform, () => atStrait, () => true, () => true,
                    () => true, () => true, () => false,
                    () => new M14CivilianCounts(12, 3, 1), read, write);
                Assert.That(host.TryOpen(), Is.False);

                SeedM13Departure(values, M13ApproachRoute.RouteASupervisedCivilian, true);
                Assert.That(host.TryOpen(), Is.False);

                atStrait = true;
                values[M14FinalPassageFacts.ConfirmedKey] = 2;
                Assert.That(host.TryOpen(), Is.False);

                values[M14FinalPassageFacts.ConfirmedKey] = 0;
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.TryOpen(), Is.False);
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
        public void RouteAFlowVerifiesCredentialsCountsSingleLuDispositionAndSafeOnlyAyuan()
        {
            var root = new GameObject("M14 host route A");
            var values = new Dictionary<string, int>();
            SeedM13Departure(values, M13ApproachRoute.RouteASupervisedCivilian, true);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool hasCredentials = false;
            M14FinalPassageHost host = null;
            M14FinalPassageHost duplicate = null;
            try
            {
                host = new M14FinalPassageHost(
                    root.transform, () => true, () => hasCredentials, () => true,
                    () => false, () => false, () => false,
                    () => new M14CivilianCounts(16, 4, 2), read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;

                view.DetainLuJunButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(M14Phase.RouteExecution));

                hasCredentials = true;
                view.DetainLuJunButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(M14Phase.CivilianExitVerification));

                view.VerifyCivilianExitButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(M14Phase.AyuanPositionResolution));
                Assert.That(view.SettleAyuanInjuredButton.gameObject.activeSelf, Is.False);
                Assert.That(view.SettleAyuanLostButton.gameObject.activeSelf, Is.False);

                view.SettleAyuanSafeButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(M14FinalPassageFacts.TryRead(read, out M14FinalPassageRecord? saved), Is.True);
                Assert.That(saved.Value.LuJunDisposition, Is.EqualTo(M14LuJunDisposition.Detained));
                Assert.That(saved.Value.TotalAccountedCivilians, Is.EqualTo(22));
                Assert.That(saved.Value.AyuanOutcome, Is.EqualTo(M14AyuanOutcome.Safe));

                duplicate = new M14FinalPassageHost(
                    root.transform, () => true, () => true, () => true,
                    () => false, () => false, () => false,
                    () => new M14CivilianCounts(16, 4, 2), read, write);
                Assert.That(duplicate.TryOpen(), Is.False);
            }
            finally
            {
                duplicate?.Dispose();
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RouteBHighRiskFlowAndInterruptedSaveRecoverWithoutDuplicateWrites()
        {
            var root = new GameObject("M14 host route B retry");
            var values = new Dictionary<string, int>();
            SeedM13Departure(values, M13ApproachRoute.RouteBBellTowerAssault,
                safetyPrepComplete: false, qiWarning: true, highRiskConsent: true, threeBellSignal: true);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == M14FinalPassageFacts.PassageOutcomeKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == M14FinalPassageFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };
            M14FinalPassageHost host = null;
            try
            {
                host = new M14FinalPassageHost(
                    root.transform, () => true, () => false, () => false,
                    () => true, () => false, () => false,
                    () => new M14CivilianCounts(14, 2, 0), read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;

                view.PursueWarehousesButton.onClick.Invoke();
                view.VerifyCivilianExitButton.onClick.Invoke();
                Assert.That(view.SettleAyuanInjuredButton.gameObject.activeSelf, Is.True);
                Assert.That(view.SettleAyuanLostButton.gameObject.activeSelf, Is.True);

                Assert.Throws<InvalidOperationException>(() =>
                    view.SettleAyuanInjuredButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.False);

                Assert.Throws<InvalidOperationException>(() =>
                    view.SettleAyuanInjuredButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(confirmCommits, Is.EqualTo(1));

                view.SettleAyuanInjuredButton.onClick.Invoke();
                Assert.That(confirmCommits, Is.EqualTo(1));
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(M14FinalPassageFacts.TryRead(read, out M14FinalPassageRecord? saved), Is.True);
                Assert.That(saved.Value.BorderForceAction, Is.EqualTo(M14BorderForceAction.PursueAndSeizeWarehouses));
                Assert.That(saved.Value.AyuanOutcome, Is.EqualTo(M14AyuanOutcome.SeverelyInjuredRetired));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RouteCLocaleAspectPresenceWithdrawalAndDisposalGuardsWorkCleanly()
        {
            var root = new GameObject("M14 host route C guards");
            var values = new Dictionary<string, int>();
            SeedM13Departure(values, M13ApproachRoute.RouteCOldCanalNightRun, true);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string prevLang = SoundwellUiLocalization.Language;
            bool atStrait = true;
            M14FinalPassageHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                host = new M14FinalPassageHost(
                    root.transform, () => atStrait, () => false, () => false,
                    () => false, () => true, () => true,
                    () => new M14CivilianCounts(20, 0, 1), read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.That(view.Heading.text, Does.Contain("ROUTE C OLD CANAL NIGHT RUN"));
                Assert.That(view.Body.text, Does.Contain("RC-04"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                view.CompleteFogRelayButton.onClick.Invoke();
                view.VerifyCivilianExitButton.onClick.Invoke();
                var safeBtn = view.SettleAyuanSafeButton;

                atStrait = false;
                safeBtn.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.False);

                atStrait = true;
                host.Dispose();
                safeBtn.onClick.Invoke();
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.IsCompleted, Is.False);
            }
            finally
            {
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(prevLang);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
