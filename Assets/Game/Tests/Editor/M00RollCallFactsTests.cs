using System;
using System.Collections;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rpg.Tests.Editor
{
    public sealed class M00RollCallFactsTests
    {
        [TestCase(M00RollCallOutcome.AnsweredFirst, 1, "应声第1遍")]
        [TestCase(M00RollCallOutcome.AnsweredSecond, 2, "应声第2遍")]
        [TestCase(M00RollCallOutcome.AnsweredThird, 3, "应声第3遍")]
        [TestCase(M00RollCallOutcome.Silent, 4, "沉默")]
        [TestCase(M00RollCallOutcome.LeftCrowd, 5, "离开人群")]
        public void EncodesStableStoryFactCodesAndAuthoredValues(
            M00RollCallOutcome outcome, int code, string authoredValue)
        {
            Assert.That(M00RollCallFacts.Encode(outcome), Is.EqualTo(code));
            Assert.That(M00RollCallFacts.Decode(code), Is.EqualTo(outcome));
            Assert.That(M00RollCallFacts.IdentityRevealValueFor(outcome),
                Is.EqualTo(authoredValue));
        }

        [Test]
        public void MissingOrUnknownFactDefaultsToSilence()
        {
            Assert.That(M00RollCallFacts.Decode(0), Is.EqualTo(M00RollCallOutcome.Silent));
            Assert.That(M00RollCallFacts.Decode(-1), Is.EqualTo(M00RollCallOutcome.Silent));
            Assert.That(M00RollCallFacts.Decode(99), Is.EqualTo(M00RollCallOutcome.Silent));
            Assert.That(M00RollCallFacts.IdentityRevealValueFor(M00RollCallFacts.Decode(0)),
                Is.EqualTo("沉默"));
        }

        [Test]
        public void PendingOutcomeCannotBeEncodedAsASettledFact()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => M00RollCallFacts.Encode(M00RollCallOutcome.Pending));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => M00RollCallFacts.Encode((M00RollCallOutcome)99));
        }

        [Test]
        public void IncompleteSequenceCannotBePersisted()
        {
            var sequence = new M00RollCallSequence();
            sequence.Begin();

            Assert.Throws<InvalidOperationException>(
                () => M00RollCallFacts.TryRecord(sequence));
            Assert.Throws<ArgumentNullException>(
                () => M00RollCallFacts.TryRecord(null));
        }

        [UnityTest]
        public IEnumerator WritesAndReadsAllFiveSettledOutcomesThroughMakinomVariables()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return null;
            EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Game/Scenes/Bootstrap.unity",
                new LoadSceneParameters(LoadSceneMode.Single));

            float deadline = Time.realtimeSinceStartup + 30f;
            while ((!Maki.Initialized || Maki.Game?.Variables == null) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(Maki.Initialized, Is.True,
                "The Bootstrap scene must initialize Makinom before story facts are written.");
            Assert.That(Maki.Game?.Variables, Is.Not.Null);

            var variables = Maki.Game.Variables;
            int previousValue = variables.GetInt(M00RollCallFacts.IdentityRevealKey);
            try
            {
                foreach (M00RollCallOutcome expected in new[]
                {
                    M00RollCallOutcome.AnsweredFirst,
                    M00RollCallOutcome.AnsweredSecond,
                    M00RollCallOutcome.AnsweredThird,
                    M00RollCallOutcome.Silent,
                    M00RollCallOutcome.LeftCrowd
                })
                {
                    M00RollCallSequence sequence = CompletedSequence(expected);
                    Assert.That(M00RollCallFacts.TryRecord(sequence), Is.True,
                        "Makinom's global variable store should be available in Bootstrap.");
                    Assert.That(variables.GetInt(M00RollCallFacts.IdentityRevealKey),
                        Is.EqualTo(M00RollCallFacts.Encode(expected)));

                    Assert.That(M00RollCallFacts.TryRead(out M00RollCallOutcome actual), Is.True);
                    Assert.That(actual, Is.EqualTo(expected));
                    Assert.That(M00RollCallFacts.IdentityRevealValueFor(actual),
                        Is.EqualTo(sequence.IdentityRevealValue));
                }

                variables.Set(M00RollCallFacts.IdentityRevealKey, 0);
                Assert.That(M00RollCallFacts.TryRead(out M00RollCallOutcome missing), Is.True);
                Assert.That(missing, Is.EqualTo(M00RollCallOutcome.Silent));
            }
            finally
            {
                if (Maki.Initialized && Maki.Game?.Variables != null)
                    Maki.Game.Variables.Set(M00RollCallFacts.IdentityRevealKey, previousValue);
            }

            yield return new ExitPlayMode();
        }

        private static M00RollCallSequence CompletedSequence(M00RollCallOutcome outcome)
        {
            var sequence = new M00RollCallSequence();
            sequence.Begin();

            switch (outcome)
            {
                case M00RollCallOutcome.AnsweredFirst:
                    sequence.Respond(M00RollCallChoice.Answer);
                    break;
                case M00RollCallOutcome.AnsweredSecond:
                    sequence.Respond(M00RollCallChoice.RemainSilent);
                    sequence.AdvanceAfterUnansweredCall();
                    sequence.Respond(M00RollCallChoice.Answer);
                    break;
                case M00RollCallOutcome.AnsweredThird:
                    sequence.Respond(M00RollCallChoice.RemainSilent);
                    sequence.AdvanceAfterUnansweredCall();
                    sequence.Respond(M00RollCallChoice.RemainSilent);
                    sequence.AdvanceAfterUnansweredCall();
                    sequence.Respond(M00RollCallChoice.Answer);
                    break;
                case M00RollCallOutcome.Silent:
                    sequence.Respond(M00RollCallChoice.RemainSilent);
                    sequence.AdvanceAfterUnansweredCall();
                    sequence.Respond(M00RollCallChoice.RemainSilent);
                    sequence.AdvanceAfterUnansweredCall();
                    sequence.Respond(M00RollCallChoice.RemainSilent);
                    break;
                case M00RollCallOutcome.LeftCrowd:
                    sequence.Respond(M00RollCallChoice.LeaveCrowd);
                    sequence.AdvanceAfterUnansweredCall();
                    sequence.AdvanceAfterUnansweredCall();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
            }

            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(sequence.Outcome, Is.EqualTo(outcome));
            return sequence;
        }
    }
}
