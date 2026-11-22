using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M06MineCampReconViewLifecycleTests
    {
        [Test]
        public void LayoutForViewportAspectIgnoresNonFiniteAndNonPositiveAspects()
        {
            var root = new GameObject("M06 recon view invalid aspect");
            M06MineCampReconView view = null;
            try
            {
                view = new M06MineCampReconView(
                    root.transform, M06MineCampLead.GuardPatrol, _ => { });
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
        public void DestroyedHeadingDisposesViewDuringLayoutWithoutThrowing()
        {
            var root = new GameObject("M06 recon view destroyed heading");
            M06MineCampReconView view = null;
            try
            {
                view = new M06MineCampReconView(
                    root.transform, M06MineCampLead.SupplyLedger, _ => { });
                UnityEngine.Object.DestroyImmediate(view.Heading);

                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
                Assert.That(view.IsDisposed, Is.True);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PartialCanvasDestructionDisposesOnConfirmClickWithoutInvokingCallback()
        {
            var root = new GameObject("M06 recon view partial destroy confirm");
            M06MineCampReconView view = null;
            try
            {
                int callbacks = 0;
                view = new M06MineCampReconView(
                    root.transform, M06MineCampLead.EscortSchedule, _ => callbacks++);
                Button confirmBtn = view.ConfirmButton;

                UnityEngine.Object.DestroyImmediate(view.Summary);
                Assert.DoesNotThrow(() => confirmBtn.onClick.Invoke());
                Assert.That(callbacks, Is.Zero);
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
