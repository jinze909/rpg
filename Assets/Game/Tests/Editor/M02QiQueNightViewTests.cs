using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M02QiQueNightViewTests
    {
        private GameObject root;
        private M02QiQueNightView view;
        private string previousLanguage;

        [SetUp]
        public void SetUp()
        {
            previousLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M02 Qi Que night test");
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
        public void DebtConfrontationAppearsOnlyWhenFactWasKnownEarlier()
        {
            int completed = 0;
            view = new M02QiQueNightView(root.transform, true, () => completed++);
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.ConfrontingDebt));
            Assert.That(view.Dialogue.text, Does.Contain("mortgaged"));
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Night.DebtConfronted, Is.True);
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Night.ChartStudied, Is.True);
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.Finished));
        }

        [Test]
        public void UnknownDebtAndLanguageChangesNeverLeakTheSecret()
        {
            int completed = 0;
            view = new M02QiQueNightView(root.transform, false, () => completed++);
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.OfferingCake));
            Assert.That(view.Dialogue.text, Does.Not.Contain("韩峤"));
            view.LayoutForViewportAspect(9f / 16f);
            view.LayoutForViewportAspect(32f / 9f);
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Night.DebtConfronted, Is.False);
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void FailedHostCallbackKeepsWarehouseDepartureRetryableAndBlocksReentrancy()
        {
            int attempts = 0;
            view = new M02QiQueNightView(root.transform, true, () =>
            {
                attempts++;
                if (attempts == 1)
                {
                    view.ProgressButton.onClick.Invoke();
                    throw new InvalidOperationException("Simulated host handoff failure");
                }
            });
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.ReviewingChart));

            Assert.Throws<InvalidOperationException>(() => view.ProgressButton.onClick.Invoke());
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.ReviewingChart));
            Assert.That(view.Night.DebtConfronted, Is.True);
            Assert.That(view.Night.ChartStudied, Is.True);
            Assert.That(view.ProgressButton.gameObject.activeSelf, Is.True);

            view.ProgressButton.onClick.Invoke();
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.Finished));
            Assert.That(view.ProgressButton.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void DisposalStopsCallbackAndDetachesLocalization()
        {
            int calls = 0;
            view = new M02QiQueNightView(root.transform, true, () => calls++);
            view.ProgressButton.onClick.Invoke();
            view.Dispose();
            view.ProgressButton.onClick.Invoke();
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(calls, Is.Zero);
        }
    }
}
