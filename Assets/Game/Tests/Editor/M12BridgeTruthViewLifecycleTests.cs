using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M12BridgeTruthViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static M12BridgeTruthSession CreateVerifiedSession()
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
                M10HanLedgerUse.KeepConfidential,
                M10SupplyContractStatus.SignedFullCoverage,
                leverageWeakenedByRC02: false,
                coversRC05MountainFamilies: false,
                bellTowerSeizureHeard: true), read, write), Is.True);
            Assert.That(M11ReverseRescueFacts.TryRecord(new M11ReverseRescueRecord(
                threeBellsObserved: true,
                M11PassEntryMethod.LandGateNegotiation,
                conscriptionObserved: true,
                M11ThirdSquadConscriptionStance.NotInPass,
                M11RescueProvider.ShenLiShallowBoat,
                echoCallsHeard: 3,
                oulinJoinedExtraction: true,
                M11ShenDivisionPact.MutualConsultAndDivision), read, write), Is.True);

            Assert.That(M12BridgeTruthSession.TryCreateFromSavedFacts(
                read, false, false, false, false, out var session), Is.True);
            return session;
        }

        private static void AdvanceToDisclosure(M12BridgeTruthView view)
        {
            view.SomeWantNoMoreWarButton.onClick.Invoke();
            view.HearJiangShenButton.onClick.Invoke();
            view.HearHanAndYiLeiButton.onClick.Invoke();
        }

        [Test]
        public void DestroyingCoreChildControlDetachesLocaleAndBlocksClicks()
        {
            var root = new GameObject("M12 view partial teardown");
            string savedLanguage = SoundwellUiLocalization.Language;
            M12BridgeTruthView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = CreateVerifiedSession();
                int disclosures = 0;

                view = new M12BridgeTruthView(
                    root.transform, session, () => false, _ => disclosures++);
                Assert.That(view.IsDisposed, Is.False);

                // Destroy a core child control while leaving canvas and panel alive.
                UnityEngine.Object.DestroyImmediate(view.LinSuEchoButton.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => view.SomeWantNoMoreWarButton.onClick.Invoke());
                Assert.That(session.Phase, Is.EqualTo(M12Phase.BellTowerConfrontation));
                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
                Assert.DoesNotThrow(() => view.Dispose());
                Assert.That(disclosures, Is.Zero);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidViewportAspectsAreIgnoredWithoutThrowing()
        {
            var root = new GameObject("M12 view aspect guard");
            M12BridgeTruthView view = null;
            try
            {
                var session = CreateVerifiedSession();
                view = new M12BridgeTruthView(root.transform, session, () => false, _ => { });

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
        public void ReentrantDisclosureClickIsBlockedWhileHostCallbackIsInFlight()
        {
            var root = new GameObject("M12 view reentrant disclosure");
            M12BridgeTruthView view = null;
            try
            {
                var session = CreateVerifiedSession();
                int disclosures = 0;

                view = new M12BridgeTruthView(root.transform, session, () => false, _ =>
                {
                    disclosures++;
                    Assert.That(view.PublishTruthButton.interactable, Is.False);
                    view.PublishTruthButton.onClick.Invoke();
                });

                AdvanceToDisclosure(view);
                view.PublishTruthButton.onClick.Invoke();
                Assert.That(disclosures, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ThrowingOnDisclosureAfterDisposingViewDoesNotResurrectView()
        {
            var root = new GameObject("M12 view dispose-then-throw");
            string savedLanguage = SoundwellUiLocalization.Language;
            M12BridgeTruthView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = CreateVerifiedSession();

                view = new M12BridgeTruthView(root.transform, session, () => false, _ =>
                {
                    view.Dispose();
                    throw new InvalidOperationException("Host disposed view before throwing");
                });

                AdvanceToDisclosure(view);
                Assert.Throws<InvalidOperationException>(() =>
                    view.PublishTruthButton.onClick.Invoke());
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
