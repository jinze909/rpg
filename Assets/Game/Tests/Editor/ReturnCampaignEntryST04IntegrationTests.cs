using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnCampaignEntryST04IntegrationTests
    {
        private sealed class VerifiedLongRoadWorld : IST04EchoValleyWorldEvidence
        {
            public bool AtValley => true;
            public bool SangJingAndMengHuiPresent => true;
            public bool HasInspected(ST04RoadClue clue) => true;
            public bool HasEnteredVia(ST04ValleyEntry approach) => approach == ST04ValleyEntry.SurveyEchoesFromRidge;
            public int SurveyorsActuallyRescued => 2;
            public bool ClearContinuousEchoAttractsBeasts => true;
            public bool LowVoicesDoNotAttractBeasts => true;
            public bool WetBellLosesBeastTarget => true;
            public bool AncientRainAmplifierIdentified => true;
            public bool QuarryWaterDiversionConfirmed => true;
            public bool HasEnteredChamberVia(ST04ChamberEntry approach) => approach == ST04ChamberEntry.LureWithBell;
            public bool CompanionsMarkedBeforeDrain => true;
            public bool DrainActuallyOpened => true;
            public bool HasResolvedBeastsAs(ST04BeastHandling outcome) => outcome == ST04BeastHandling.LedBackToOldHabitat;
            public bool RoadOutcomeActuallyEstablished(ST04RoadResolution outcome) =>
                outcome == ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad;
            public bool LongRoadActuallyFunded => true;
            public bool AlternativeWorksiteActuallyArranged => false;
        }

        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void CampaignEntryDispatchesST04RecoversFromExternalDisposalAndReflectsInLedgerAndVoyageReset()
        {
            var root = new GameObject("CampaignEntryST04IntegrationRoot");
            string previousLanguage = SoundwellUiLocalization.Language;
            try
            {
                var values = new Dictionary<string, int>();
                Func<string, int> read = Reader(values);
                Action<string, int> write = (k, v) => values[k] = v;

                var regionalContext = new ReturnRegionalStoriesHubContext
                {
                    ST04EchoValleyWorld = () => new VerifiedLongRoadWorld()
                };

                using (var entry = new ReturnCampaignEntryHost(
                    root.transform, read, write, regionalContext: regionalContext))
                {
                    Assert.That(entry.TryOpenRegionalStory(ReturnRegionalStoryId.ST04EchoValley), Is.True);
                    Assert.That(entry.ActiveMode, Is.EqualTo(ReturnCampaignEntryMode.RegionalStory));
                    Assert.That(entry.HasAnyActivePanel, Is.True);
                    Assert.That(entry.CommandHub.RegionalHub.ActiveST04Host, Is.Not.Null);
                    Assert.That(entry.TryOpenStoryLedger(), Is.False);

                    // External view disposal must clear ActiveMode across RegionalHub -> CommandHub -> CampaignEntry.
                    var firstView = entry.CommandHub.RegionalHub.ActiveST04Host.ActiveView;
                    Assert.That(firstView, Is.Not.Null);
                    firstView.Dispose();
                    Assert.That(entry.ActiveMode, Is.EqualTo(ReturnCampaignEntryMode.None));
                    Assert.That(entry.HasAnyActivePanel, Is.False);

                    // Clean reopen and full UGUI settlement through ReturnCampaignEntryHost.
                    Assert.That(entry.TryOpenRegionalStory(ReturnRegionalStoryId.ST04EchoValley), Is.True);
                    var view = entry.CommandHub.RegionalHub.ActiveST04Host.ActiveView;
                    Assert.That(view, Is.Not.Null);

                    view.FirstOption.onClick.Invoke();
                    view.SecondOption.onClick.Invoke();
                    view.ThirdOption.onClick.Invoke();
                    view.SecondOption.onClick.Invoke(); // SurveyEchoesFromRidge
                    view.FirstOption.onClick.Invoke();  // Rescue 2 surveyors
                    view.FirstOption.onClick.Invoke();  // Verify sound & rain mechanism
                    view.SecondOption.onClick.Invoke(); // LureWithBell
                    view.FirstOption.onClick.Invoke();  // Mark allies & open drain
                    view.SecondOption.onClick.Invoke(); // LedBackToOldHabitat
                    view.SecondOption.onClick.Invoke(); // ReturnBeastsAndUseCostlyLongRoad

                    Assert.That(entry.ActiveMode, Is.EqualTo(ReturnCampaignEntryMode.None));
                    Assert.That(entry.TryBuildProgressSnapshot(out var snapshot), Is.True);
                    Assert.That(snapshot.ST04Outcome.HasValue, Is.True);
                    Assert.That(snapshot.ST04Outcome.Value.Story.Resolution,
                        Is.EqualTo(ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad));
                    Assert.That(snapshot.SettledRegionalStoryCount, Is.EqualTo(1));

                    // Story Ledger opened via CampaignEntry must display ST-04 settled and 1/6 counter.
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                    Assert.That(entry.TryOpenStoryLedger(), Is.True);
                    Assert.That(entry.ActiveMode, Is.EqualTo(ReturnCampaignEntryMode.StoryLedger));
                    var ledgerView = entry.CommandHub.LedgerHost.ActiveView;
                    Assert.That(ledgerView.Body.text, Does.Contain("1/6"));

                    ledgerView.RegionalTabButton.onClick.Invoke();
                    Assert.That(ledgerView.Body.text, Does.Contain("ST-04 钟岭回声谷：已落盘"));

                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                    Assert.That(ledgerView.Body.text, Does.Contain("ST-04 Echo Valley: Settled"));

                    // New voyage reset closes active ledger and preserves permanent ST-04 settlement.
                    Assert.That(entry.TryStartNewVoyageReset(), Is.True);
                    Assert.That(entry.ActiveMode, Is.EqualTo(ReturnCampaignEntryMode.None));
                    Assert.That(entry.TryBuildProgressSnapshot(out var afterReset), Is.True);
                    Assert.That(afterReset.ST04Outcome.HasValue, Is.True);
                    Assert.That(afterReset.SettledRegionalStoryCount, Is.EqualTo(1));
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
