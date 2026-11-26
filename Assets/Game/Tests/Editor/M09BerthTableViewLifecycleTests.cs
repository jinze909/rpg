using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M09BerthTableViewLifecycleTests
    {
        [Test]
        public void FeeEvidenceReadBlocksReentrantNegotiationAndSettlement()
        {
            var root = new GameObject("M09 fee evidence reentry");
            M09BerthTableView view = null;
            try
            {
                var session = new M09BerthTableSession(shenLiPresent: false);
                bool paid = true, insideRead = false, exerciseReentry = false;
                int settleCalls = 0, reads = 0;
                view = new M09BerthTableView(root.transform, session, _ => settleCalls++,
                    shipwrightFeePaidInWorld: () =>
                    {
                        reads++;
                        if (exerciseReentry && !insideRead)
                        {
                            insideRead = true;
                            try
                            {
                                view.Refresh();
                                view.HanQiaoButton.onClick.Invoke();
                                view.QiQueButton.onClick.Invoke();
                                view.ShipwrightButton.onClick.Invoke();
                                view.SettleButton.onClick.Invoke();
                            }
                            finally { insideRead = false; }
                        }
                        return paid;
                    });
                exerciseReentry = true;
                int before = reads;
                view.Refresh();
                Assert.That(reads, Is.EqualTo(before + 1));
                Assert.That(session.AddedBerths, Is.Zero);
                Assert.That(settleCalls, Is.Zero);

                paid = false; // Evidence withdrawn after the button was displayed.
                view.ShipwrightButton.onClick.Invoke();
                Assert.That(session.AddedBerths, Is.Zero);
                Assert.That(session.ShipwrightFeePaid, Is.False);
                Assert.That(settleCalls, Is.Zero);

                paid = true;
                view.ShipwrightButton.onClick.Invoke();
                Assert.That(session.AddedBerths, Is.EqualTo(2));
                Assert.That(session.NegotiatedExchanges,
                    Is.EqualTo(M09BerthExchangeFlags.ShipwrightExtraBerths));
                Assert.That(session.ShipwrightFeePaid, Is.True);
                Assert.That(settleCalls, Is.Zero);
                view.ShipwrightButton.onClick.Invoke();
                Assert.That(session.AddedBerths, Is.EqualTo(2));

                view.QiQueButton.onClick.Invoke();
                Assert.That(session.AddedBerths, Is.EqualTo(5));
                view.SettleButton.onClick.Invoke();
                Assert.That(settleCalls, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FeeEvidenceCallbackDisposingPanelCannotCommitExchange()
        {
            var root = new GameObject("M09 fee evidence disposal");
            M09BerthTableView view = null;
            try
            {
                var session = new M09BerthTableSession(shenLiPresent: false);
                bool closeOnRead = false;
                view = new M09BerthTableView(root.transform, session, _ => { },
                    shipwrightFeePaidInWorld: () =>
                    {
                        if (closeOnRead) view.Dispose();
                        return true;
                    });
                Button button = view.ShipwrightButton;
                closeOnRead = true;
                Assert.DoesNotThrow(() => button.onClick.Invoke());
                Assert.That(view.IsDisposed, Is.True);
                Assert.That(session.AddedBerths, Is.Zero);
                Assert.That(session.ShipwrightFeePaid, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

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
