using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M09BerthTableViewLifecycleTests
    {
        [Test]
        public void LayoutForViewportAspectIgnoresNonFiniteAndNonPositiveAspects()
        {
            var root = new GameObject("M09 view invalid aspect");
            M09BerthTableView view = null;
            try
            {
                var session = new M09BerthTableSession();
                view = new M09BerthTableView(root.transform, session, _ => { });
                Vector3 initialScale = view.Panel.localScale;

                foreach (float aspect in new[]
                    { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f, -1.5f })
                {
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));
                    Assert.That(view.Panel.localScale, Is.EqualTo(initialScale));
                    Assert.That(view.IsDisposed, Is.False);
                }
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedButtonLabelMarksViewDisposedAndPreventsRefreshException()
        {
            var root = new GameObject("M09 view destroyed label");
            M09BerthTableView view = null;
            try
            {
                var session = new M09BerthTableSession();
                view = new M09BerthTableView(root.transform, session, _ => { });
                Text label = view.HanQiaoButton.GetComponentInChildren<Text>(true);
                Assert.That(label, Is.Not.Null);

                UnityEngine.Object.DestroyImmediate(label);
                Assert.That(view.IsDisposed, Is.True);
                Assert.DoesNotThrow(() => view.Refresh());
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExternalCanvasDestructionDisposesOnActionClickWithoutMutatingSessionOrSettling()
        {
            var root = new GameObject("M09 view destroyed canvas click");
            M09BerthTableView view = null;
            try
            {
                var session = new M09BerthTableSession();
                int settleCalls = 0;
                view = new M09BerthTableView(root.transform, session, _ => settleCalls++);
                Button hanBtn = view.HanQiaoButton;
                Button reconcileBtn = view.ReconcileButton;
                Button settleBtn = view.SettleButton;

                UnityEngine.Object.DestroyImmediate(view.Panel.parent.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => hanBtn.onClick.Invoke());
                Assert.DoesNotThrow(() => reconcileBtn.onClick.Invoke());
                Assert.DoesNotThrow(() => settleBtn.onClick.Invoke());
                Assert.That(session.AddedBerths, Is.Zero);
                Assert.That(session.ShenReconciled, Is.False);
                Assert.That(settleCalls, Is.Zero);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
