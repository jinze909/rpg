using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RC04ReefListeningViewTests
    {
        [Test]
        public void RealThreeRoundEchoDiscriminationRequiresSlowHumanReplyEachTime()
        {
            var root = new GameObject("RC04 UGUI three rounds");
            var session = new RC04DingYueReefSession();
            RC04ReefListeningView view = null;
            string language = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                int approaches = 0;
                view = new RC04ReefListeningView(root.transform,
                    session, _ => { approaches++; return true; });
                Assert.That(view.Body.text, Does.Contain("第一轮"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                Assert.That(view.ApproachButton.gameObject.activeSelf, Is.False);
                view.FastEchoButton.onClick.Invoke();
                Assert.That(session.FalseMimicPicks, Is.EqualTo(1));
                Assert.That(session.CompletedListeningRounds, Is.Zero);
                Assert.That(view.Body.text, Does.Contain("不能增加识别轮次"));

                view.SlowEchoButton.onClick.Invoke();
                Assert.That(session.CompletedListeningRounds, Is.EqualTo(1));
                Assert.That(view.Body.text, Does.Contain("第二轮"));
                view.SlowEchoButton.onClick.Invoke();
                Assert.That(session.CompletedListeningRounds, Is.EqualTo(2));
                Assert.That(view.Body.text, Does.Contain("第三轮"));
                Assert.That(view.ApproachButton.interactable, Is.False);
                view.SlowEchoButton.onClick.Invoke();
                Assert.That(session.TidalWindowLocated, Is.True);
                Assert.That(view.ApproachButton.interactable, Is.True);
                Assert.That(view.Body.text, Does.Contain("不能凭此认定丁岳已获救"));
                Assert.That(approaches, Is.Zero);
                view.ApproachButton.onClick.Invoke();
                Assert.That(approaches, Is.EqualTo(1));
                Assert.That(view.ApproachDelivered, Is.True);
                view.ApproachButton.onClick.Invoke();
                Assert.That(approaches, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(language);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PhysicalApproachRejectionAndThrownCallbackAreRetryable()
        {
            var root = new GameObject("RC04 UGUI tide gate");
            var session = new RC04DingYueReefSession();
            for (int i = 0; i < 3; i++)
                Assert.That(session.SubmitEchoDiscrimination(RC04EchoChoice.SlowHumanEcho), Is.True);
            RC04ReefListeningView view = null;
            try
            {
                int attempts = 0;
                view = new RC04ReefListeningView(root.transform, session, _ =>
                {
                    attempts++;
                    if (attempts == 1) return false; // No agreed short-tide boat yet.
                    if (attempts == 2) throw new InvalidOperationException("Interrupted scene handoff");
                    return true;
                });
                view.ApproachButton.onClick.Invoke();
                Assert.That(view.ApproachDelivered, Is.False);
                Assert.That(view.ApproachButton.interactable, Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    view.ApproachButton.onClick.Invoke());
                Assert.That(view.ApproachDelivered, Is.False);
                view.ApproachButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(3));
                Assert.That(view.ApproachDelivered, Is.True);
                Assert.That(session.IsResolved, Is.False,
                    "Requesting a reef approach never invents a saved RC04 resolution.");
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EnglishLocalizationAspectAndDisposeGuardsKeepExistingFactsUntouched()
        {
            var root = new GameObject("RC04 UGUI accessibility");
            var session = new RC04DingYueReefSession();
            RC04ReefListeningView view = null;
            string language = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                view = new RC04ReefListeningView(root.transform, session, _ => true);
                Assert.That(view.Heading.text, Does.Contain("REPLIES IN THE SALT FOG"));
                Assert.That(view.Body.text, Does.Contain("Round 1"));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Body.text, Does.Contain("第一轮"));
                var echo = view.SlowEchoButton;
                view.Dispose();
                echo.onClick.Invoke();
                Assert.That(session.CompletedListeningRounds, Is.Zero);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(language);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AlreadyResolvedSessionDoesNotPermitReplayOrApproach()
        {
            var root = new GameObject("RC04 already complete");
            var session = new RC04DingYueReefSession();
            for (int i = 0; i < 3; i++)
                session.SubmitEchoDiscrimination(RC04EchoChoice.SlowHumanEcho);
            var values = new Dictionary<string, int>();
            Func<string, int> read = k => values.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => values[k] = v;
            Assert.That(session.TryResolveReef(
                RC04Resolution.BringDingYueBack, read, write), Is.True);
            int calls = 0;
            RC04ReefListeningView view = null;
            try
            {
                view = new RC04ReefListeningView(root.transform, session,
                    _ => { calls++; return true; });
                Assert.That(view.ApproachButton.gameObject.activeSelf, Is.False);
                Assert.That(view.SlowEchoButton.gameObject.activeSelf, Is.False);
                Assert.That(view.FastEchoButton.gameObject.activeSelf, Is.False);
                string expected = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                    ? "已有经核验的处置结果"
                    : "already has a verified resolution";
                Assert.That(view.Body.text, Does.Contain(expected));
                view.ApproachButton.onClick.Invoke();
                Assert.That(calls, Is.Zero);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
