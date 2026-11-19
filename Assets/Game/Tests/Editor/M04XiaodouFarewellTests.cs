using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04XiaodouFarewellTests
    {
        [Test]
        public void TeachingTheRealLineUsesActualPriorPromptHistory()
        {
            // Keep both previously learned line histories in one test inventory entry.
            foreach (bool prompted in new[] { true, false })
            {
                var farewell = new M04XiaodouFarewell(prompted);
                Assert.Throws<InvalidOperationException>(() => M04XiaodouFarewellFacts.TryRecord(farewell));
                farewell.Begin();
                farewell.ReceiveCandy();
                farewell.AnswerChild(M04XiaodouAnswer.TeachRealLine);
                string zh = M04XiaodouFarewellCopy.Describe(farewell, true);
                string en = M04XiaodouFarewellCopy.Describe(farewell, false);
                Assert.That(zh, Does.Contain("谁带了多余的绳子"));
                Assert.That(en, Does.Contain("Who has spare rope"));
                Assert.That(zh, Does.Contain("已经过了桥").And.Contain("稳住主缆"));
                Assert.That(en, Does.Contain("were already across").And.Contain("The main cable"));
                Assert.That(zh.Contains("帮我提词"), Is.EqualTo(prompted));
                farewell.SayGoodbye();
                Assert.That(farewell.TaughtRealLine, Is.True);
                Assert.That(M04XiaodouFarewellCopy.Choices(farewell, true), Is.Empty);
            }
        }

        [Test]
        public void KeepingTheStageLineNeverTeachesTheTruthOrBooksVoyage()
        {
            var farewell = new M04XiaodouFarewell(true);
            farewell.Begin();
            farewell.ReceiveCandy();
            Assert.That(M04XiaodouFarewellCopy.Choices(farewell, true), Has.Length.EqualTo(2));
            farewell.AnswerChild(M04XiaodouAnswer.KeepStageLine);
            Assert.That(farewell.TaughtRealLine, Is.False);
            Assert.That(M04XiaodouFarewellCopy.Describe(farewell, true),
                Does.Contain("替我谢谢姜婶"));
            farewell.SayGoodbye();
            Assert.That(farewell.Stage, Is.EqualTo(M04FarewellStage.Complete));
        }

        [Test]
        public void RejectsSkippedOrRepeatedChoicesAndInvalidCodes()
        {
            var farewell = new M04XiaodouFarewell(false);
            Assert.Throws<InvalidOperationException>(() => farewell.ReceiveCandy());
            farewell.Begin();
            Assert.Throws<InvalidOperationException>(() => farewell.AnswerChild(M04XiaodouAnswer.TeachRealLine));
            farewell.ReceiveCandy();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                farewell.AnswerChild((M04XiaodouAnswer)42));
            Assert.Throws<InvalidOperationException>(() => farewell.SayGoodbye());
            farewell.AnswerChild(M04XiaodouAnswer.KeepStageLine);
            farewell.SayGoodbye();
            Assert.Throws<InvalidOperationException>(() => farewell.AnswerChild(M04XiaodouAnswer.TeachRealLine));
            Assert.Throws<ArgumentNullException>(() => M04XiaodouFarewellFacts.TryRecord(null));
        }

        [Test]
        public void InterruptedFarewellWritePreservesEachChoiceAndAllowsOneRetry()
        {
            foreach (var answer in new[]
            {
                M04XiaodouAnswer.TeachRealLine,
                M04XiaodouAnswer.KeepStageLine
            })
            {
                var root = new GameObject("M04 Xiaodou retry " + answer);
                M04XiaodouFarewellView view = null;
                try
                {
                    int attempts = 0;
                    int saved = 0;
                    bool interrupt = true;
                    view = new M04XiaodouFarewellView(root.transform, true, farewell =>
                    {
                        attempts++;
                        Assert.That(farewell.Stage, Is.EqualTo(M04FarewellStage.Complete));
                        Assert.That(farewell.Answer, Is.EqualTo(answer));
                        Assert.That(view.AdvanceButton.gameObject.activeSelf, Is.False);
                        // Reentrant direct submit while host is saving must
                        // not invoke this callback a second time.
                        view.AdvanceButton.onClick.Invoke();
                        view.GetChoiceButton(0).onClick.Invoke();
                        if (interrupt)
                            throw new InvalidOperationException("Interrupted Makinom write");
                        saved++;
                    });

                    view.AdvanceButton.onClick.Invoke();
                    view.GetChoiceButton((int)answer).onClick.Invoke();
                    Assert.Throws<InvalidOperationException>(() =>
                        view.AdvanceButton.onClick.Invoke());
                    Assert.That(attempts, Is.EqualTo(1));
                    Assert.That(saved, Is.Zero);
                    Assert.That(view.Encounter.Stage,
                        Is.EqualTo(M04FarewellStage.HearingResponse));
                    Assert.That(view.Encounter.Answer, Is.EqualTo(answer));
                    Assert.That(view.AdvanceButton.gameObject.activeSelf, Is.True);

                    interrupt = false;
                    view.AdvanceButton.onClick.Invoke();
                    Assert.That(attempts, Is.EqualTo(2));
                    Assert.That(saved, Is.EqualTo(1));
                    Assert.That(view.Encounter.Stage, Is.EqualTo(M04FarewellStage.Complete));
                    view.AdvanceButton.onClick.Invoke();
                    Assert.That(saved, Is.EqualTo(1));
                }
                finally
                {
                    view?.Dispose();
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void UguiRequiresARealDecisionAndDeliversResultExactlyOnce()
        {
            var root = new GameObject("M04 farewell test root");
            M04XiaodouFarewellView view = null;
            string prior = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                int count = 0;
                M04XiaodouFarewell result = null;
                view = new M04XiaodouFarewellView(root.transform, true,
                    finished => { count++; result = finished; });
                view.AdvanceButton.onClick.Invoke();
                Assert.That(view.Encounter.Stage, Is.EqualTo(M04FarewellStage.ChoosingTrueLine));
                Assert.That(view.AdvanceButton.gameObject.activeSelf, Is.False);
                view.GetChoiceButton(0).onClick.Invoke();
                Assert.That(view.Dialogue.text, Does.Contain("帮我提词"));
                view.AdvanceButton.onClick.Invoke();
                Assert.That(count, Is.EqualTo(1));
                Assert.That(result.TaughtRealLine, Is.True);
                view.AdvanceButton.onClick.Invoke();
                Assert.That(count, Is.EqualTo(1));
                view.LayoutForViewportAspect(9f / 16f);
                view.LayoutForViewportAspect(32f / 9f);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                SoundwellUiLocalization.SetLanguage(prior);
            }
        }
    }
}
