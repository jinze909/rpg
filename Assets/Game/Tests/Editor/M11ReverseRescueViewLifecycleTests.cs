using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M11ReverseRescueViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static M11ReverseRescueSession CreateVerifiedSession(
            bool shenLiAccompanying = false,
            bool oulinParticipating = false)
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
            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                worldNoticeThreeObserved: true,
                M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.PublishDraft,
                M10SupplyContractStatus.SignedFullCoverage,
                leverageWeakenedByRC02: false,
                coversRC05MountainFamilies: false,
                bellTowerSeizureHeard: true), read, write), Is.True);

            Assert.That(M11ReverseRescueSession.TryCreateFromSavedFacts(
                read, shenLiAccompanying, oulinParticipating, out var session), Is.True);
            return session;
        }

        private static void AdvanceFallbackToConfirm(M11ReverseRescueView view)
        {
            view.LandGateButton.onClick.Invoke();
            view.CoverReturneesButton.onClick.Invoke();
            view.FallbackArtisansButton.onClick.Invoke();
        }

        [Test]
        public void DestroyingCoreChildControlDetachesLocaleAndBlocksClicks()
        {
            var root = new GameObject("M11 view partial teardown");
            string savedLanguage = SoundwellUiLocalization.Language;
            M11ReverseRescueView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = CreateVerifiedSession();
                int commits = 0;

                view = new M11ReverseRescueView(root.transform, session, () => commits++);
                Assert.That(view.IsDisposed, Is.False);

                // Destroy a core child control while leaving canvas and panel alive.
                UnityEngine.Object.DestroyImmediate(view.MerchantCaravanButton.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => view.LandGateButton.onClick.Invoke());
                Assert.That(session.Phase, Is.EqualTo(M11Phase.ThreeBellsAndEntry));
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
        public void EffectiveInputLockBlocksFallbackRescueDecisionsAndCommit()
        {
            AssertEffectiveInputLockBlocksRescueDecisionsAndCommit(false);
        }

        [Test]
        public void EffectiveInputLockBlocksShenLiRescueDecisionsAndCommit()
        {
            AssertEffectiveInputLockBlocksRescueDecisionsAndCommit(true);
        }

        private static void AssertEffectiveInputLockBlocksRescueDecisionsAndCommit(bool shenLiAccompanying)
        {
            var root = new GameObject("M11 rescue input lock");
            M11ReverseRescueView view = null;
            try
            {
                var session = CreateVerifiedSession(shenLiAccompanying);
                int commits = 0;
                view = new M11ReverseRescueView(root.transform, session, () => commits++);
                var group = view.Panel.gameObject.AddComponent<CanvasGroup>();

                group.interactable = false;
                Assert.That(view.LandGateButton.IsInteractable(), Is.False);
                view.LandGateButton.onClick.Invoke();
                view.MerchantCaravanButton.onClick.Invoke();
                view.ReliefPassButton.onClick.Invoke();
                Assert.That(session.ThreeBellsObserved, Is.False);
                Assert.That(session.EntryMethod.HasValue, Is.False);

                group.interactable = true;
                view.LandGateButton.onClick.Invoke();
                view.Panel.gameObject.SetActive(false);
                view.CoverReturneesButton.onClick.Invoke();
                Assert.That(session.ConscriptionObserved, Is.False);
                view.Panel.gameObject.SetActive(true);
                view.CoverReturneesButton.onClick.Invoke();
                Assert.That(session.Phase, Is.EqualTo(M11Phase.FogExtraction));

                group.interactable = false;
                if (shenLiAccompanying)
                {
                    view.HearFogCallButton.onClick.Invoke();
                    Assert.That(session.EchoCallsHeard, Is.Zero);
                    group.interactable = true;
                    for (int i = 0; i < 3; i++)
                        view.HearFogCallButton.onClick.Invoke();
                    Assert.That(session.EchoCallsHeard, Is.EqualTo(3));

                    group.interactable = false;
                    view.AnswerPresentButton.onClick.Invoke();
                    Assert.That(session.RescueProvider.HasValue, Is.False);
                    group.interactable = true;
                    view.AnswerPresentButton.onClick.Invoke();
                    Assert.That(session.RescueProvider, Is.EqualTo(M11RescueProvider.ShenLiShallowBoat));

                    view.Panel.gameObject.SetActive(false);
                    view.MutualDivisionButton.onClick.Invoke();
                    view.CallItEvenButton.onClick.Invoke();
                    Assert.That(session.DivisionPact, Is.EqualTo(M11ShenDivisionPact.NoneShenAbsent));
                    view.Panel.gameObject.SetActive(true);
                    view.MutualDivisionButton.onClick.Invoke();
                    Assert.That(session.DivisionPact, Is.EqualTo(M11ShenDivisionPact.MutualConsultAndDivision));
                }
                else
                {
                    view.FallbackArtisansButton.onClick.Invoke();
                    Assert.That(session.RescueProvider.HasValue, Is.False);
                    group.interactable = true;
                    view.FallbackArtisansButton.onClick.Invoke();
                    Assert.That(session.RescueProvider.HasValue, Is.True);
                    Assert.That(session.EchoCallsHeard, Is.Zero);
                    Assert.That(session.DivisionPact, Is.EqualTo(M11ShenDivisionPact.NoneShenAbsent));
                }

                Assert.That(session.Phase, Is.EqualTo(M11Phase.PostRescuePact));
                group.interactable = false;
                view.ConfirmRescueButton.onClick.Invoke();
                group.interactable = true;
                view.Panel.gameObject.SetActive(false);
                view.ConfirmRescueButton.onClick.Invoke();
                Assert.That(commits, Is.Zero);
                view.Panel.gameObject.SetActive(true);
                view.ConfirmRescueButton.onClick.Invoke();
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
            var root = new GameObject("M11 view aspect guard");
            M11ReverseRescueView view = null;
            try
            {
                var session = CreateVerifiedSession();
                view = new M11ReverseRescueView(root.transform, session, () => { });

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
            var root = new GameObject("M11 view reentrant commit");
            M11ReverseRescueView view = null;
            try
            {
                var session = CreateVerifiedSession();
                int commits = 0;

                view = new M11ReverseRescueView(root.transform, session, () =>
                {
                    commits++;
                    Assert.That(view.ConfirmRescueButton.interactable, Is.False);
                    view.ConfirmRescueButton.onClick.Invoke();
                });

                AdvanceFallbackToConfirm(view);
                view.ConfirmRescueButton.onClick.Invoke();
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
            var root = new GameObject("M11 view dispose-then-throw");
            string savedLanguage = SoundwellUiLocalization.Language;
            M11ReverseRescueView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = CreateVerifiedSession();

                view = new M11ReverseRescueView(root.transform, session, () =>
                {
                    view.Dispose();
                    throw new InvalidOperationException("Host disposed view before throwing");
                });

                AdvanceFallbackToConfirm(view);
                Assert.Throws<InvalidOperationException>(() =>
                    view.ConfirmRescueButton.onClick.Invoke());
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
