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
        public void EffectiveInputLockProtectsDeliveryIdentityAndExplicitSettlement()
        {
            var root = new GameObject("RC05 effective input lock");
            var group = root.AddComponent<CanvasGroup>();
            var values = new Dictionary<string, int>();
            RC05ThirdSquadView view = null;
            try
            {
                RC05ThirdSquadSession session = CreateSession(values);
                int boatCalls = 0, rosterCalls = 0, testimonyCalls = 0, talksCalls = 0, submits = 0;
                bool boatMatched = false, rosterChecked = false, testimonyConfirmed = false;
                view = new RC05ThirdSquadView(root.transform, session,
                    () => { boatCalls++; return boatMatched; },
                    () => { rosterCalls++; return rosterChecked; },
                    () => { testimonyCalls++; return testimonyConfirmed; },
                    () => { talksCalls++; return false; },
                    resolution =>
                    {
                        submits++;
                        Assert.That(session.TrySettleResolution(
                            resolution, Reader(values), (key, value) => values[key] = value), Is.True);
                    });

                void AssertBlockedClicks()
                {
                    var stage = session.Stage;
                    bool roll = session.RollCallObserved;
                    bool delivery = session.TrialDeliveryVerified;
                    bool stone = session.OldWomanStoneMentioned;
                    bool identity = session.RealZhengYuanVerified;
                    bool skirmish = session.SkirmishOccurred;
                    int[] counts = { boatCalls, rosterCalls, testimonyCalls, talksCalls, submits };
                    var before = new Dictionary<string, int>(values);
                    void InvokeAll()
                    {
                        view.ObserveRollCallButton.onClick.Invoke();
                        view.VerifyZhengYuanButton.onClick.Invoke();
                        view.ProceedTalksButton.onClick.Invoke();
                    }

                    group.interactable = false;
                    InvokeAll();
                    group.interactable = true;
                    view.Panel.gameObject.SetActive(false);
                    InvokeAll();
                    view.Panel.gameObject.SetActive(true);

                    Assert.That(session.Stage, Is.EqualTo(stage));
                    Assert.That(session.RollCallObserved, Is.EqualTo(roll));
                    Assert.That(session.TrialDeliveryVerified, Is.EqualTo(delivery));
                    Assert.That(session.OldWomanStoneMentioned, Is.EqualTo(stone));
                    Assert.That(session.RealZhengYuanVerified, Is.EqualTo(identity));
                    Assert.That(session.SkirmishOccurred, Is.EqualTo(skirmish));
                    Assert.That(new[] { boatCalls, rosterCalls, testimonyCalls, talksCalls, submits },
                        Is.EqualTo(counts));
                    CollectionAssert.AreEquivalent(before, values);
                }

                AssertBlockedClicks();
                view.ObserveRollCallButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(RC05Stage.SaltGrainBoatTrial));
                AssertBlockedClicks();
                view.DeliverSaltGrainButton.onClick.Invoke();
                Assert.That(session.TrialDeliveryVerified, Is.False);
                boatMatched = true;
                AssertBlockedClicks();
                view.DeliverSaltGrainButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(RC05Stage.MillMeeting));
                Assert.That(session.TrialDeliveryVerified, Is.True);

                AssertBlockedClicks();
                view.MentionStoneButton.onClick.Invoke();
                Assert.That(session.OldWomanStoneMentioned, Is.False);
                rosterChecked = true;
                view.VerifyZhengYuanButton.onClick.Invoke();
                Assert.That(session.RealZhengYuanVerified, Is.False);
                testimonyConfirmed = true;
                AssertBlockedClicks();
                view.VerifyZhengYuanButton.onClick.Invoke();
                Assert.That(session.RealZhengYuanVerified, Is.True);
                AssertBlockedClicks();
                view.ProceedTalksButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(RC05Stage.ThreePartyDecision));
                Assert.That(talksCalls, Is.EqualTo(1));
                Assert.That(session.SkirmishOccurred, Is.False);

                AssertBlockedClicks();
                Assert.That(values.ContainsKey(RC05ThirdSquadFacts.ConfirmedKey), Is.False);
                view.CivilianPardonButton.onClick.Invoke();
                Assert.That(submits, Is.EqualTo(1));
                Assert.That(session.IsResolved, Is.True);
                Assert.That(RC05ThirdSquadFacts.TryRead(Reader(values), out RC05ThirdSquadRecord? record), Is.True);
                Assert.That(record.HasValue, Is.True);
                Assert.That(record.Value.TrialDeliveryVerified, Is.True);
                Assert.That(record.Value.RealZhengYuanVerified, Is.True);
                Assert.That(record.Value.OldWomanStoneMentioned, Is.False);
                Assert.That(record.Value.SkirmishOccurred, Is.False);
                Assert.That(record.Value.NeedsM10SupplyContract, Is.True);
                AssertBlockedClicks();
                view.EnlistLuJunButton.onClick.Invoke();
                Assert.That(submits, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
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
