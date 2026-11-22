using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M16EpilogueGameViewEvidenceTests
    {
        [UnityTest]
        public IEnumerator DeskLetterReadoutsCaptureChineseAndEnglishGameViewEvidence()
        {
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return null;
            yield return CaptureDeskLetterViews();
            yield return new ExitPlayMode();
        }

        private static IEnumerator CaptureDeskLetterViews()
        {
            string previousLanguage = SoundwellUiLocalization.Language;
            var values = new Dictionary<string, int>();
            var root = new GameObject("M16 epilogue visual evidence root");
            M16EpilogueHost host = null;
            try
            {
                M16EpilogueLettersViewTests.SeedM15WithCompletedRC01AndST02(values);
                Func<string, int> read = key =>
                    values.TryGetValue(key, out int value) ? value : 0;
                Action<string, int> write = (key, value) => values[key] = value;

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                host = new M16EpilogueHost(
                    root.transform,
                    () => true,
                    () => true,
                    () => M16RespectAgencyTone.RespectedOthersChoices,
                    read,
                    write);
                Assert.That(host.TryOpen(), Is.True,
                    "The M16 desk evidence must use a verified M15 roll-call record.");

                M16EpilogueView view = host.ActiveView;
                Assert.That(view, Is.Not.Null);
                view.ProceedToDeskButton.onClick.Invoke();
                yield return null;

                Text nextLetterLabel = view.ReadNextLetterButton.GetComponentInChildren<Text>();
                Assert.That(nextLetterLabel.text, Does.Contain("砚叔"),
                    "The desk must name the next actually delivered letter before it is opened.");
                Canvas canvas = root.GetComponentsInChildren<Canvas>(true)
                    .Single(item => item.gameObject.name == "M16 epilogue canvas");
                SoundwellSmokeHost.CaptureStoryCanvas(canvas, "m16-desk-letter-next-zh");

                view.ReadNextLetterButton.onClick.Invoke();
                yield return null;
                Assert.That(view.Body.text, Does.Contain("RC-01 砚叔来信"));
                SoundwellSmokeHost.CaptureStoryCanvas(canvas, "m16-desk-letter-rc01-zh");

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                yield return null;
                Assert.That(view.Body.text, Does.Contain("[RC-01 Chen Yan]"));
                Assert.That(nextLetterLabel.text, Does.Contain("River Lanterns"));
                SoundwellSmokeHost.CaptureStoryCanvas(canvas, "m16-desk-letter-rc01-en");
            }
            finally
            {
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                if (root != null)
                    UnityEngine.Object.Destroy(root);
            }

            yield return null;
        }
    }
}