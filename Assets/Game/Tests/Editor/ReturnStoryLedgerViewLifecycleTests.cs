using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnStoryLedgerViewLifecycleTests
    {
        private static ReturnStoryProgressSnapshot EmptySnapshot()
        {
            var values = new Dictionary<string, int>();
            Assert.That(ReturnStoryLedger.TryBuildSnapshot(
                k => values.TryGetValue(k, out int v) ? v : 0,
                out ReturnStoryProgressSnapshot snapshot), Is.True);
            return snapshot;
        }

        [Test]
        public void PartialChildTeardownDisposesViewAndBlocksTabClickLayoutAndLocaleRefresh()
        {
            var root = new GameObject("Ledger view partial teardown tab");
            string previousLanguage = SoundwellUiLocalization.Language;
            ReturnStoryLedgerView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                int closedCalls = 0;
                view = new ReturnStoryLedgerView(root.transform, EmptySnapshot(), () => closedCalls++);

                var rosterTab = view.RosterTabButton;
                UnityEngine.Object.DestroyImmediate(view.Heading.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => rosterTab.onClick.Invoke());
                Assert.That(view.ActiveTab, Is.EqualTo(ReturnStoryLedgerTab.MainStory));
                Assert.That(closedCalls, Is.EqualTo(0));
                Assert.That(view.Panel == null, Is.True);

                Assert.DoesNotThrow(() => view.UpdateSnapshot(EmptySnapshot()));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
                Assert.DoesNotThrow(() => SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PartialChildTeardownOnCloseClickDisposesViewWithoutInvokingOnClosed()
        {
            var root = new GameObject("Ledger view partial teardown close");
            ReturnStoryLedgerView view = null;
            try
            {
                int closedCalls = 0;
                view = new ReturnStoryLedgerView(root.transform, EmptySnapshot(), () => closedCalls++);

                var closeBtn = view.CloseButton;
                UnityEngine.Object.DestroyImmediate(view.Body.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => closeBtn.onClick.Invoke());
                Assert.That(closedCalls, Is.EqualTo(0));
                Assert.That(view.Panel == null, Is.True);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidViewportAspectsAreIgnoredWithoutDistortingPanelScale()
        {
            var root = new GameObject("Ledger view invalid aspects");
            ReturnStoryLedgerView view = null;
            try
            {
                view = new ReturnStoryLedgerView(root.transform, EmptySnapshot());
                view.LayoutForViewportAspect(16f / 9f);
                Vector3 baselineScale = view.Panel.localScale;

                foreach (float invalidAspect in new[]
                    { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(invalidAspect));
                    Assert.That(view.Panel.localScale, Is.EqualTo(baselineScale));
                }
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidTabEnumIsIgnoredAndReentrantCloseClickFiresOnce()
        {
            var root = new GameObject("Ledger view invalid tab and close");
            ReturnStoryLedgerView view = null;
            try
            {
                int closedCalls = 0;
                view = new ReturnStoryLedgerView(root.transform, EmptySnapshot(), () =>
                {
                    closedCalls++;
                    Assert.That(view.IsDisposed, Is.True);
                    view.CloseButton.onClick.Invoke();
                });

                view.SelectTab((ReturnStoryLedgerTab)99);
                Assert.That(view.ActiveTab, Is.EqualTo(ReturnStoryLedgerTab.MainStory));

                view.CloseButton.onClick.Invoke();
                Assert.That(closedCalls, Is.EqualTo(1));
                Assert.That(view.IsDisposed, Is.True);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
