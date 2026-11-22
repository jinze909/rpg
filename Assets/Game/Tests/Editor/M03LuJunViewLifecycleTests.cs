using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M03LuJunViewLifecycleTests
    {
        private GameObject root;
        private M03LuJunView view;
        private string previousLanguage;

        [SetUp]
        public void SetUp()
        {
            previousLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M03 Lu Jun lifecycle root");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
            SoundwellUiLocalization.SetLanguage(previousLanguage);
        }

        [Test]
        public void IsDisposedTracksExplicitDisposeAndExternalCanvasDestruction()
        {
            view = new M03LuJunView(root.transform, M02BridgeEvidence.None, false, _ => { });
            Assert.That(view.IsDisposed, Is.False);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.That(view.IsDisposed, Is.True);

            Assert.DoesNotThrow(() => view.Dispose());
            Assert.That(view.IsDisposed, Is.True);
        }

        [Test]
        public void DestroyedCanvasBlocksAdvanceAndVoyageChoiceWithoutMutatingEncounterOrThrowing()
        {
            int finishedCalls = 0;
            view = new M03LuJunView(root.transform, M02BridgeEvidence.FerrymanLog, true, _ => finishedCalls++);
            for (int beat = 0; beat < 4; beat++)
                view.AdvanceButton.onClick.Invoke();
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));

            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.DoesNotThrow(() => view.GetChoiceButton(0).onClick.Invoke());
            Assert.DoesNotThrow(() => view.AdvanceButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));
            Assert.That(view.Encounter.Voyage, Is.Null);
            Assert.That(finishedCalls, Is.Zero);
        }

        [Test]
        public void FailedHostCallbackThatDisposesViewDoesNotReopenOrThrowSecondaryError()
        {
            view = new M03LuJunView(root.transform, M02BridgeEvidence.None, false, _ =>
            {
                view.Dispose();
                throw new InvalidOperationException("Host tore down Lu Jun view and failed");
            });
            for (int beat = 0; beat < 4; beat++)
                view.AdvanceButton.onClick.Invoke();
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));

            Assert.Throws<InvalidOperationException>(() => view.GetChoiceButton(1).onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));
            Assert.That(view.Encounter.Voyage, Is.Null);
        }

        [Test]
        public void LayoutForViewportAspectIgnoresNonPositiveAndNonFiniteAspects()
        {
            view = new M03LuJunView(root.transform, M02BridgeEvidence.None, false, _ => { });

            view.LayoutForViewportAspect(16f / 9f);
            Vector3 expectedScale = view.AdvanceButton.transform.parent.localScale;

            view.LayoutForViewportAspect(0f);
            view.LayoutForViewportAspect(-1f);
            view.LayoutForViewportAspect(float.NaN);
            view.LayoutForViewportAspect(float.PositiveInfinity);
            view.LayoutForViewportAspect(float.NegativeInfinity);

            Assert.That(view.AdvanceButton.transform.parent.localScale, Is.EqualTo(expectedScale));
            Assert.That(view.IsDisposed, Is.False);
        }
    }
}
