using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M00RollCallSessionTests
    {
        private GameObject root;
        private M00RollCallSession session;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("M00 roll call session test");
            session = root.AddComponent<M00RollCallSession>();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void SilenceRequiresAnIndependentPauseBetweenEachCall()
        {
            session.Begin(_ => { });
            session.View.GetChoiceButton(1).onClick.Invoke();

            session.Tick(10f);
            session.Tick(11.49f);
            Assert.That(session.Sequence.CallsMade, Is.EqualTo(1));
            session.Tick(11.5f);
            Assert.That(session.Sequence.CallsMade, Is.EqualTo(2));

            session.View.GetChoiceButton(1).onClick.Invoke();
            session.Tick(12f);
            session.Tick(13.49f);
            Assert.That(session.Sequence.CallsMade, Is.EqualTo(2));
            session.Tick(13.5f);
            Assert.That(session.Sequence.CallsMade, Is.EqualTo(3));

            session.View.GetChoiceButton(0).onClick.Invoke();
            session.Tick(50f);
            Assert.That(session.Sequence.Outcome, Is.EqualTo(M00RollCallOutcome.AnsweredThird));
            Assert.That(session.Sequence.CallsMade, Is.EqualTo(3));
            Assert.That(session.View.ContinueButton.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void LeavingCrowdRetainsAllThreeSpokenCallsButNoFurtherChoices()
        {
            session.Begin(_ => { });
            session.View.GetChoiceButton(2).onClick.Invoke();
            session.Tick(0f);
            session.Tick(1.5f);

            Assert.That(session.Sequence.CallsMade, Is.EqualTo(2));
            Assert.That(session.Sequence.HasLeftCrowd, Is.True);
            Assert.That(session.View.GetChoiceButton(0).gameObject.activeSelf, Is.False);

            session.Tick(1.5f);
            session.Tick(3f);
            Assert.That(session.Sequence.CallsMade, Is.EqualTo(3));
            Assert.That(session.Sequence.IsComplete, Is.True);
            Assert.That(session.Sequence.Outcome, Is.EqualTo(M00RollCallOutcome.LeftCrowd));
            Assert.That(session.View.ContinueButton.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void AnsweringFirstCallCannotBeFollowedBySpuriousCalls()
        {
            session.Begin(_ => { });
            session.View.GetChoiceButton(0).onClick.Invoke();

            session.Tick(0f);
            session.Tick(1000f);
            Assert.That(session.Sequence.CallsMade, Is.EqualTo(1));
            Assert.That(session.Sequence.Outcome, Is.EqualTo(M00RollCallOutcome.AnsweredFirst));
            Assert.That(session.AwaitingPersistence, Is.False,
                "The ritual only requests persistence when the player continues.");
        }

        [Test]
        public void DestroyedOwnerDoesNotLeaveDeadTextSubscribedToLocaleChanges()
        {
            string previousLanguage = SoundwellUiLocalization.Language;
            session.Begin(_ => { });
            // Simulate a scene unload without calling the view's Dispose.
            // A stale locale delegate previously tried to mutate destroyed Text.
            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            try
            {
                string nextLanguage = previousLanguage == SoundwellUiLocalization.Chinese
                    ? SoundwellUiLocalization.English
                    : SoundwellUiLocalization.Chinese;
                Assert.DoesNotThrow(() => SoundwellUiLocalization.SetLanguage(nextLanguage));
            }
            finally
            {
                SoundwellUiLocalization.SetLanguage(previousLanguage);
            }
        }

        [Test]
        public void CannotStartOverOrBeginWithoutCompletionCallback()
        {
            Assert.Throws<ArgumentNullException>(() => session.Begin(null));
            session.Begin(_ => { });
            Assert.Throws<InvalidOperationException>(() => session.Begin(_ => { }));
        }

        [Test]
        public void ViewportAspectChangesRescaleActiveViewAndIgnoreInvalidValues()
        {
            Assert.DoesNotThrow(() => session.LayoutForViewportAspect(9f / 16f));

            session.Begin(_ => { });
            RectTransform authored = session.View.ContinueButton.transform.parent as RectTransform;
            Assert.That(authored, Is.Not.Null);

            session.LayoutForViewportAspect(9f / 16f);
            Vector3 portraitScale = authored.localScale;
            Assert.That(portraitScale.x, Is.LessThan(1f));

            session.LayoutForViewportAspect(float.NaN);
            session.LayoutForViewportAspect(float.PositiveInfinity);
            session.LayoutForViewportAspect(0f);
            Assert.That(authored.localScale, Is.EqualTo(portraitScale));

            session.LayoutForViewportAspect(16f / 9f);
            Assert.That(authored.localScale.x, Is.GreaterThan(portraitScale.x));

            session.View.Dispose();
            Assert.DoesNotThrow(() => session.LayoutForViewportAspect(9f / 16f));
            Assert.DoesNotThrow(() => session.View.LayoutForViewportAspect(32f / 9f),
                "A stale direct resize callback after scene handoff must also be inert.");
        }    
    }
}
