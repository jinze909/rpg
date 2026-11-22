using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST04EchoValleyHostTests
    {
        private sealed class FakeEvidence : IST04EchoValleyWorldEvidence
        {
            public bool AtValley { get; set; } = true;
            public bool SangJingAndMengHuiPresent { get; set; } = true;
            public bool Clues { get; set; } = true;
            public bool Entry { get; set; } = true;
            public int SurveyorsActuallyRescued { get; set; } = 2;
            public bool SoundProof { get; set; } = true;
            public bool Chamber { get; set; } = true;
            public bool CompanionsMarkedBeforeDrain { get; set; } = true;
            public bool DrainActuallyOpened { get; set; } = true;
            public ST04BeastHandling? BeastOutcome { get; set; }
            public ST04RoadResolution? RoadOutcome { get; set; }
            public bool LongRoadActuallyFunded { get; set; }
            public bool AlternativeWorksiteActuallyArranged { get; set; }

            public bool HasInspected(ST04RoadClue clue) => Clues;
            public bool HasEnteredVia(ST04ValleyEntry approach) => Entry;
            public bool ClearContinuousEchoAttractsBeasts => SoundProof;
            public bool LowVoicesDoNotAttractBeasts => SoundProof;
            public bool WetBellLosesBeastTarget => SoundProof;
            public bool AncientRainAmplifierIdentified => SoundProof;
            public bool QuarryWaterDiversionConfirmed => SoundProof;
            public bool HasEnteredChamberVia(ST04ChamberEntry approach) => Chamber;
            public bool HasResolvedBeastsAs(ST04BeastHandling result) =>
                BeastOutcome == result;
            public bool RoadOutcomeActuallyEstablished(ST04RoadResolution result) =>
                RoadOutcome == result;
        }

        private static Func<string, int> Read(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int result) ? result : 0;
        }

        private static Action<string, int> Write(Dictionary<string, int> values)
        {
            return (key, value) => values[key] = value;
        }

        private static void ReachDecision(ST04EchoValleyHost host,
            FakeEvidence world, ST04BeastHandling beast,
            ST04ValleyEntry entry = ST04ValleyEntry.EnterQuietly)
        {
            var view = host.ActiveView;
            view.FirstOption.onClick.Invoke();
            view.SecondOption.onClick.Invoke();
            view.ThirdOption.onClick.Invoke();
            Assert.That(host.Session.Phase,
                Is.EqualTo(ST04EchoValleyPhase.ChooseValleyEntry));
            (entry == ST04ValleyEntry.EnterQuietly
                ? view.FirstOption : entry == ST04ValleyEntry.SurveyEchoesFromRidge
                    ? view.SecondOption : view.ThirdOption).onClick.Invoke();
            Assert.That(host.Session.Phase,
                Is.EqualTo(ST04EchoValleyPhase.RescueSurveyors));
            view.FirstOption.onClick.Invoke();
            Assert.That(host.Session.SurveyorsRescued, Is.EqualTo(2));
            view.FirstOption.onClick.Invoke();
            Assert.That(host.Session.Phase,
                Is.EqualTo(ST04EchoValleyPhase.EnterInnerChamber));
            view.SecondOption.onClick.Invoke();
            Assert.That(host.Session.Phase,
                Is.EqualTo(ST04EchoValleyPhase.MarkAlliesAndOpenDrain));
            view.FirstOption.onClick.Invoke();
            world.BeastOutcome = beast;
            view.Refresh();
            (beast == ST04BeastHandling.Slain
                ? view.FirstOption
                : beast == ST04BeastHandling.LedBackToOldHabitat
                    ? view.SecondOption : view.ThirdOption).onClick.Invoke();
            Assert.That(host.Session.Phase,
                Is.EqualTo(ST04EchoValleyPhase.DecideRoadAndRainWall));
        }

        [Test]
        public void WorldAndActorEvidenceAreRequiredAndUiDoesNotCreateRescues()
        {
            var root = new GameObject("ST04 world evidence");
            var values = new Dictionary<string, int>();
            var world = new FakeEvidence
            {
                AtValley = false, Clues = false, SurveyorsActuallyRescued = 0
            };
            ST04EchoValleyHost host = null;
            try
            {
                host = new ST04EchoValleyHost(
                    root.transform, world, Read(values), Write(values));
                Assert.That(host.TryOpen(), Is.False);
                world.AtValley = true;
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView.FirstOption.interactable, Is.False);
                host.ActiveView.FirstOption.onClick.Invoke();
                Assert.That(host.Session.Phase,
                    Is.EqualTo(ST04EchoValleyPhase.InspectEmptyMuleCart));
                world.Clues = true;
                host.RefreshView();
                host.ActiveView.FirstOption.onClick.Invoke();
                host.ActiveView.SecondOption.onClick.Invoke();
                host.ActiveView.ThirdOption.onClick.Invoke();
                host.ActiveView.FirstOption.onClick.Invoke();
                Assert.That(host.Session.Phase,
                    Is.EqualTo(ST04EchoValleyPhase.RescueSurveyors));
                host.ActiveView.FirstOption.onClick.Invoke();
                Assert.That(host.Session.Phase,
                    Is.EqualTo(ST04EchoValleyPhase.RescueSurveyors));
                world.SurveyorsActuallyRescued = 2;
                host.RefreshView();
                host.ActiveView.FirstOption.onClick.Invoke();
                Assert.That(host.Session.Phase,
                    Is.EqualTo(ST04EchoValleyPhase.VerifyEchoBehavior));
                world.SangJingAndMengHuiPresent = false;
                host.RefreshView();
                Assert.That(host.ActiveView.FirstOption.interactable, Is.False);
                Assert.That(host.IsCommitted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FullActualRainWallPreservationCannotInventUnfundedStoneShipment()
        {
            var root = new GameObject("ST04 preserve rain wall");
            var values = new Dictionary<string, int>();
            var world = new FakeEvidence();
            ST04EchoValleyHost host = null;
            try
            {
                host = new ST04EchoValleyHost(
                    root.transform, world, Read(values), Write(values));
                Assert.That(host.TryOpen(), Is.True);
                ReachDecision(host, world,
                    ST04BeastHandling.ContainedAwayFromClosedRoad);
                world.RoadOutcome = ST04RoadResolution.PreserveRainWallAndCloseRoad;
                host.RefreshView();
                Assert.That(host.ActiveView.ThirdOption.interactable, Is.False,
                    "Workers must have a real replacement worksite.");
                world.AlternativeWorksiteActuallyArranged = true;
                host.RefreshView();
                Assert.That(host.ActiveView.ThirdOption.interactable, Is.True);
                host.ActiveView.ThirdOption.onClick.Invoke();
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.IsCommitted, Is.True);
                Assert.That(ST04EchoValleyFacts.TryRead(Read(values),
                    out ST04EchoValleyOutcome? committed), Is.True);
                Assert.That(committed.Value.Story.OriginalTransportRoadPermanentlyClosed,
                    Is.True);
                Assert.That(committed.Value.ShowZhonglingStoneOnM15Monument, Is.False);
                Assert.That(ST04EchoValleyFacts.TryMarkStoneOrderAccepted(
                    Read(values), Write(values)), Is.False);
                Assert.That(host.TryOpen(), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedCommitRemainsInSameDecisionUntilExactRetrySucceeds()
        {
            var root = new GameObject("ST04 torn write");
            var values = new Dictionary<string, int>();
            var world = new FakeEvidence();
            bool interrupted = true;
            Action<string, int> unstableWrite = (key, value) =>
            {
                values[key] = value;
                if (key == ST04EchoValleyFacts.IntentKey && interrupted)
                {
                    interrupted = false;
                    throw new InvalidOperationException("Lost ack after intent");
                }
            };
            ST04EchoValleyHost host = null;
            try
            {
                host = new ST04EchoValleyHost(
                    root.transform, world, Read(values), unstableWrite);
                Assert.That(host.TryOpen(), Is.True);
                ReachDecision(host, world, ST04BeastHandling.Slain);
                world.RoadOutcome = ST04RoadResolution.KillAdultsAndCloseWell;
                host.RefreshView();
                Assert.Throws<InvalidOperationException>(() =>
                    host.ActiveView.FirstOption.onClick.Invoke());
                Assert.That(host.IsCommitted, Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.ActiveView.SecondOption.interactable, Is.False);
                host.ActiveView.FirstOption.onClick.Invoke();
                Assert.That(host.IsCommitted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(ST04EchoValleyFacts.TryRead(Read(values),
                    out ST04EchoValleyOutcome? result), Is.True);
                Assert.That(result.Value.Story.RainWallPartlyCollapsed, Is.True);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LanguageAspectFocusAndDisposedClickDoNotMutateWorld()
        {
            var root = new GameObject("ST04 locale");
            var values = new Dictionary<string, int>();
            var world = new FakeEvidence();
            string original = SoundwellUiLocalization.Language;
            ST04EchoValleyHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                host = new ST04EchoValleyHost(
                    root.transform, world, Read(values), Write(values));
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView.Heading.text, Does.Contain("ECHO VALLEY"));
                Assert.That(host.ActiveView.Panel.anchorMin.y, Is.EqualTo(0f));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(host.ActiveView.Heading.text, Does.Contain("钟岭"));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() =>
                        host.ActiveView.LayoutForViewportAspect(aspect));
                var oldButton = host.ActiveView.FirstOption;
                host.Dispose();
                oldButton.onClick.Invoke();
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.IsCommitted, Is.False);
            }
            finally
            {
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(original);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
