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
            public bool HasResolvedBeastsAs(ST04BeastHandling outcome) => false;
            public bool RoadOutcomeActuallyEstablished(ST04RoadResolution outcome) => false;
            public bool LongRoadActuallyFunded => false;
            public bool AlternativeWorksiteActuallyArranged => false;
        }

        private static Func<string, int> Read(Dictionary<string, int> values) =>
            key => values.TryGetValue(key, out int result) ? result : 0;

        private static Action<string, int> Write(Dictionary<string, int> values) =>
            (key, value) => values[key] = value;

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
