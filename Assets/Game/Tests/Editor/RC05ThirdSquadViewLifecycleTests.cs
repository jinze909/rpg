using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RC05ThirdSquadViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static RC05ThirdSquadSession CreateSession(Dictionary<string, int> values)
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

            var winch = new M08FirstWinchOrderSession();
            Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
            Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
            Assert.That(winch.TryAssemble(), Is.True);
            Assert.That(winch.TryTightenCable(), Is.True);
            Assert.That(winch.TryTestLift(), Is.True);
            Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);
            Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);

            Assert.That(RC05ThirdSquadSession.TryCreateFromSavedFacts(
                read, oldWomanStoneTriggered: false, out RC05ThirdSquadSession session), Is.True);
            return session;
        }

        private static void AdvanceToThreePartyDecision(RC05ThirdSquadView view)
        {
            view.ObserveRollCallButton.onClick.Invoke();
            view.DeliverSaltGrainButton.onClick.Invoke();
            view.ProceedTalksButton.onClick.Invoke();
        }

        [Test]
        public void PartialChildTeardownDisposesViewAndBlocksClickLayoutAndLocaleRefresh()
        {
            var root = new GameObject("RC05 view partial teardown");
            var values = new Dictionary<string, int>();
            string previousLanguage = SoundwellUiLocalization.Language;
            RC05ThirdSquadView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                RC05ThirdSquadSession session = CreateSession(values);
                int calls = 0;
                view = new RC05ThirdSquadView(
                    root.transform, session, () => true, () => true, () => true, () => false, _ => calls++);

                var rollCallBtn = view.ObserveRollCallButton;
                UnityEngine.Object.DestroyImmediate(view.Heading.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => rollCallBtn.onClick.Invoke());
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(session.Stage, Is.EqualTo(RC05Stage.MountainPassTracking));
                Assert.That(view.Panel == null, Is.True);

                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
                Assert.DoesNotThrow(() => SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidViewportAspectsAreIgnoredWithoutDistortingPanelScale()
        {
            var root = new GameObject("RC05 view invalid aspects");
            var values = new Dictionary<string, int>();
            RC05ThirdSquadView view = null;
            try
            {
                RC05ThirdSquadSession session = CreateSession(values);
                view = new RC05ThirdSquadView(
                    root.transform, session, () => true, () => true, () => true, () => false, _ => { });
                view.LayoutForViewportAspect(16f / 9f);
                Vector3 baselineScale = view.Panel.localScale;

                foreach (float invalidAspect in new[]
                    { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(invalidAspect));
                    Assert.That(view.Panel.localScale, Is.EqualTo(baselineScale));
                }
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReentrantResolutionSubmitClickIsIgnoredDuringSubmissionCallback()
        {
            var root = new GameObject("RC05 view reentrant submit");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            RC05ThirdSquadView view = null;
            try
            {
                RC05ThirdSquadSession session = CreateSession(values);
                int invocations = 0;
                view = new RC05ThirdSquadView(
                    root.transform, session, () => true, () => true, () => true, () => false, resolution =>
                    {
                        invocations++;
                        Assert.That(view.CivilianPardonButton.interactable, Is.False);
                        view.CivilianPardonButton.onClick.Invoke();
                        Assert.That(session.TrySettleResolution(resolution, read, write), Is.True);
                    });

                AdvanceToThreePartyDecision(view);
                view.CivilianPardonButton.onClick.Invoke();

                Assert.That(invocations, Is.EqualTo(1));
                Assert.That(session.IsResolved, Is.True);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposingViewInsideThrowingCallbackDoesNotThrowOnFinallyRefresh()
        {
            var root = new GameObject("RC05 view dispose in callback");
            var values = new Dictionary<string, int>();
            RC05ThirdSquadView view = null;
            try
            {
                RC05ThirdSquadSession session = CreateSession(values);
                view = new RC05ThirdSquadView(
                    root.transform, session, () => true, () => true, () => true, () => false, _ =>
                    {
                        view.Dispose();
                        throw new InvalidOperationException("Simulated RC05 teardown during submit");
                    });

                AdvanceToThreePartyDecision(view);
                Assert.Throws<InvalidOperationException>(() => view.EnlistLuJunButton.onClick.Invoke());
                Assert.That(view.IsDisposed, Is.True);
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
