using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ST04EchoValleyTests
    {
        private static Func<string, int> Read(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int found) ? found : 0;
        }

        private static Action<string, int> Write(Dictionary<string, int> values)
        {
            return (key, value) => values[key] = value;
        }

        private static ST04EchoValleySession ReachResolution(
            Dictionary<string, int> values,
            ST04BeastHandling handling,
            ST04ValleyEntry entry = ST04ValleyEntry.EnterQuietly)
        {
            Assert.That(ST04EchoValleySession.TryCreate(Read(values),
                out ST04EchoValleySession session), Is.True);
            Assert.That(session.TryInspectClue(ST04RoadClue.SlippedClawsAtOldBellRope), Is.True);
            Assert.That(session.TryInspectClue(ST04RoadClue.HoofprintsTurningToRockCrack), Is.True);
            Assert.That(session.TryInspectClue(ST04RoadClue.MossFellOnBellFacingSide), Is.True);
            Assert.That(session.TryChooseEntry(entry), Is.True);
            Assert.That(session.TryRescueSurveyors(2), Is.True);
            Assert.That(session.TryVerifySoundAndRainMechanism(true, true, true, true, true),
                Is.True);
            Assert.That(session.TryEnterInnerChamber(ST04ChamberEntry.LureWithBell), Is.True);
            Assert.That(session.TryOpenDrain(true, true), Is.True);
            Assert.That(session.TryResolveBeasts(handling), Is.True);
            return session;
        }

        [Test]
        public void RoadCluesTwoSurveyorsSoundRulesAndCompanionSafetyGateInnerChamber()
        {
            var values = new Dictionary<string, int>();
            Assert.That(ST04EchoValleySession.TryCreate(Read(values),
                out ST04EchoValleySession session), Is.True);
            Assert.That(session.TryChooseEntry(ST04ValleyEntry.FollowSangJingAndBell), Is.False);
            Assert.That(session.TryInspectClue(ST04RoadClue.SlippedClawsAtOldBellRope), Is.True);
            Assert.That(session.TryInspectClue(ST04RoadClue.SlippedClawsAtOldBellRope), Is.False);
            Assert.That(session.TryInspectClue(ST04RoadClue.HoofprintsTurningToRockCrack), Is.True);
            Assert.That(session.TryInspectClue(ST04RoadClue.MossFellOnBellFacingSide), Is.True);
            Assert.That(session.TryChooseEntry(ST04ValleyEntry.SurveyEchoesFromRidge), Is.True);
            Assert.That(session.TryRescueSurveyors(1), Is.False);
            Assert.That(session.TryRescueSurveyors(2), Is.True);
            Assert.That(session.TryVerifySoundAndRainMechanism(
                true, true, false, true, true), Is.False);
            Assert.That(session.TryEnterInnerChamber(ST04ChamberEntry.OpenVentGrate), Is.False);
            Assert.That(session.TryVerifySoundAndRainMechanism(
                true, true, true, true, true), Is.True);
            Assert.That(session.TryEnterInnerChamber(ST04ChamberEntry.OpenVentGrate), Is.True);
            Assert.That(session.TryOpenDrain(false, true), Is.False);
            Assert.That(session.TryOpenDrain(true, true), Is.True);
            Assert.That(session.TryResolveBeasts(ST04BeastHandling.LedBackToOldHabitat),
                Is.True);
            Assert.That(session.TryChooseResolution(
                ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad, false, false),
                Is.False);
            Assert.That(session.TryChooseResolution(
                ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad, true, false),
                Is.True);
            Assert.That(session.TryCommit(Read(values), Write(values)), Is.True);
        }

        [Test]
        public void EveryEndingHasItsOwnBeastTreatmentAndHonestRoadConsequence()
        {
            var choices = new[]
            {
                ST04RoadResolution.KillAdultsAndCloseWell,
                ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad,
                ST04RoadResolution.PreserveRainWallAndCloseRoad
            };
            var handling = new[]
            {
                ST04BeastHandling.Slain,
                ST04BeastHandling.LedBackToOldHabitat,
                ST04BeastHandling.ContainedAwayFromClosedRoad
            };
            for (int i = 0; i < choices.Length; i++)
            {
                var values = new Dictionary<string, int>();
                var session = ReachResolution(values, handling[i]);
                bool pay = i == 1;
                bool worksite = i == 2;
                Assert.That(session.TryChooseResolution(choices[(i + 1) % choices.Length],
                    false, false), Is.False);
                Assert.That(session.TryChooseResolution(choices[i], pay, worksite), Is.True);
                Assert.That(session.TryCommit(Read(values), Write(values)), Is.True);
                Assert.That(ST04EchoValleyFacts.TryRead(Read(values),
                    out ST04EchoValleyOutcome? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.Story.RainWallPartlyCollapsed, Is.EqualTo(i == 0));
                Assert.That(saved.Value.Story.UsesFundedLongRoad, Is.EqualTo(i == 1));
                Assert.That(saved.Value.Story.OriginalTransportRoadPermanentlyClosed,
                    Is.EqualTo(i == 2));
                Assert.That(saved.Value.ShowZhonglingStoneOnM15Monument, Is.False,
                    "An operating route does not mean the quarry accepted or delivered an order.");
            }
        }

        [Test]
        public void MonumentStoneRequiresActualOrderAndDeliveryAndPreservedRoadNeedsAlternativeRoute()
        {
            var first = new Dictionary<string, int>();
            var ordinary = ReachResolution(first, ST04BeastHandling.Slain);
            Assert.That(ordinary.TryChooseResolution(
                ST04RoadResolution.KillAdultsAndCloseWell, false, false), Is.True);
            Assert.That(ordinary.TryCommit(Read(first), Write(first)), Is.True);
            Assert.That(ST04EchoValleyFacts.TryMarkStoneDelivered(Read(first), Write(first)),
                Is.False);
            Assert.That(ST04EchoValleyFacts.TryMarkStoneOrderAccepted(Read(first), Write(first)),
                Is.True);
            Assert.That(ST04EchoValleyFacts.TryRead(Read(first),
                out ST04EchoValleyOutcome? ordered), Is.True);
            Assert.That(ordered.Value.ShowZhonglingStoneOnM15Monument, Is.False);
            Assert.That(ST04EchoValleyFacts.TryMarkStoneDelivered(Read(first), Write(first)),
                Is.True);
            Assert.That(ST04EchoValleyFacts.TryRead(Read(first),
                out ST04EchoValleyOutcome? delivered), Is.True);
            Assert.That(delivered.Value.ShowZhonglingStoneOnM15Monument, Is.True);

            var preserved = new Dictionary<string, int>();
            var preserveSession = ReachResolution(preserved,
                ST04BeastHandling.ContainedAwayFromClosedRoad);
            Assert.That(preserveSession.TryChooseResolution(
                ST04RoadResolution.PreserveRainWallAndCloseRoad, false, false), Is.False,
                "Alternative worker livelihood must be arranged first.");
            Assert.That(preserveSession.TryChooseResolution(
                ST04RoadResolution.PreserveRainWallAndCloseRoad, false, true), Is.True);
            Assert.That(preserveSession.TryCommit(Read(preserved), Write(preserved)), Is.True);
            Assert.That(ST04EchoValleyFacts.TryMarkStoneOrderAccepted(
                Read(preserved), Write(preserved)), Is.False,
                "An alternative worksite does not automatically create a stone delivery road.");
            Assert.That(ST04EchoValleyFacts.TryVerifyAdditionalStoneTransport(
                Read(preserved), Write(preserved)), Is.True);
            Assert.That(ST04EchoValleyFacts.TryMarkStoneOrderAccepted(
                Read(preserved), Write(preserved)), Is.True);
            Assert.That(ST04EchoValleyFacts.TryMarkStoneDelivered(
                Read(preserved), Write(preserved)), Is.True);
            Assert.That(ST04EchoValleyFacts.TryRead(Read(preserved),
                out ST04EchoValleyOutcome? actualDelivery), Is.True);
            Assert.That(actualDelivery.Value.ShowZhonglingStoneOnM15Monument, Is.True);
        }

        [Test]
        public void TornWriteCannotChangeDecisionAndSameChoiceMayReplayFromNewSession()
        {
            var values = new Dictionary<string, int>();
            var session = ReachResolution(values,
                ST04BeastHandling.ContainedAwayFromClosedRoad);
            Assert.That(session.TryChooseResolution(
                ST04RoadResolution.PreserveRainWallAndCloseRoad, false, true), Is.True);
            var read = Read(values);
            var write = Write(values);
            Assert.Throws<InvalidOperationException>(() => session.TryCommit(read,
                (key, value) =>
                {
                    write(key, value);
                    if (key == ST04EchoValleyFacts.IntentKey)
                        throw new InvalidOperationException("Interrupted immediately after intent");
                }));
            Assert.That(ST04EchoValleyFacts.TryRead(read, out _), Is.False);
            var competing = new ST04EchoValleyRecord(
                ST04ValleyEntry.EnterQuietly, ST04ChamberEntry.LureWithBell,
                ST04BeastHandling.Slain, ST04RoadResolution.KillAdultsAndCloseWell,
                2, true, true, true, false, false);
            Assert.That(ST04EchoValleyFacts.TryRecord(competing, read, write), Is.False);
            Assert.That(session.TryCommit(read, write), Is.True);
            Assert.That(ST04EchoValleyFacts.TryRead(read,
                out ST04EchoValleyOutcome? saved), Is.True);
            Assert.That(saved.Value.Story.Resolution,
                Is.EqualTo(ST04RoadResolution.PreserveRainWallAndCloseRoad));
            Assert.That(ST04EchoValleySession.TryCreate(read, out _), Is.False);
        }

        [Test]
        public void CorruptFlagsCannotForgeStoneAndExplicitNewGameClearsEverything()
        {
            var values = new Dictionary<string, int>();
            var session = ReachResolution(values, ST04BeastHandling.Slain);
            Assert.That(session.TryChooseResolution(
                ST04RoadResolution.KillAdultsAndCloseWell, false, false), Is.True);
            Assert.That(session.TryCommit(Read(values), Write(values)), Is.True);
            var read = Read(values);
            values[ST04EchoValleyFacts.StoneShipmentKey] =
                (int)ST04StoneShipment.DeliveredToLinchao;
            Assert.That(ST04EchoValleyFacts.TryRead(read,
                out ST04EchoValleyOutcome? deliveredWithoutOrder), Is.True);
            // The delivery flag is a downstream event; the scene host must
            // write it only after a real accepted order and physical arrival.
            Assert.That(deliveredWithoutOrder.HasValue, Is.True);
            values[ST04EchoValleyFacts.StoneShipmentKey] = 0;
            values[ST04EchoValleyFacts.SurveyorsKey] = 3;
            Assert.That(ST04EchoValleyFacts.TryRead(read, out _), Is.False);
            values[ST04EchoValleyFacts.SurveyorsKey] = 2;
            values[ST04EchoValleyFacts.AlternativeTransportKey] = 1;
            Assert.That(ST04EchoValleyFacts.TryRead(read, out _), Is.False);
            Assert.That(ST04EchoValleyFacts.TryResetForNewGame(Write(values)), Is.True);
            Assert.That(ST04EchoValleyFacts.TryRead(read,
                out ST04EchoValleyOutcome? reset), Is.True);
            Assert.That(reset.HasValue, Is.False);
        }
    }
}
