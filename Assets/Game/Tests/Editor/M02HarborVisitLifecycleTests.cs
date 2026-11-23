using System;
using System.Reflection;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M02HarborVisitLifecycleTests
    {
        private GameObject root;
        private M02HarborVisit visit;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("M02HarborVisitLifecycleRoot");
        }

        [TearDown]
        public void TearDown()
        {
            visit?.Dispose();
            visit = null;
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void Constructor_UndefinedPriorChoice_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new M02HarborVisit(root.transform, (M01PensionChoice)999, () => { }));
        }

        [Test]
        public void ExternalParentTeardown_SetsIsDisposedImmediately_AndTickDisposesWithoutCallback()
        {
            int finished = 0;
            visit = new M02HarborVisit(root.transform,
                M01PensionChoice.RegisterLivingIdentity, () => finished++);
            Assert.That(visit.IsDisposed, Is.False);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.That(visit.IsDisposed, Is.True);
            Assert.DoesNotThrow(() => visit.Tick(16f / 9f));
            Assert.That(finished, Is.Zero);
        }

        [Test]
        public void FailedFinalHandoffKeepsReviewPendingUntilSuccessfulRetry()
        {
            int attempts = 0;
            visit = new M02HarborVisit(root.transform,
                M01PensionChoice.LeaveWithoutDecision, () =>
                {
                    attempts++;
                    if (attempts == 1)
                        throw new InvalidOperationException("Simulated scene handoff failure");
                });

            var review = new M02MissingReturnReview(
                M01PensionChoice.LeaveWithoutDecision, M02BridgeEvidence.None, false);
            review.Begin();
            M02MissingReturnOutcome outcome = review.Leave();

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo pending = typeof(M02HarborVisit).GetField("reviewOutcome", flags);
            MethodInfo complete = typeof(M02HarborVisit).GetMethod("CompleteReview", flags);
            Assert.That(pending, Is.Not.Null);
            Assert.That(complete, Is.Not.Null);
            pending.SetValue(visit, outcome);

            TargetInvocationException error =
                Assert.Throws<TargetInvocationException>(() => complete.Invoke(visit, null));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(pending.GetValue(visit), Is.SameAs(outcome));
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(visit.IsDisposed, Is.False);

            Assert.DoesNotThrow(() => complete.Invoke(visit, null));
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(pending.GetValue(visit), Is.Null);
            Assert.DoesNotThrow(() => complete.Invoke(visit, null));
            Assert.That(attempts, Is.EqualTo(2), "A completed visit must not deliver twice.");
        }

        [Test]
        public void Dispose_IsIdempotent_AndIgnoresSubsequentTicks()
        {
            int finished = 0;
            visit = new M02HarborVisit(root.transform,
                M01PensionChoice.LeaveWithoutDecision, () => finished++);

            Assert.DoesNotThrow(() => visit.Dispose());
            Assert.DoesNotThrow(() => visit.Dispose());
            Assert.That(visit.IsDisposed, Is.True);
            Assert.DoesNotThrow(() => visit.Tick(16f / 9f));
            Assert.That(finished, Is.Zero);
        }
    }
}
