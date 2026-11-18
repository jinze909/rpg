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
        public void CannotStartOverOrBeginWithoutCompletionCallback()
        {
            Assert.Throws<ArgumentNullException>(() => session.Begin(null));
            session.Begin(_ => { });
            Assert.Throws<InvalidOperationException>(() => session.Begin(_ => { }));
        }
    }
}
