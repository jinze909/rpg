using System;
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
