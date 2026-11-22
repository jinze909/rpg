using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnStoryLedgerST04Tests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static ST04EchoValleyRecord ValidLongRoadRecord()
        {
            return new ST04EchoValleyRecord(
                ST04ValleyEntry.SurveyEchoesFromRidge,
                ST04ChamberEntry.LureWithBell,
                ST04BeastHandling.LedBackToOldHabitat,
                ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad,
                rescuedSurveyors: 2,
                echoRulesVerified: true,
                companionsMarked: true,
                drainOpened: true,
                longRoadFunded: true,
                alternativeWorksiteReady: false);
        }

        [Test]
        public void SnapshotTracksST04SettlementFailsClosedOnTornFactsAndSurvivesVoyageReset()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var empty), Is.True);
            Assert.That(empty.ST04Outcome, Is.Null);
            Assert.That(empty.SettledRegionalStoryCount, Is.Zero);

            // Torn uncommitted ST-04 intent must fail closed in the ledger snapshot.
            values[ST04EchoValleyFacts.IntentKey] = 123456;
            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var torn), Is.True);
            Assert.That(torn.ST04Outcome, Is.Null);
            Assert.That(torn.SettledRegionalStoryCount, Is.Zero);

            values.Clear();
            Assert.That(ST04EchoValleyFacts.TryRecord(ValidLongRoadRecord(), read, write), Is.True);
            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var settled), Is.True);
            Assert.That(settled.ST04Outcome.HasValue, Is.True);
            Assert.That(settled.ST04Outcome.Value.Story.Resolution,
                Is.EqualTo(ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad));
            Assert.That(settled.SettledRegionalStoryCount, Is.EqualTo(1));

            // Spoofed shipment status without order/delivery evidence must fail closed.
            values[ST04EchoValleyFacts.StoneShipmentKey] = (int)ST04StoneShipment.DeliveredToLinchao;
            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var spoofed), Is.True);
            Assert.That(spoofed.ST04Outcome, Is.Null);
            Assert.That(spoofed.SettledRegionalStoryCount, Is.Zero);

            // Restoring valid persisted state and running downstream voyage reset preserves ST-04.
            values[ST04EchoValleyFacts.StoneShipmentKey] = 0;
            Assert.That(ReturnStoryLedger.TryResetAllDownstreamVoyageFacts(write), Is.True);
            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var afterReset), Is.True);
            Assert.That(afterReset.ST04Outcome.HasValue, Is.True);
            Assert.That(afterReset.ST04Outcome.Value.Story.Resolution,
                Is.EqualTo(ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad));
            Assert.That(afterReset.SettledRegionalStoryCount, Is.EqualTo(1));
        }

        [Test]
        public void StoryLedgerViewDisplaysST04StatusAndSixStoryCounterInChineseAndEnglish()
        {
            var root = new GameObject("LedgerST04ViewRoot");
            string previousLanguage = SoundwellUiLocalization.Language;
            try
            {
                var values = new Dictionary<string, int>();
                Func<string, int> read = Reader(values);
                Action<string, int> write = (k, v) => values[k] = v;

                Assert.That(ST04EchoValleyFacts.TryRecord(ValidLongRoadRecord(), read, write), Is.True);

                using (var host = new ReturnStoryLedgerHost(root.transform, read))
                {
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                    Assert.That(host.TryOpenOrRefresh(), Is.True);
                    var view = host.ActiveView;
                    Assert.That(view.Body.text, Does.Contain("1/6"));

                    view.RegionalTabButton.onClick.Invoke();
                    Assert.That(view.Body.text, Does.Contain("ST-04 钟岭回声谷：已落盘"));

                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                    Assert.That(view.Body.text, Does.Contain("ST-04 Echo Valley: Settled"));

                    view.MainTabButton.onClick.Invoke();
                    Assert.That(view.Body.text, Does.Contain("1/6"));
                }
            }
            finally
            {
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
