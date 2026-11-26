using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M10AssemblyViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static M10AssemblySession CreateVerifiedSession()
        {
            var values = new Dictionary<string, int>();
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
            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(
                    M06RescuePlan.OfficialHandover, true,
                    M06LetterDeliveryOutcome.NotCarried, true), read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRecord(new M07ExtractionRecord(
                M07ExecutedRoute.OfficialHandover,
                M07PumpShutdownState.KeptRunning,
                M07RouteCostFlags.HarborSupplyDebt,
                boardedFirstBatchCount: 10,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Arrested, M07LedgerState.Preserved,
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.HandToInspector,
                taoJiRescued: false), read, write), Is.True);
            Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                totalBerths: 20,
                pendingNames: 23,
                M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued,
                qiDebtDisclosedHere: false,
                shenReconciled: false), read, write), Is.True);

            Assert.That(M10AssemblySession.TryCreateFromSavedFacts(read, out var session), Is.True);
            return session;
        }

        private static void AdvanceToConfirm(M10AssemblyView view)
        {
            view.ObserveNoticeButton.onClick.Invoke();
            view.ReconcileRecordsButton.onClick.Invoke();
            view.PresentM02CopyButton.onClick.Invoke();
            view.PublishDraftButton.onClick.Invoke();
            view.SignFullCoverageButton.onClick.Invoke();
            view.HearBellTowerReportButton.onClick.Invoke();
        }

        [Test]
        public void ChineseAssemblyUsesCanonicalJingwanAndYiLeiNames()
        {
            var root = new GameObject("M10 canonical names");
            string savedLanguage = SoundwellUiLocalization.Language;
            M10AssemblyView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                var session = CreateVerifiedSession();
                view = new M10AssemblyView(root.transform, session,
                    () => true, () => true, () => false, () => false, () => { });

                Assert.That(view.Body.text, Does.Contain("镜湾"));
                Assert.That(view.ObserveNoticeButton.GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Does.Contain("镜湾"));
                view.ObserveNoticeButton.onClick.Invoke();
                Assert.That(view.Body.text, Does.Contain("伊蕾"));
                Assert.That(view.ReconcileRecordsButton.GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Does.Contain("伊蕾"));
                view.ReconcileRecordsButton.onClick.Invoke();
                view.PresentM02CopyButton.onClick.Invoke();
                view.PublishDraftButton.onClick.Invoke();
                view.SignFullCoverageButton.onClick.Invoke();
                view.HearBellTowerReportButton.onClick.Invoke();
                Assert.That(view.Body.text, Does.Contain("镜湾"));
                Assert.That(view.Body.text, Does.Not.Contain("靖湾"));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(view.Body.text, Does.Contain("Jingwan"));
                Assert.That(session.Phase, Is.EqualTo(M10AssemblyPhase.BellTowerTurn));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyingCoreChildControlDetachesLocaleAndBlocksClicks()
        {
            var root = new GameObject("M10 view partial teardown");
            string savedLanguage = SoundwellUiLocalization.Language;
            M10AssemblyView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = CreateVerifiedSession();
                int commits = 0;

                view = new M10AssemblyView(
                    root.transform, session, () => true, () => true, () => false, () => false,
                    () => commits++);
                Assert.That(view.IsDisposed, Is.False);

                // Destroy a core child control while leaving canvas and panel alive.
                UnityEngine.Object.DestroyImmediate(view.TradeForConcessionsButton.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => view.ObserveNoticeButton.onClick.Invoke());
                Assert.That(session.Phase, Is.EqualTo(M10AssemblyPhase.NoticeAndArrival));
                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
                Assert.DoesNotThrow(() => view.Dispose());
                Assert.That(commits, Is.Zero);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EffectiveInputLockBlocksAssemblyChoicesAndCommitUntilRestored()
        {
            var root = new GameObject("M10 assembly input lock");
            M10AssemblyView view = null;
            try
            {
                var session = CreateVerifiedSession();
                int reconciliations = 0, commits = 0;
                view = new M10AssemblyView(
                    root.transform, session,
                    () => { reconciliations++; return true; }, () => true,
                    () => false, () => false, () => commits++);
                var group = view.Panel.gameObject.AddComponent<CanvasGroup>();

                group.interactable = false;
                Assert.That(view.ObserveNoticeButton.IsInteractable(), Is.False);
                view.ObserveNoticeButton.onClick.Invoke();
                Assert.That(session.WorldNoticeThreeObserved, Is.False);
                Assert.That(session.Phase, Is.EqualTo(M10AssemblyPhase.NoticeAndArrival));

                group.interactable = true;
                view.ObserveNoticeButton.onClick.Invoke();
                group.interactable = false;
                view.ReconcileRecordsButton.onClick.Invoke();
                Assert.That(reconciliations, Is.Zero);
                Assert.That(session.RosterAccuracy.HasValue, Is.False);
                group.interactable = true;
                view.ReconcileRecordsButton.onClick.Invoke();
                Assert.That(reconciliations, Is.EqualTo(1));
                Assert.That(session.Phase, Is.EqualTo(M10AssemblyPhase.HanQiaoConfrontation));

                group.interactable = false;
                view.PresentM02CopyButton.onClick.Invoke();
                view.RetrieveArchiveCopyButton.onClick.Invoke();
                Assert.That(session.BattleReportSource.HasValue, Is.False);
                group.interactable = true;
                view.PresentM02CopyButton.onClick.Invoke();
                Assert.That(session.BattleReportSource, Is.EqualTo(M10BattleReportSource.CarriedFromM02));

                view.Panel.gameObject.SetActive(false);
                view.PublishDraftButton.onClick.Invoke();
                view.TradeForConcessionsButton.onClick.Invoke();
                view.KeepConfidentialButton.onClick.Invoke();
                Assert.That(session.HanLedgerUse.HasValue, Is.False);
                view.Panel.gameObject.SetActive(true);
                view.PublishDraftButton.onClick.Invoke();
                Assert.That(session.Phase, Is.EqualTo(M10AssemblyPhase.SupplyContractTable));

                group.interactable = false;
                view.SignFullCoverageButton.onClick.Invoke();
                view.RecordPartialGapButton.onClick.Invoke();
                Assert.That(session.SupplyContract.HasValue, Is.False);
                group.interactable = true;
                view.RecordPartialGapButton.onClick.Invoke();
                Assert.That(session.SupplyContract, Is.EqualTo(M10SupplyContractStatus.PartialGapToM13));

                group.interactable = false;
                view.HearBellTowerReportButton.onClick.Invoke();
                Assert.That(session.BellTowerSeizureHeard, Is.False);
                group.interactable = true;
                view.HearBellTowerReportButton.onClick.Invoke();
                Assert.That(session.BellTowerSeizureHeard, Is.True);
                group.interactable = false;
                view.ConfirmAssemblyButton.onClick.Invoke();
                group.interactable = true;
                view.Panel.gameObject.SetActive(false);
                view.ConfirmAssemblyButton.onClick.Invoke();
                Assert.That(commits, Is.Zero);
                view.Panel.gameObject.SetActive(true);
                view.ConfirmAssemblyButton.onClick.Invoke();
                Assert.That(commits, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidViewportAspectsAreIgnoredWithoutThrowing()
        {
            var root = new GameObject("M10 view aspect guard");
            M10AssemblyView view = null;
            try
            {
                var session = CreateVerifiedSession();
                view = new M10AssemblyView(
                    root.transform, session, () => true, () => true, () => false, () => false,
                    () => { });

                view.LayoutForViewportAspect(16f / 9f);
                Vector3 expectedScale = view.Panel.localScale;

                foreach (float badAspect in new[]
                {
                    0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity
                })
                {
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(badAspect));
                    Assert.That(view.Panel.localScale, Is.EqualTo(expectedScale));
                }

                Assert.That(view.IsDisposed, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReentrantCommitClickIsBlockedWhileHostCallbackIsInFlight()
        {
            var root = new GameObject("M10 view reentrant commit");
            M10AssemblyView view = null;
            try
            {
                var session = CreateVerifiedSession();
                int commits = 0;

                view = new M10AssemblyView(
                    root.transform, session, () => true, () => true, () => false, () => false,
                    () =>
                    {
                        commits++;
                        Assert.That(view.ConfirmAssemblyButton.interactable, Is.False);
                        view.ConfirmAssemblyButton.onClick.Invoke();
                    });

                AdvanceToConfirm(view);
                view.ConfirmAssemblyButton.onClick.Invoke();
                Assert.That(commits, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ThrowingOnCommitAfterDisposingViewDoesNotResurrectView()
        {
            var root = new GameObject("M10 view dispose-then-throw");
            string savedLanguage = SoundwellUiLocalization.Language;
            M10AssemblyView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = CreateVerifiedSession();

                view = new M10AssemblyView(
                    root.transform, session, () => true, () => true, () => false, () => false,
                    () =>
                    {
                        view.Dispose();
                        throw new InvalidOperationException("Host disposed view before throwing");
                    });

                AdvanceToConfirm(view);
                Assert.Throws<InvalidOperationException>(() =>
                    view.ConfirmAssemblyButton.onClick.Invoke());
                Assert.That(view.IsDisposed, Is.True);
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
