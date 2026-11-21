using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M09BerthTableViewTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM07(Dictionary<string, int> values)
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
            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(
                    M06RescuePlan.OfficialHandover,
                    true,
                    M06LetterDeliveryOutcome.NotCarried,
                    true),
                read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRecord(
                new M07ExtractionRecord(
                    M07ExecutedRoute.OfficialHandover,
                    M07PumpShutdownState.KeptRunning,
                    M07RouteCostFlags.HarborSupplyDebt,
                    boardedFirstBatchCount: 300,
                    lowerLevelEvacuatedCount: 0,
                    M07RusFate.Arrested,
                    M07LedgerState.Preserved,
                    M07LiangChoice.StayOnIsland,
                    M07YidaChoice.HandToInspector,
                    taoJiRescued: true),
                read, write), Is.True);
        }

        [Test]
        public void NegotiatingExchangesUpdatesShortfallDisclosesQiDebtAndSettlesOnce()
        {
            var root = new GameObject("M09 view negotiate");
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string previousLanguage = SoundwellUiLocalization.Language;
            M09BerthTableView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                var session = new M09BerthTableSession(
                    baseBerths: 18, pendingNames: 23,
                    shenLiPresent: true, qiDebtPreviouslyKnown: false);
                int saves = 0;
                view = new M09BerthTableView(root.transform, session, s =>
                {
                    saves++;
                    Assert.That(s.TrySettleBerthTable(M09RuiyaOutcome.Rescued, read, write), Is.True);
                });

                Assert.That(view.Summary.text, Does.Contain("尚缺铺位：5"));
                view.QiQueButton.onClick.Invoke();
                Assert.That(view.Summary.text, Does.Contain("尚缺铺位：2"));
                Assert.That(view.Summary.text, Does.Contain("祁雀已摊开船贷"));
                Assert.That(view.QiQueButton.interactable, Is.False);

                view.ShipwrightButton.onClick.Invoke();
                Assert.That(view.Summary.text, Does.Contain("尚缺铺位：0"));
                view.ReconcileButton.onClick.Invoke();
                Assert.That(view.Summary.text, Does.Contain("已与沈砾谈妥各自打算"));

                view.SettleButton.onClick.Invoke();
                view.SettleButton.onClick.Invoke();
                Assert.That(saves, Is.EqualTo(1));
                Assert.That(session.IsSettled, Is.True);
                Assert.That(view.SettleButton.gameObject.activeSelf, Is.False);

                Assert.That(M09BerthTableFacts.TryRead(read, out M09BerthTableRecord? saved), Is.True);
                Assert.That(saved.Value.TotalBerths, Is.EqualTo(23));
                Assert.That(saved.Value.ShenReconciled, Is.True);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AbsentShenLiHidesShipwrightAndReconcileButtonsAndKeepsUnassignedPending()
        {
            var root = new GameObject("M09 view absent shen");
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string previousLanguage = SoundwellUiLocalization.Language;
            M09BerthTableView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                var session = new M09BerthTableSession(
                    baseBerths: 18, pendingNames: 23,
                    shenLiPresent: false, qiDebtPreviouslyKnown: true);
                view = new M09BerthTableView(root.transform, session, s =>
                {
                    Assert.That(s.TrySettleBerthTable(M09RuiyaOutcome.Deferred, read, write), Is.True);
                });

                Assert.That(view.ShipwrightButton.gameObject.activeSelf, Is.False);
                Assert.That(view.ReconcileButton.gameObject.activeSelf, Is.False);
                Assert.That(view.Summary.text, Does.Contain("沈砾未同行"));

                view.HanQiaoButton.onClick.Invoke();
                Assert.That(session.CurrentShortfall, Is.EqualTo(2));
                view.SettleButton.onClick.Invoke();
                Assert.That(M09BerthTableFacts.TryRead(read, out M09BerthTableRecord? saved), Is.True);
                Assert.That(saved.Value.RemainingUnassigned, Is.EqualTo(2));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AbsentShenLiCanClaimOnlyWorldVerifiedPaidShipwrightBerths()
        {
            var root = new GameObject("M09 paid shipyard alternate");
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            bool feeVerified = false;
            string language = SoundwellUiLocalization.Language;
            M09BerthTableView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                var session = new M09BerthTableSession(shenLiPresent: false);
                view = new M09BerthTableView(root.transform, session,
                    s => Assert.That(s.TrySettleBerthTable(
                        M09RuiyaOutcome.Deferred, read, write), Is.True),
                    shipwrightFeePaidInWorld: () => feeVerified);
                Assert.That(view.ShipwrightButton.gameObject.activeSelf, Is.False);
                view.ShipwrightButton.onClick.Invoke();
                Assert.That(session.AddedBerths, Is.Zero);

                feeVerified = true; // A separate scene verified actual payment.
                view.Refresh();
                Assert.That(view.ShipwrightButton.gameObject.activeSelf, Is.True);
                Assert.That(view.ShipwrightButton.interactable, Is.True);
                Assert.That(view.ShipwrightButton.GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Does.Contain("已付修船费"));
                feeVerified = false; // The world proof was revoked before the click.
                view.ShipwrightButton.onClick.Invoke();
                Assert.That(session.AddedBerths, Is.Zero);

                feeVerified = true;
                view.ShipwrightButton.onClick.Invoke();
                Assert.That(session.TotalBerths, Is.EqualTo(20));
                Assert.That(session.ShipwrightFeePaid, Is.True);
                Assert.That(session.ShenLiTwoDayApprenticeLeave, Is.False);
                Assert.That(view.ReconcileButton.gameObject.activeSelf, Is.False);
                view.SettleButton.onClick.Invoke();
                Assert.That(M09BerthTableFacts.TryRead(
                    read, out M09BerthTableRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.ShipwrightFeePaid, Is.True);
                Assert.That(saved.Value.ShenReconciled, Is.False);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(language);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedSettleSaveRetainsSettleButtonForCleanRetry()
        {
            var root = new GameObject("M09 view retry");
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M09BerthTableView view = null;
            try
            {
                var session = new M09BerthTableSession();
                int attempts = 0;
                view = new M09BerthTableView(root.transform, session, s =>
                {
                    attempts++;
                    if (attempts == 1)
                        throw new InvalidOperationException("Interrupted M09 settle write");
                    Assert.That(s.TrySettleBerthTable(M09RuiyaOutcome.Deferred, read, write), Is.True);
                });

                Assert.Throws<InvalidOperationException>(() => view.SettleButton.onClick.Invoke());
                Assert.That(session.IsSettled, Is.False);
                Assert.That(view.SettleButton.interactable, Is.True);

                view.SettleButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(session.IsSettled, Is.True);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocaleSwitchAspectScalingAndDisposalGuardsWorkCleanly()
        {
            var root = new GameObject("M09 view locale");
            string previousLanguage = SoundwellUiLocalization.Language;
            M09BerthTableView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = new M09BerthTableSession();
                view = new M09BerthTableView(root.transform, session, _ => { });
                Assert.That(view.Heading.text, Does.Contain("CABIN BERTH TABLE"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Heading.text, Does.Contain("船舱床位表"));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                var btn = view.HanQiaoButton;
                view.Dispose();
                btn.onClick.Invoke();
                Assert.That(session.AddedBerths, Is.Zero);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
