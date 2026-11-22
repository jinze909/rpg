using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnStoryLedgerST01Tests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void SnapshotTracksST01TheaterSettlementWithoutCountingDepartureOnlyOrTornFacts()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var empty), Is.True);
            Assert.That(empty.ST01Outcome, Is.Null);
            Assert.That(empty.SettledRegionalStoryCount, Is.Zero);

            // White Reed departure alone is optional history and does not settle ST-01 theater.
            Assert.That(ST01RegionalStoryFacts.TryRecordDeparture(
                ST01DepartureChoice.TalkTogether, read, write), Is.True);
            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var departureOnly), Is.True);
            Assert.That(departureOnly.ST01Outcome.HasValue, Is.True);
            Assert.That(departureOnly.ST01Outcome.Value.Departure,
                Is.EqualTo(ST01DepartureChoice.TalkTogether));
            Assert.That(departureOnly.ST01Outcome.Value.Theater, Is.Null);
            Assert.That(departureOnly.SettledRegionalStoryCount, Is.Zero);

            // Torn unconfirmed theater choice must fail closed in the ledger snapshot.
            values[ST01RegionalStoryFacts.TheaterChoiceKey] =
                (int)ST01TheaterChoice.RewritePlayWithFundingRisk;
            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var torn), Is.True);
            Assert.That(torn.ST01Outcome, Is.Null);
            Assert.That(torn.SettledRegionalStoryCount, Is.Zero);

            // Confirming the pending ST-01 theater choice settles ST-01 and increments the regional count.
            Assert.That(ST01RegionalStoryFacts.TryRecordTheater(
                ST01TheaterChoice.RewritePlayWithFundingRisk, read, write), Is.True);
            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var settled), Is.True);
            Assert.That(settled.ST01Outcome.HasValue, Is.True);
            Assert.That(settled.ST01Outcome.Value.Theater,
                Is.EqualTo(ST01TheaterChoice.RewritePlayWithFundingRisk));
            Assert.That(settled.SettledRegionalStoryCount, Is.EqualTo(1));

            // Downstream voyage reset must preserve permanent regional story ST-01.
            Assert.That(ReturnStoryLedger.TryResetAllDownstreamVoyageFacts(write), Is.True);
            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var afterReset), Is.True);
            Assert.That(afterReset.ST01Outcome.HasValue, Is.True);
            Assert.That(afterReset.ST01Outcome.Value.Theater,
                Is.EqualTo(ST01TheaterChoice.RewritePlayWithFundingRisk));
            Assert.That(afterReset.SettledRegionalStoryCount, Is.EqualTo(1));
        }

        [Test]
        public void StoryLedgerViewDisplaysST01StatusAndFiveStoryCounterInChineseAndEnglish()
        {
            var root = new GameObject("LedgerST01ViewRoot");
            string previousLanguage = SoundwellUiLocalization.Language;
            try
            {
                var values = new Dictionary<string, int>();
                Func<string, int> read = Reader(values);
                Action<string, int> write = (k, v) => values[k] = v;

                Assert.That(ST01RegionalStoryFacts.TryRecordTheater(
                    ST01TheaterChoice.PublicBoycott, read, write), Is.True);

                using (var host = new ReturnStoryLedgerHost(root.transform, read))
                {
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                    Assert.That(host.TryOpenOrRefresh(), Is.True);
                    var view = host.ActiveView;
                    Assert.That(view.Body.text, Does.Contain("1/5"));

                    view.RegionalTabButton.onClick.Invoke();
                    Assert.That(view.Body.text, Does.Contain("ST-01 镜湾戏院：已落盘"));

                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                    Assert.That(view.Body.text, Does.Contain("ST-01 Jingwan Theater: Settled"));

                    view.MainTabButton.onClick.Invoke();
                    Assert.That(view.Body.text, Does.Contain("1/5"));
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
