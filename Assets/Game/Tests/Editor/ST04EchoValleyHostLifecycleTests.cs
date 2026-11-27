using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST04EchoValleyHostLifecycleTests
    {
        private sealed class FakeEvidence : IST04EchoValleyWorldEvidence
        {
            public bool AtValley => true;
            public bool SangJingAndMengHuiPresent => true;
            public bool HasInspected(ST04RoadClue clue) => true;
            public bool HasEnteredVia(ST04ValleyEntry approach) => true;
            public int SurveyorsActuallyRescued => 2;
            public bool ClearContinuousEchoAttractsBeasts => true;
            public bool LowVoicesDoNotAttractBeasts => true;
            public bool WetBellLosesBeastTarget => true;
            public bool AncientRainAmplifierIdentified => true;
            public bool QuarryWaterDiversionConfirmed => true;
            public bool HasEnteredChamberVia(ST04ChamberEntry approach) => true;
            public bool CompanionsMarkedBeforeDrain => true;
            public bool DrainActuallyOpened => true;
            public bool AllowFundedRidgeOutcome { get; set; }
            public bool HasResolvedBeastsAs(ST04BeastHandling outcome) =>
                AllowFundedRidgeOutcome && outcome == ST04BeastHandling.LedBackToOldHabitat;
            public bool RoadOutcomeActuallyEstablished(ST04RoadResolution outcome) =>
                AllowFundedRidgeOutcome && outcome == ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad;
            public bool LongRoadActuallyFunded => AllowFundedRidgeOutcome;
            public bool AlternativeWorksiteActuallyArranged => false;
        }

        private static Func<string, int> Read(Dictionary<string, int> values) =>
            key => values.TryGetValue(key, out int result) ? result : 0;

        private static Action<string, int> Write(Dictionary<string, int> values) =>
            (key, value) => values[key] = value;

        private static void AssertLockedClicksDoNotAdvance(
            ST04EchoValleyView view, CanvasGroup group, Dictionary<string, int> values)
        {
            ST04EchoValleyPhase before = view.Session.Phase;
            group.interactable = false;
            view.FirstOption.onClick.Invoke();
            view.SecondOption.onClick.Invoke();
            view.ThirdOption.onClick.Invoke();
            Assert.That(view.Session.Phase, Is.EqualTo(before));
            Assert.That(values, Is.Empty);
            group.interactable = true;

            view.Panel.gameObject.SetActive(false);
            view.FirstOption.onClick.Invoke();
            view.SecondOption.onClick.Invoke();
            view.ThirdOption.onClick.Invoke();
            Assert.That(view.Session.Phase, Is.EqualTo(before));
            Assert.That(values, Is.Empty);
            view.Panel.gameObject.SetActive(true);
        }

        [Test]
        public void EffectiveInputLockProtectsValleyEvidenceAndFundedRoadUntilExplicitConfirmation()
        {
            var root = new GameObject("ST04 effective input lock");
            var values = new Dictionary<string, int>();
            ST04EchoValleyHost host = null;
            try
            {
                var world = new FakeEvidence { AllowFundedRidgeOutcome = true };
                host = new ST04EchoValleyHost(root.transform, world, Read(values), Write(values));
                Assert.That(host.TryOpen(), Is.True);
                ST04EchoValleyView view = host.ActiveView;
                var group = root.AddComponent<CanvasGroup>();

                AssertLockedClicksDoNotAdvance(view, group, values);
                view.FirstOption.onClick.Invoke();
                view.SecondOption.onClick.Invoke();
                view.ThirdOption.onClick.Invoke();
                Assert.That(view.Session.Phase, Is.EqualTo(ST04EchoValleyPhase.ChooseValleyEntry));

                AssertLockedClicksDoNotAdvance(view, group, values);
                view.SecondOption.onClick.Invoke();
                Assert.That(view.Session.Phase, Is.EqualTo(ST04EchoValleyPhase.RescueSurveyors));

                AssertLockedClicksDoNotAdvance(view, group, values);
                Assert.That(view.Session.SurveyorsRescued, Is.Zero);
                view.FirstOption.onClick.Invoke();
                Assert.That(view.Session.SurveyorsRescued, Is.EqualTo(2));
                Assert.That(view.Session.Phase, Is.EqualTo(ST04EchoValleyPhase.VerifyEchoBehavior));

                AssertLockedClicksDoNotAdvance(view, group, values);
                view.FirstOption.onClick.Invoke();
                Assert.That(view.Session.Phase, Is.EqualTo(ST04EchoValleyPhase.EnterInnerChamber));

                AssertLockedClicksDoNotAdvance(view, group, values);
                view.ThirdOption.onClick.Invoke();
                Assert.That(view.Session.Phase, Is.EqualTo(ST04EchoValleyPhase.MarkAlliesAndOpenDrain));

                AssertLockedClicksDoNotAdvance(view, group, values);
                view.FirstOption.onClick.Invoke();
                Assert.That(view.Session.Phase, Is.EqualTo(ST04EchoValleyPhase.HandleBeasts));

                AssertLockedClicksDoNotAdvance(view, group, values);
                view.FirstOption.onClick.Invoke();
                Assert.That(view.Session.Phase, Is.EqualTo(ST04EchoValleyPhase.HandleBeasts));
                view.SecondOption.onClick.Invoke();
                Assert.That(view.Session.Phase, Is.EqualTo(ST04EchoValleyPhase.DecideRoadAndRainWall));

                AssertLockedClicksDoNotAdvance(view, group, values);
                Assert.That(host.IsCommitted, Is.False);
                Assert.That(view.SecondOption.IsInteractable(), Is.True);
                view.SecondOption.onClick.Invoke();
                Assert.That(host.IsCommitted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(ST04EchoValleyFacts.TryRead(Read(values),
                    out ST04EchoValleyOutcome? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.Story.Entry, Is.EqualTo(ST04ValleyEntry.SurveyEchoesFromRidge));
                Assert.That(saved.Value.Story.Chamber, Is.EqualTo(ST04ChamberEntry.OpenVentGrate));
                Assert.That(saved.Value.Story.Resolution,
                    Is.EqualTo(ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad));
                Assert.That(saved.Value.Story.RescuedSurveyors, Is.EqualTo(2));
                Assert.That(saved.Value.Story.UsesFundedLongRoad, Is.True);
                Assert.That(saved.Value.ShowZhonglingStoneOnM15Monument, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExternalViewDisposalClearsActiveViewAndAllowsCleanReopen()
        {
            var root = new GameObject("ST04 external view dispose");
            var values = new Dictionary<string, int>();
            ST04EchoValleyHost host = null;
            try
            {
                host = new ST04EchoValleyHost(
                    root.transform, new FakeEvidence(), Read(values), Write(values));
                Assert.That(host.TryOpen(), Is.True);
                ST04EchoValleyView firstView = host.ActiveView;
                Assert.That(firstView, Is.Not.Null);

                firstView.Dispose();
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.DoesNotThrow(() => host.RefreshView());

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.ActiveView, Is.Not.SameAs(firstView));
                Assert.That(host.ActiveView.IsDisposed, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExternalCanvasDestructionMarksViewDisposedAndAllowsHostReopen()
        {
            var root = new GameObject("ST04 external canvas destroy");
            var values = new Dictionary<string, int>();
            ST04EchoValleyHost host = null;
            try
            {
                host = new ST04EchoValleyHost(
                    root.transform, new FakeEvidence(), Read(values), Write(values));
                Assert.That(host.TryOpen(), Is.True);
                ST04EchoValleyView firstView = host.ActiveView;
                GameObject canvasObject = firstView.Panel.parent.gameObject;

                UnityEngine.Object.DestroyImmediate(canvasObject);
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.DoesNotThrow(() => host.RefreshView());

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.ActiveView.IsDisposed, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposingHostDuringFactReadDoesNotPublishValleySessionOrCanvas()
        {
            var root = new GameObject("ST04 retired during fact read");
            ST04EchoValleyHost host = null;
            try
            {
                host = new ST04EchoValleyHost(root.transform, new FakeEvidence(), key =>
                {
                    if (key == ST04EchoValleyFacts.IntentKey) host.Dispose();
                    return 0;
                }, (_, __) => Assert.Fail("Opening must not write facts"));

                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.Session, Is.Null);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(host.TryOpen(), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ParentDestroyedDuringFactReadDoesNotPublishValleySession()
        {
            var root = new GameObject("ST04 parent retired during fact read");
            ST04EchoValleyHost host = null;
            try
            {
                host = new ST04EchoValleyHost(root.transform, new FakeEvidence(), key =>
                {
                    if (key == ST04EchoValleyFacts.IntentKey && root != null)
                        UnityEngine.Object.DestroyImmediate(root);
                    return 0;
                }, (_, __) => Assert.Fail("Opening must not write facts"));

                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.Session, Is.Null);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.TryOpen(), Is.False);
            }
            finally
            {
                host?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NestedFactReadOpenCannotPublishSecondValleyCanvas()
        {
            var root = new GameObject("ST04 nested fact read");
            ST04EchoValleyHost host = null;
            bool attempted = false;
            try
            {
                host = new ST04EchoValleyHost(root.transform, new FakeEvidence(), key =>
                {
                    if (!attempted)
                    {
                        attempted = true;
                        Assert.That(host.TryOpen(), Is.False);
                    }
                    return 0;
                }, (_, __) => Assert.Fail("Opening must not write facts"));

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(attempted, Is.True);
                Assert.That(host.ActiveView.Session, Is.SameAs(host.Session));
                Assert.That(root.transform.childCount, Is.EqualTo(1));
                var first = host.ActiveView;
                first.Dispose();
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView, Is.Not.SameAs(first));
                Assert.That(root.transform.childCount, Is.EqualTo(1));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedParentFailsClosedOnTryOpenWithoutThrowing()
        {
            var root = new GameObject("ST04 destroyed parent");
            var values = new Dictionary<string, int>();
            ST04EchoValleyHost host = new ST04EchoValleyHost(
                root.transform, new FakeEvidence(), Read(values), Write(values));
            try
            {
                UnityEngine.Object.DestroyImmediate(root);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host.Dispose();
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LayoutForViewportAspectIgnoresNonFiniteAndNonPositiveAspects()
        {
            var root = new GameObject("ST04 invalid aspect");
            var values = new Dictionary<string, int>();
            ST04EchoValleyHost host = null;
            try
            {
                host = new ST04EchoValleyHost(
                    root.transform, new FakeEvidence(), Read(values), Write(values));
                Assert.That(host.TryOpen(), Is.True);
                Vector3 initialScale = host.ActiveView.Panel.localScale;

                foreach (float aspect in new[]
                    { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f, -1.5f })
                {
                    Assert.DoesNotThrow(() => host.ActiveView.LayoutForViewportAspect(aspect));
                    Assert.That(host.ActiveView.Panel.localScale, Is.EqualTo(initialScale));
                    Assert.That(host.ActiveView.IsDisposed, Is.False);
                }
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
