using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rpg.Tests.Editor
{
    public sealed class M06FamilyLetterDeliveryGameViewEvidenceTests
    {
        [UnityTest]
        public IEnumerator FamilyLetterHandoffCapturesChineseAndEnglishGameViewEvidence()
        {
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return null;
            yield return CaptureLetterHandoff();
            yield return new ExitPlayMode();
        }

        private static IEnumerator CaptureLetterHandoff()
        {
            string previousLanguage = SoundwellUiLocalization.Language;
            var values = new Dictionary<string, int>();
            var root = new GameObject("M06 family letter handoff visual evidence root");
            M06PumpRoomReunionView view = null;
            try
            {
                M06FamilyLetterDeliveryFactsTests.SeedArrival(values);
                Func<string, int> read = key =>
                    values.TryGetValue(key, out int value) ? value : 0;
                Action<string, int> write = (key, value) => values[key] = value;

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(
                    read, write, out M06PumpRoomReunionSession session), Is.True);
                view = new M06PumpRoomReunionView(root.transform, session, _ => { });
                view.GreetingWrenchButton.onClick.Invoke();
                view.AdvanceButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.LetterHandoff));
                Assert.That(view.Body.text, Does.Contain("已处理 0/3"));

                Canvas canvas = root.GetComponentsInChildren<Canvas>(true)
                    .Single(item => item.gameObject.name == "M06 pump room reunion canvas");
                SoundwellSmokeHost.CaptureStoryCanvas(canvas, "m06-family-letter-handoff-zh");

                view.TaoJiLetterButton.onClick.Invoke();
                yield return null;
                Assert.That(view.Body.text, Does.Contain("已处理 1/3"));
                SoundwellSmokeHost.CaptureStoryCanvas(canvas, "m06-family-letter-handoff-progress-zh");

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                yield return null;
                Assert.That(view.Body.text, Does.Contain("1/3"));
                SoundwellSmokeHost.CaptureStoryCanvas(canvas, "m06-family-letter-handoff-progress-en");
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                if (root != null)
                    UnityEngine.Object.Destroy(root);
            }

            yield return null;
        }
    }
}