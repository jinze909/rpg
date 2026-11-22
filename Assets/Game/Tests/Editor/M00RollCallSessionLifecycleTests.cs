using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M00RollCallSessionLifecycleTests
    {
        private GameObject root;
        private M00RollCallSession session;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("M00RollCallSessionLifecycleRoot");
            session = root.AddComponent<M00RollCallSession>();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void Tick_NonFiniteTime_DoesNotPoisonPauseTimerOrAdvancePrematurely()
        {
            session.Begin(_ => { });
            session.View.GetChoiceButton(1).onClick.Invoke();

            Assert.DoesNotThrow(() => session.Tick(float.NaN));
            Assert.DoesNotThrow(() => session.Tick(float.PositiveInfinity));
            Assert.DoesNotThrow(() => session.Tick(float.NegativeInfinity));
            Assert.That(session.Sequence.CallsMade, Is.EqualTo(1));

            session.Tick(10f);
            session.Tick(11.49f);
            Assert.That(session.Sequence.CallsMade, Is.EqualTo(1));
            session.Tick(11.5f);
            Assert.That(session.Sequence.CallsMade, Is.EqualTo(2));
        }

        [Test]
        public void ExternalViewTeardown_ClearsLiveViewAndPreventsTickOrLayoutExceptions()
        {
            session.Begin(_ => { });
            Assert.That(session.View, Is.Not.Null);

            UnityEngine.Object.DestroyImmediate(session.View.ContinueButton.gameObject);

            Assert.That(session.View, Is.Null);
            Assert.DoesNotThrow(() => session.LayoutForViewportAspect(16f / 9f));
            Assert.DoesNotThrow(() => session.Tick(10f));
        }

        [Test]
        public void ManualViewDispose_ClearsLiveViewAndStopsFurtherCallAdvances()
        {
            session.Begin(_ => { });
            session.View.GetChoiceButton(1).onClick.Invoke();
            session.View.Dispose();

            Assert.That(session.View, Is.Null);
            Assert.DoesNotThrow(() => session.Tick(0f));
            Assert.DoesNotThrow(() => session.Tick(10f));
            Assert.That(session.Sequence.CallsMade, Is.EqualTo(1));
        }
    }
}
