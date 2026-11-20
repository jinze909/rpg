using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M05SeranLettersViewTests
    {
        [Test]
        public void TakingPacketPreviewsAuthoredLettersButOnlyTheHostRecordsTheChoice()
        {
            var root = new GameObject("M05 letters accepted");
            M05SeranLettersView view = null;
            string previousLanguage = SoundwellUiLocalization.Language;
            var store = new Dictionary<string, int>();
            Func<string, int> read = key => store.TryGetValue(key, out int v) ? v : 0;
            Action<string, int> write = (key, value) => store[key] = value;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                int calls = 0;
                view = new M05SeranLettersView(root.transform, decision =>
                {
                    calls++;
                    Assert.That(M05LettersFacts.TryRecord(decision, read, write), Is.True);
                });
                Assert.That(view.SelectedDecision, Is.Null);
                Assert.That(view.TakeButton.gameObject.activeSelf, Is.True);
                view.ContinueButton.onClick.Invoke();
                Assert.That(calls, Is.Zero, "Hidden Continue cannot skip the choice.");

                view.TakeButton.onClick.Invoke();
                Assert.That(view.SelectedDecision, Is.EqualTo(M05LetterDecision.TakePacket));
                Assert.That(view.ReadPacketButton.gameObject.activeSelf, Is.True);
                Assert.That(view.Dialogue.text, Does.Not.Contain("household accounts"));
                view.ReadPacketButton.onClick.Invoke();
                Assert.That(view.Dialogue.text, Does.Contain("household accounts"));
                Assert.That(view.Dialogue.text, Does.Contain("child's drawing"));
                Assert.That(view.Dialogue.text, Does.Contain("None has been delivered yet"));
                Assert.That(view.ContinueButton.interactable, Is.True);
                Assert.That(M05LettersFacts.TryRead(read, out M05LetterDecision? beforeSave), Is.True);
                Assert.That(beforeSave, Is.Null);
                view.LeaveButton.onClick.Invoke();
                Assert.That(view.SelectedDecision, Is.EqualTo(M05LetterDecision.TakePacket));

                view.ContinueButton.onClick.Invoke();
                view.ContinueButton.onClick.Invoke();
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(M05LettersFacts.TryRead(read, out M05LetterDecision? afterSave), Is.True);
                Assert.That(afterSave, Is.EqualTo(M05LetterDecision.TakePacket));
                Assert.That(M05LettersFacts.LettersCarried(afterSave), Is.EqualTo(8));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LeavingPacketCannotTurnIntoAcceptanceOnHiddenButtonSubmit()
        {
            var root = new GameObject("M05 letters left for next boat");
            M05SeranLettersView view = null;
            try
            {
                int calls = 0;
                M05LetterDecision result = default;
                view = new M05SeranLettersView(root.transform, decision =>
                {
                    calls++;
                    result = decision;
                });
                view.LeaveButton.onClick.Invoke();
                Assert.That(view.Dialogue.text, Does.Contain("next boat"));
                Assert.That(view.ReadPacketButton.gameObject.activeSelf, Is.False);
                view.ReadPacketButton.onClick.Invoke();
                Assert.That(view.Dialogue.text, Does.Not.Contain("doorstep"));
                view.TakeButton.onClick.Invoke();
                Assert.That(view.SelectedDecision,
                    Is.EqualTo(M05LetterDecision.LeaveForNextBoat));
                view.ContinueButton.onClick.Invoke();
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(result, Is.EqualTo(M05LetterDecision.LeaveForNextBoat));
                Assert.That(M05LettersFacts.LettersCarried(result), Is.Zero);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedHostWriteKeepsSelectionAndAllowsExactlyOneRetry()
        {
            var root = new GameObject("M05 letters interrupted host");
            M05SeranLettersView view = null;
            try
            {
                int attempts = 0;
                int successes = 0;
                view = new M05SeranLettersView(root.transform, decision =>
                {
                    attempts++;
                    view.ContinueButton.onClick.Invoke();
                    if (attempts == 1)
                        throw new InvalidOperationException("Interrupted Makinom write");
                    successes++;
                    Assert.That(decision, Is.EqualTo(M05LetterDecision.TakePacket));
                });
                view.TakeButton.onClick.Invoke();
                Assert.Throws<InvalidOperationException>(() =>
                    view.ContinueButton.onClick.Invoke());
                Assert.That(view.SelectedDecision, Is.EqualTo(M05LetterDecision.TakePacket));
                Assert.That(view.ContinueButton.interactable, Is.True);
                Assert.That(attempts, Is.EqualTo(1));
                view.ContinueButton.onClick.Invoke();
                view.ContinueButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(successes, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SwitchingLanguageKeepsTheUncommittedChoiceAndLetterCount()
        {
            var root = new GameObject("M05 letters locale switch");
            M05SeranLettersView view = null;
            string previousLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                view = new M05SeranLettersView(root.transform, _ => { });
                view.TakeButton.onClick.Invoke();
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Dialogue.text, Does.Not.Contain("我把门槛修好了"));
                view.ReadPacketButton.onClick.Invoke();
                Assert.That(view.Dialogue.text, Does.Contain("我把门槛修好了"));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(view.Dialogue.text, Does.Contain("I fixed the doorstep"));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Dialogue.text, Does.Contain("还没有送达任何人"));
                Assert.That(view.SelectedDecision, Is.EqualTo(M05LetterDecision.TakePacket));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));
                Assert.That(view.ContinueButton.interactable, Is.True);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NullParentOrCallbackRejectedAndDisposedUiCannotDeliverAgain()
        {
            var root = new GameObject("M05 letters disposed");
            M05SeranLettersView view = null;
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new M05SeranLettersView(null, _ => { }));
                Assert.Throws<ArgumentNullException>(() =>
                    new M05SeranLettersView(root.transform, null));
                int saved = 0;
                view = new M05SeranLettersView(root.transform, _ => saved++);
                view.TakeButton.onClick.Invoke();
                view.Dispose();
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(1f));
                Assert.That(saved, Is.Zero);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
