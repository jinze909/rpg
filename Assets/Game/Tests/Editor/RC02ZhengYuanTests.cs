using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RC02ZhengYuanTests
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

        private static RC02ZhengYuanSession ReachDeskDecision(
            Func<string, int> read, bool realZhengYuanFound = false)
        {
            Assert.That(RC02ZhengYuanSession.TryCreateFromSavedFacts(
                read, realZhengYuanFound, out var session), Is.True);
            Assert.That(session.TryCompleteIntakeCheck(RC02IntakeChecks.UpperLeftToothCheck), Is.True);
            Assert.That(session.TryCompleteIntakeCheck(RC02IntakeChecks.BattleFlankQuestion), Is.True);
            Assert.That(session.TryCompleteIntakeCheck(RC02IntakeChecks.DominantHandQuestion), Is.True);
            Assert.That(session.TryHearRoofConfession(), Is.True);
            return session;
        }

        [Test]
        public void CaseRequiresM08NoticeAllThreeIntakeChecksAndRoofConfession()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(RC02ZhengYuanSession.TryCreateFromSavedFacts(
                read, false, out var blocked), Is.False);
            Assert.That(blocked, Is.Null);

            SeedM08RosterCasesAvailable(values);
            Assert.That(RC02ZhengYuanSession.TryCreateFromSavedFacts(
                read, false, out var session), Is.True);
            Assert.That(session.TryHearRoofConfession(), Is.False);
            Assert.That(session.TryCompleteIntakeCheck(RC02IntakeChecks.UpperLeftToothCheck), Is.True);
            Assert.That(session.TryCompleteIntakeCheck(RC02IntakeChecks.UpperLeftToothCheck), Is.False);
            Assert.That(session.TryCompleteIntakeCheck(RC02IntakeChecks.BattleFlankQuestion), Is.True);
            Assert.That(session.TryHearRoofConfession(), Is.False);

            Assert.That(session.TryCompleteIntakeCheck(RC02IntakeChecks.DominantHandQuestion), Is.True);
            Assert.That(session.Stage, Is.EqualTo(RC02Stage.RoofConfession));
            Assert.That(session.TrySettleResolution(RC02Resolution.ExposeImpostor, read, write), Is.False);

            Assert.That(session.TryHearRoofConfession(), Is.True);
            Assert.That(session.Stage, Is.EqualTo(RC02Stage.DeskDecision));
        }

        [Test]
        public void AdoptionRequiresMotherCallingShiLengAndMutualConsentOrReturnsToDesk()
        {
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = ReachDeskDecision(read, realZhengYuanFound: true);
            Assert.That(session.TrySettleResolution(RC02Resolution.AdoptAsShiLeng, read, write), Is.False);

            Assert.That(session.TryBeginAdoptionNegotiation(), Is.True);
            Assert.That(session.Stage, Is.EqualTo(RC02Stage.AdoptionNameMoment));

            // Speaking for her fails the naming moment and returns to DeskDecision.
            Assert.That(session.TryResolveAdoptionMoment(
                RC02AdoptionCompanionship.SpeakForHer, shiLengWilling: true), Is.False);
            Assert.That(session.Stage, Is.EqualTo(RC02Stage.DeskDecision));

            // Waiting quietly while Shi Leng declines also returns to DeskDecision.
            Assert.That(session.TryBeginAdoptionNegotiation(), Is.True);
            Assert.That(session.TryResolveAdoptionMoment(
                RC02AdoptionCompanionship.WaitQuietly, shiLengWilling: false), Is.False);
            Assert.That(session.Stage, Is.EqualTo(RC02Stage.DeskDecision));

            // Waiting quietly with Shi Leng consenting satisfies both requirements.
            Assert.That(session.TryBeginAdoptionNegotiation(), Is.True);
            Assert.That(session.TryResolveAdoptionMoment(
                RC02AdoptionCompanionship.WaitQuietly, shiLengWilling: true), Is.True);
            Assert.That(session.TrySettleResolution(RC02Resolution.AdoptAsShiLeng, read, write), Is.True);

            Assert.That(RC02ZhengYuanFacts.TryRead(read, out RC02ZhengYuanRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.Resolution, Is.EqualTo(RC02Resolution.AdoptAsShiLeng));
            Assert.That(saved.Value.MotherCalledShiLeng, Is.True);
            Assert.That(saved.Value.ShiLengConsented, Is.True);
            Assert.That(saved.Value.RealZhengYuanFound, Is.True);
            Assert.That(saved.Value.WeakensNegotiationCard, Is.False);
        }

        [Test]
        public void ExceptionResolutionFlagsNegotiationAndDuplicateIdentityRisksAndRecoversRetries()
        {
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == RC02ZhengYuanFacts.ResolutionKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == RC02ZhengYuanFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };

            var session = ReachDeskDecision(read);
            Assert.Throws<InvalidOperationException>(() =>
                session.TrySettleResolution(RC02Resolution.AllowByException, read, unstable));
            Assert.That(session.IsResolved, Is.False);

            Assert.Throws<InvalidOperationException>(() =>
                session.TrySettleResolution(RC02Resolution.AllowByException, read, unstable));
            Assert.That(confirmCommits, Is.EqualTo(1));

            Assert.That(session.TrySettleResolution(RC02Resolution.ExposeImpostor, read, unstable), Is.False);
            Assert.That(session.TrySettleResolution(RC02Resolution.AllowByException, read, unstable), Is.True);
            Assert.That(confirmCommits, Is.EqualTo(1));
            Assert.That(session.CompletedRecord.Value.WeakensNegotiationCard, Is.True);
            Assert.That(session.CompletedRecord.Value.RequiresSeparateRealZhengYuanVerification, Is.True);
        }

        [Test]
        public void TornIntakeIntentRejectsDifferentRegistrationOrRealIdentity()
        {
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var original = new RC02ZhengYuanRecord(
                RC02IntakeChecks.All, true, RC02Resolution.AllowByException,
                false, false, realZhengYuanFound: true);
            var differentDecision = new RC02ZhengYuanRecord(
                RC02IntakeChecks.All, true, RC02Resolution.ExposeImpostor,
                false, false, realZhengYuanFound: true);
            var differentIdentity = new RC02ZhengYuanRecord(
                RC02IntakeChecks.All, true, RC02Resolution.AllowByException,
                false, false, realZhengYuanFound: false);

            // The signature must be the FIRST persistent story write. Crash
            // before either the decision or the identity flag is persisted.
            Assert.Throws<InvalidOperationException>(() =>
                RC02ZhengYuanFacts.TryRecord(original, read, (key, value) =>
                {
                    write(key, value);
                    if (key == RC02ZhengYuanFacts.PendingSignatureKey)
                        throw new InvalidOperationException("Interrupted after intent");
                }));
            Assert.That(read(RC02ZhengYuanFacts.PendingSignatureKey), Is.Not.Zero);
            Assert.That(RC02ZhengYuanFacts.TryRead(read, out _), Is.False);
            Assert.That(RC02ZhengYuanFacts.TryRecord(
                differentDecision, read, write), Is.False);
            Assert.That(RC02ZhengYuanFacts.TryRecord(
                differentIdentity, read, write), Is.False);
            Assert.That(RC02ZhengYuanFacts.TryRecord(original, read, write), Is.True);
            Assert.That(RC02ZhengYuanFacts.TryRead(
                read, out RC02ZhengYuanRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.Resolution, Is.EqualTo(original.Resolution));
            Assert.That(saved.Value.RealZhengYuanFound, Is.True);

            values[RC02ZhengYuanFacts.PendingSignatureKey] = 0;
            Assert.That(RC02ZhengYuanFacts.TryRead(
                read, out RC02ZhengYuanRecord? legacy), Is.True);
            Assert.That(legacy.HasValue, Is.True,
                "A fully confirmed pre-upgrade identity case must stay readable.");

            Assert.That(RC02ZhengYuanFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(read(RC02ZhengYuanFacts.PendingSignatureKey), Is.Zero);
            values[RC02ZhengYuanFacts.ChecksMaskKey] = (int)RC02IntakeChecks.All;
            Assert.That(RC02ZhengYuanFacts.TryRead(read, out _), Is.False);
            Assert.That(RC02ZhengYuanFacts.TryRecord(original, read, write), Is.False);
        }

        [Test]
        public void CorruptOrResetUpstreamFactsInvalidateReadbackAndNullDelegatesFailClosed()
        {
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            // Adoption without mother calling name must be rejected by facts guard.
            Assert.That(RC02ZhengYuanFacts.TryRecord(
                new RC02ZhengYuanRecord(
                    RC02IntakeChecks.All, true, RC02Resolution.AdoptAsShiLeng,
                    motherCalledShiLeng: false, shiLengConsented: true, realZhengYuanFound: false),
                read, write), Is.False);

            Assert.That(RC02ZhengYuanFacts.TryRecord(
                new RC02ZhengYuanRecord(
                    RC02IntakeChecks.All, true, RC02Resolution.ExposeImpostor,
                    motherCalledShiLeng: false, shiLengConsented: false, realZhengYuanFound: false),
                read, write), Is.True);

            values[RC02ZhengYuanFacts.ResolutionKey] = 99;
            Assert.That(RC02ZhengYuanFacts.TryRead(read, out RC02ZhengYuanRecord? corrupt), Is.True);
            Assert.That(corrupt, Is.Null);

            values[RC02ZhengYuanFacts.ResolutionKey] = (int)RC02Resolution.ExposeImpostor;
            Assert.That(M08FirstWorldNoticeFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(RC02ZhengYuanFacts.TryRead(read, out RC02ZhengYuanRecord? stale), Is.True);
            Assert.That(stale, Is.Null);

            Assert.That(RC02ZhengYuanFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[RC02ZhengYuanFacts.ConfirmedKey], Is.Zero);
            Assert.That(RC02ZhengYuanFacts.TryRead(null, out _), Is.False);
            Assert.That(RC02ZhengYuanFacts.TryResetForNewVoyage(null), Is.False);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                Assert.That(RC02ZhengYuanFacts.TryRead(out _), Is.False);
        }
    }
}
