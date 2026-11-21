using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RC05ThirdSquadViewHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM08RosterCasesAvailable(Dictionary<string, int> values)
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
        }

        [Test]
        public void HostCannotOpenWithoutM08NoticePresenceOrCleanConfirmedKey()
        {
            var root = new GameObject("RC05 host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool atMill = false;
            RC05ThirdSquadHost host = null;
            try
            {
                host = new RC05ThirdSquadHost(
                    root.transform, () => atMill, () => false, () => true,
                    () => true, () => true, () => false, read, write);
                Assert.That(host.TryOpen(), Is.False);

                SeedM08RosterCasesAvailable(values);
                Assert.That(host.TryOpen(), Is.False);

                atMill = true;
                values[RC05ThirdSquadFacts.ConfirmedKey] = 2;
                Assert.That(host.TryOpen(), Is.False);

                values[RC05ThirdSquadFacts.ConfirmedKey] = 0;
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.IsResolved, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FullFlowVerifiesBoatTrialStoneCallbackRealZhengYuanAndPardonOnce()
        {
            var root = new GameObject("RC05 host pardon");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool boatMatched = false;
            bool rosterChecked = false;
            RC05ThirdSquadHost host = null;
            RC05ThirdSquadHost duplicate = null;
            try
            {
                host = new RC05ThirdSquadHost(
                    root.transform, () => true, () => true, () => boatMatched,
                    () => rosterChecked, () => true, () => false, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;

                view.ObserveRollCallButton.onClick.Invoke();
                view.DeliverSaltGrainButton.onClick.Invoke();
                Assert.That(host.Session.Stage, Is.EqualTo(RC05Stage.SaltGrainBoatTrial));

                boatMatched = true;
                view.DeliverSaltGrainButton.onClick.Invoke();
                Assert.That(host.Session.Stage, Is.EqualTo(RC05Stage.MillMeeting));

                Assert.That(view.MentionStoneButton.gameObject.activeSelf, Is.True);
                view.MentionStoneButton.onClick.Invoke();
                Assert.That(host.Session.OldWomanStoneMentioned, Is.True);

                view.VerifyZhengYuanButton.onClick.Invoke();
                Assert.That(host.Session.RealZhengYuanVerified, Is.False);

                rosterChecked = true;
                view.VerifyZhengYuanButton.onClick.Invoke();
                Assert.That(host.Session.RealZhengYuanVerified, Is.True);

                view.ProceedTalksButton.onClick.Invoke();
                view.CivilianPardonButton.onClick.Invoke();

                Assert.That(host.IsResolved, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(RC05ThirdSquadFacts.TryRead(read, out RC05ThirdSquadRecord? saved), Is.True);
                Assert.That(saved.Value.Resolution, Is.EqualTo(RC05Resolution.CivilianPardon));
                Assert.That(saved.Value.RealZhengYuanVerified, Is.True);
                Assert.That(saved.Value.OldWomanStoneMentioned, Is.True);

                duplicate = new RC05ThirdSquadHost(
                    root.transform, () => true, () => true, () => true,
                    () => true, () => true, () => false, read, write);
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
        public void InterruptedSaveAndCommitThenThrowRecoverWithoutDuplicateWrites()
        {
            var root = new GameObject("RC05 host retry");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == RC05ThirdSquadFacts.ResolutionKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == RC05ThirdSquadFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };
            RC05ThirdSquadHost host = null;
            try
            {
                host = new RC05ThirdSquadHost(
                    root.transform, () => true, () => false, () => true,
                    () => false, () => false, () => true, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.ObserveRollCallButton.onClick.Invoke();
                view.DeliverSaltGrainButton.onClick.Invoke();
                Assert.That(view.MentionStoneButton.gameObject.activeSelf, Is.False);
                view.ProceedTalksButton.onClick.Invoke();

                Assert.Throws<InvalidOperationException>(() =>
                    view.ContinueSmugglingButton.onClick.Invoke());
                Assert.That(host.IsResolved, Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);

                Assert.Throws<InvalidOperationException>(() =>
                    view.ContinueSmugglingButton.onClick.Invoke());
                Assert.That(host.IsResolved, Is.True);
                Assert.That(confirmCommits, Is.EqualTo(1));

                view.ContinueSmugglingButton.onClick.Invoke();
                Assert.That(confirmCommits, Is.EqualTo(1));
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void SettlementRechecksLiveMillEvidenceAndAllowsRecovery(int withdrawnProof)
        {
            var root = new GameObject("RC05 live settlement proofs");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            // Boatman count, squad roster, witness testimony, M00 stele,
            // and recorded skirmish must still match the final scene.
            var proof = new[] { true, true, true, true, true };
            RC05ThirdSquadHost host = null;
            try
            {
                host = new RC05ThirdSquadHost(root.transform, () => true,
                    () => proof[3], () => proof[0], () => proof[1],
                    () => proof[2], () => proof[4], read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.ObserveRollCallButton.onClick.Invoke();
                view.DeliverSaltGrainButton.onClick.Invoke();
                view.MentionStoneButton.onClick.Invoke();
                view.VerifyZhengYuanButton.onClick.Invoke();
                view.ProceedTalksButton.onClick.Invoke();
                Assert.That(host.Session.Stage, Is.EqualTo(RC05Stage.ThreePartyDecision));
                Assert.That(host.Session.OldWomanStoneMentioned, Is.True);
                Assert.That(host.Session.RealZhengYuanVerified, Is.True);
                Assert.That(host.Session.SkirmishOccurred, Is.True);

                proof[withdrawnProof] = false;
                view.CivilianPardonButton.onClick.Invoke();
                Assert.That(host.IsResolved, Is.False,
                    "The stale decision must not persist withdrawn scene facts.");
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(values.ContainsKey(RC05ThirdSquadFacts.ConfirmedKey), Is.False);

                proof[withdrawnProof] = true;
                view.CivilianPardonButton.onClick.Invoke();
                Assert.That(host.IsResolved, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(RC05ThirdSquadFacts.TryRead(read,
                    out RC05ThirdSquadRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.Resolution, Is.EqualTo(RC05Resolution.CivilianPardon));
                Assert.That(saved.Value.RealZhengYuanVerified, Is.True);
                Assert.That(saved.Value.OldWomanStoneMentioned, Is.True);
                Assert.That(saved.Value.SkirmishOccurred, Is.True);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocaleAspectPresenceWithdrawalAndDisposalGuardsWorkCleanly()
        {
            var root = new GameObject("RC05 host guards");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string prevLang = SoundwellUiLocalization.Language;
            bool atMill = true;
            RC05ThirdSquadHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                host = new RC05ThirdSquadHost(
                    root.transform, () => atMill, () => false, () => true,
                    () => false, () => false, () => false, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.That(view.Heading.text, Does.Contain("MOUNTAIN PASS ROLL CALL"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                view.ObserveRollCallButton.onClick.Invoke();
                view.DeliverSaltGrainButton.onClick.Invoke();
                view.ProceedTalksButton.onClick.Invoke();
                var enlistBtn = view.EnlistLuJunButton;

                atMill = false;
                enlistBtn.onClick.Invoke();
                Assert.That(host.IsResolved, Is.False);

                atMill = true;
                host.Dispose();
                enlistBtn.onClick.Invoke();
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.IsResolved, Is.False);
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
