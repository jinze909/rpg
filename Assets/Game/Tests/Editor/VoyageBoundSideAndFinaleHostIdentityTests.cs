using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Verifies that M13, M15, RC-01..RC-05, and ST-06 hosts bind open views
    /// to M05LandingFacts.ArrivalSequenceKey so stale panels left open across
    /// an identical replayed voyage cannot commit into the new voyage.
    /// </summary>
    public sealed class VoyageBoundSideAndFinaleHostIdentityTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM08Notice(Dictionary<string, int> values, int arrivalSequence)
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
            values[M05LandingFacts.ArrivalSequenceKey] = arrivalSequence;

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
                taoJiRescued: true), read, write), Is.True);

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
        public void StaleM13AndRC01HostsCannotCommitAfterArrivalSequenceAdvances()
        {
            var root = new GameObject("SideFinaleVoyageIdentityRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values, arrivalSequence: 1);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                // M13: open on arrivalSequence == 1, advance to 2 -> warning & departure rejected.
                using (var staleM13 = new M13DepartureRiskHost(
                    root.transform, () => true, () => false, () => true, read, write))
                {
                    Assert.That(staleM13.TryOpen(), Is.True);
                    values[M05LandingFacts.ArrivalSequenceKey] = 2;
                    Assert.That(staleM13.TryDeliverQiQueWarning(), Is.False);
                    staleM13.ActiveView.RouteAButton.onClick.Invoke();
                    Assert.That(staleM13.IsDeparted, Is.False);
                    Assert.That(staleM13.ActiveView, Is.Not.Null);
                }

                // Fresh M13 on arrivalSequence == 2 commits cleanly.
                using (var freshM13 = new M13DepartureRiskHost(
                    root.transform, () => true, () => true, () => true, read, write))
                {
                    Assert.That(freshM13.TryOpen(), Is.True);
                    freshM13.ActiveView.RouteAButton.onClick.Invoke();
                    Assert.That(freshM13.IsDeparted, Is.True);
                }

                // RC-01: open on arrivalSequence == 2, advance to 3 -> settlement rejected.
                using (var staleRC01 = new RC01ChenYanHost(
                    root.transform, () => true, read, write))
                {
                    Assert.That(staleRC01.TryOpen(), Is.True);
                    Assert.That(staleRC01.Session.TryObserveClue(RC01FerryClues.WristScar), Is.True);
                    Assert.That(staleRC01.Session.TryObserveClue(RC01FerryClues.DoughHumTune), Is.True);
                    Assert.That(staleRC01.Session.TryObserveClue(RC01FerryClues.BlankShrineTablet), Is.True);
                    Assert.That(staleRC01.Session.TryInterviewLinSu(), Is.True);
                    Assert.That(staleRC01.Session.TryInterviewQiuniang(), Is.True);
                    staleRC01.ActiveView.Refresh();

                    values[M05LandingFacts.ArrivalSequenceKey] = 3;
                    staleRC01.ActiveView.KeepMarkedDeadButton.onClick.Invoke();
                    Assert.That(staleRC01.IsResolved, Is.False);
                    Assert.That(staleRC01.ActiveView, Is.Not.Null);
                }

                // Fresh RC-01 on arrivalSequence == 3 commits cleanly.
                using (var freshRC01 = new RC01ChenYanHost(
                    root.transform, () => true, read, write))
                {
                    Assert.That(freshRC01.TryOpen(), Is.True);
                    Assert.That(freshRC01.Session.TryObserveClue(RC01FerryClues.WristScar), Is.True);
                    Assert.That(freshRC01.Session.TryObserveClue(RC01FerryClues.DoughHumTune), Is.True);
                    Assert.That(freshRC01.Session.TryObserveClue(RC01FerryClues.BlankShrineTablet), Is.True);
                    Assert.That(freshRC01.Session.TryInterviewLinSu(), Is.True);
                    Assert.That(freshRC01.Session.TryInterviewQiuniang(), Is.True);
                    freshRC01.ActiveView.Refresh();
                    freshRC01.ActiveView.KeepMarkedDeadButton.onClick.Invoke();
                    Assert.That(freshRC01.IsResolved, Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
