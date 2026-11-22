using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RC03BaijiMineViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static RC03BaijiMineSession CreateSession(Dictionary<string, int> values)
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

            Assert.That(RC03BaijiMineSession.TryCreateFromSavedFacts(
                read, out RC03BaijiMineSession session), Is.True);
            return session;
        }

        private static void CompleteIslandVisits(RC03BaijiMineView view)
        {
            view.CanteenVisitButton.onClick.Invoke();
            view.PumpRoomVisitButton.onClick.Invoke();
            view.CharterTalksButton.onClick.Invoke();
        }

        [Test]
        public void PartialChildTeardownDisposesViewAndBlocksTopClickLayoutAndLocaleRefresh()
        {
            var root = new GameObject("RC03 view partial teardown top");
            var values = new Dictionary<string, int>();
            string previousLanguage = SoundwellUiLocalization.Language;
            RC03BaijiMineView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                RC03BaijiMineSession session = CreateSession(values);
                int calls = 0;
                view = new RC03BaijiMineView(
                    root.transform, session, () => true, () => true, () => true, _ => calls++);

                var canteenBtn = view.CanteenVisitButton;
                UnityEngine.Object.DestroyImmediate(view.Heading.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => canteenBtn.onClick.Invoke());
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(session.VisitsCompleted, Is.EqualTo(RC03IslandVisits.None));
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
        public void PartialChildTeardownOnBottomClickDisposesViewCleanly()
        {
            var root = new GameObject("RC03 view partial teardown bottom");
            var values = new Dictionary<string, int>();
            RC03BaijiMineView view = null;
            try
            {
                RC03BaijiMineSession session = CreateSession(values);
                int calls = 0;
                view = new RC03BaijiMineView(
                    root.transform, session, () => true, () => true, () => true, _ => calls++);
                CompleteIslandVisits(view);

                var evacBtn = view.SecretEvacuateButton;
                UnityEngine.Object.DestroyImmediate(view.Body.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => evacBtn.onClick.Invoke());
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(view.Panel == null, Is.True);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidViewportAspectsAreIgnoredWithoutDistortingPanelScale()
        {
            var root = new GameObject("RC03 view invalid aspects");
            var values = new Dictionary<string, int>();
            RC03BaijiMineView view = null;
            try
            {
                RC03BaijiMineSession session = CreateSession(values);
                view = new RC03BaijiMineView(
                    root.transform, session, () => true, () => true, () => true, _ => { });
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
        public void ReentrantResolutionSubmitAndDisposeInsideThrowingCallbackAreGuarded()
        {
            var root = new GameObject("RC03 view reentrant and dispose");
            var values1 = new Dictionary<string, int>();
            var values2 = new Dictionary<string, int>();
            RC03BaijiMineView view1 = null;
            RC03BaijiMineView view2 = null;
            try
            {
                RC03BaijiMineSession session1 = CreateSession(values1);
                Func<string, int> read1 = Reader(values1);
                Action<string, int> write1 = (k, v) => values1[k] = v;
                int invocations = 0;
                view1 = new RC03BaijiMineView(
                    root.transform, session1, () => true, () => true, () => true, resolution =>
                    {
                        invocations++;
                        Assert.That(view1.SecretEvacuateButton.interactable, Is.False);
                        view1.SecretEvacuateButton.onClick.Invoke();
                        Assert.That(session1.TrySettleResolution(resolution, read1, write1), Is.True);
                    });

                CompleteIslandVisits(view1);
                view1.SecretEvacuateButton.onClick.Invoke();
                Assert.That(invocations, Is.EqualTo(1));
                Assert.That(session1.IsResolved, Is.True);

                RC03BaijiMineSession session2 = CreateSession(values2);
                view2 = new RC03BaijiMineView(
                    root.transform, session2, () => true, () => true, () => true, _ =>
                    {
                        view2.Dispose();
                        throw new InvalidOperationException("Simulated RC03 teardown during submit");
                    });

                CompleteIslandVisits(view2);
                Assert.Throws<InvalidOperationException>(() => view2.SecretEvacuateButton.onClick.Invoke());
                Assert.That(view2.IsDisposed, Is.True);
                Assert.DoesNotThrow(() => view2.Refresh());
                Assert.DoesNotThrow(() => view2.LayoutForViewportAspect(16f / 9f));
            }
            finally
            {
                view1?.Dispose();
                view2?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
