using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04QiQueCupTests
    {
        [Test]
        public void UnknownDebtCannotBeInventedByAReply()
        {
            var cup = new M04QiQueCupEncounter(true, false);
            cup.Begin();
            cup.ExamineGift();
            Assert.That(M04QiQueCupCopy.Choices(cup, true), Has.Length.EqualTo(2));
            Assert.That(cup.CanChoose(M04QiQueCupReply.PromiseToSettleDebt), Is.False);
            Assert.Throws<InvalidOperationException>(() =>
                cup.Choose(M04QiQueCupReply.PromiseToSettleDebt));
            Assert.That(cup.CupTaken, Is.False);
            cup.Choose(M04QiQueCupReply.AskForReplacement);
            Assert.That(cup.CupTaken, Is.True);
            Assert.That(M04QiQueCupCopy.Narration(cup, true), Does.Contain("沈砾"));
            cup.Finish();
            Assert.That(cup.Stage, Is.EqualTo(M04QiQueCupStage.Complete));
        }

        [Test]
        public void DebtReplyIsAvailableOnlyWhenPreviouslyKnown()
        {
            var cup = new M04QiQueCupEncounter(true, true);
            cup.Begin();
            cup.ExamineGift();
            Assert.That(M04QiQueCupCopy.Choices(cup, false), Has.Length.EqualTo(3));
            cup.Choose(M04QiQueCupReply.PromiseToSettleDebt);
            Assert.That(M04QiQueCupCopy.Narration(cup, true), Does.Contain("船贷"));
            Assert.That(M04QiQueCupCopy.Narration(cup, false), Does.Contain("ship loan"));
            Assert.That(cup.CupTaken, Is.True);
        }

        [Test]
        public void MissingQiQueNeverAwardsTheCup()
        {
            var cup = new M04QiQueCupEncounter(false, true);
            Assert.Throws<InvalidOperationException>(() => M04QiQueCupFacts.TryRecord(cup));
            cup.Begin();
            Assert.That(cup.Stage, Is.EqualTo(M04QiQueCupStage.WithoutCup));
            Assert.That(M04QiQueCupCopy.Narration(cup, false),
                Does.Contain("signal lamp"));
            Assert.Throws<InvalidOperationException>(() => cup.ExamineGift());
            Assert.Throws<InvalidOperationException>(() =>
                cup.Choose(M04QiQueCupReply.TeaseUglyCup));
            cup.Finish();
            Assert.That(cup.CupTaken, Is.False);
            Assert.Throws<InvalidOperationException>(() => cup.Finish());
        }

        [Test]
        public void UguiDoesNotAwardCupUntilPlayerChoosesAndFinishes()
        {
            var root = new GameObject("M04 cup regression");
            M04QiQueCupView view = null;
            string previousLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                int called = 0;
                M04QiQueCupEncounter result = null;
                view = new M04QiQueCupView(root.transform, true, false,
                    c => { called++; result = c; });
                Assert.That(view.Encounter.CupTaken, Is.False);
                view.AdvanceButton.onClick.Invoke();
                Assert.That(view.Encounter.Stage, Is.EqualTo(M04QiQueCupStage.ChoosingReply));
                Assert.That(view.AdvanceButton.gameObject.activeSelf, Is.False);
                Assert.That(view.GetChoiceButton(2).gameObject.activeSelf, Is.False);
                view.GetChoiceButton(2).onClick.Invoke();
                Assert.That(view.Encounter.CupTaken, Is.False);
                view.GetChoiceButton(0).onClick.Invoke();
                Assert.That(view.Encounter.CupTaken, Is.True);
                view.AdvanceButton.onClick.Invoke();
                Assert.That(called, Is.EqualTo(1));
                Assert.That(result.Stage, Is.EqualTo(M04QiQueCupStage.Complete));
                view.AdvanceButton.onClick.Invoke();
                Assert.That(called, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                SoundwellUiLocalization.SetLanguage(previousLanguage);
            }
        }

        [Test]
        public void FailedGiftSaveRestoresReplyAndAllowsExactlyOneSuccessfulRetry()
        {
            var root = new GameObject("M04 gift save retry");
            M04QiQueCupView view = null;
            try
            {
                int attempts = 0;
                int persisted = 0;
                bool interrupt = true;
                view = new M04QiQueCupView(root.transform, true, true, cup =>
                {
                    attempts++;
                    Assert.That(cup.Stage, Is.EqualTo(M04QiQueCupStage.Complete));
                    Assert.That(cup.CupTaken, Is.True);
                    Assert.That(cup.Reply, Is.EqualTo(M04QiQueCupReply.PromiseToSettleDebt));
                    // Even a manually invoked hidden button cannot resubmit
                    // the callback while Makinom is writing the chosen gift.
                    view.AdvanceButton.onClick.Invoke();
                    if (interrupt)
                        throw new InvalidOperationException("Interrupted cup save");
                    persisted++;
                });
                view.AdvanceButton.onClick.Invoke();
                view.GetChoiceButton(2).onClick.Invoke();
                Assert.That(view.Encounter.Stage, Is.EqualTo(M04QiQueCupStage.Responding));
                Assert.Throws<InvalidOperationException>(() => view.AdvanceButton.onClick.Invoke());
                Assert.That(attempts, Is.EqualTo(1));
                Assert.That(persisted, Is.Zero);
                Assert.That(view.Encounter.Stage, Is.EqualTo(M04QiQueCupStage.Responding));
                Assert.That(view.Encounter.CupTaken, Is.True);
                Assert.That(view.AdvanceButton.gameObject.activeSelf, Is.True);

                interrupt = false;
                view.AdvanceButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(persisted, Is.EqualTo(1));
                Assert.That(view.Encounter.Stage, Is.EqualTo(M04QiQueCupStage.Complete));
                view.AdvanceButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(persisted, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FailedNoGiftSaveRemainsExplicitlyWithoutTheCup()
        {
            var root = new GameObject("M04 no-cup retry");
            M04QiQueCupView view = null;
            try
            {
                int attempts = 0;
                view = new M04QiQueCupView(root.transform, false, true, cup =>
                {
                    attempts++;
                    Assert.That(cup.Stage, Is.EqualTo(M04QiQueCupStage.Complete));
                    Assert.That(cup.CupTaken, Is.False);
                    if (attempts == 1)
                        throw new InvalidOperationException("Interrupted no-gift record");
                });
                Assert.Throws<InvalidOperationException>(() => view.AdvanceButton.onClick.Invoke());
                Assert.That(view.Encounter.Stage, Is.EqualTo(M04QiQueCupStage.WithoutCup));
                Assert.That(view.Encounter.CupTaken, Is.False);
                view.AdvanceButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(view.Encounter.Stage, Is.EqualTo(M04QiQueCupStage.Complete));
                Assert.That(view.Encounter.CupTaken, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NoGiftViewIsAPlayableNoCupOutcome()
        {
            var root = new GameObject("M04 no gift");
            M04QiQueCupView view = null;
            try
            {
                int called = 0;
                view = new M04QiQueCupView(root.transform, false, true, c =>
                {
                    called++;
                    Assert.That(c.CupTaken, Is.False);
                });
                view.AdvanceButton.onClick.Invoke();
                Assert.That(called, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedViewCannotReceiveStaleLocaleChangesOrFailOnDispose()
        {
            var root = new GameObject("M04 cup destroyed");
            M04QiQueCupView view = null;
            string previousLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                view = new M04QiQueCupView(root.transform, true, false, _ => { });

                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(9f / 16f));
                Assert.DoesNotThrow(() => view.Dispose());
                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
