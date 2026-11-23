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
    public sealed class M10WorldNoticeGameViewEvidenceTests
    {
        [UnityTest]
        public IEnumerator LinchaoThirdOmenCapturesChineseAndEnglishGameViewEvidence()
        {
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return null;
            yield return CaptureNotice();
            yield return new ExitPlayMode();
        }

        private static IEnumerator CaptureNotice()
        {
            string previousLanguage = SoundwellUiLocalization.Language;
            var values = new Dictionary<string, int>();
            var root = new GameObject("M10 third omen visual evidence root");
            M10AssemblyHost host = null;
            try
            {
                M10WorldNoticeThreeTests.SeedThroughM09BerthTable(values);
                Func<string, int> read = key =>
                    values.TryGetValue(key, out int value) ? value : 0;
                Action<string, int> write = (key, value) => values[key] = value;

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                host = new M10AssemblyHost(
                    root.transform,
                    () => false,
                    () => true,
                    () => false,
                    read,
                    write,
                    () => true);
                Assert.That(host.TryOpen(), Is.True,
                    "The ferry notice must require the verified current M05/M09 arrival.");

                M10WorldNoticeThreeView notice = host.ActiveWorldNoticeView;
                Assert.That(notice, Is.Not.Null);
                Assert.That(notice.Notice.text, Does.Contain("桥在我在"));
                Assert.That(notice.Notice.fontSize, Is.GreaterThanOrEqualTo(18));
                Assert.That(notice.Notice.resizeTextForBestFit, Is.False);
                Assert.That(notice.Notice.text.Split('\n').Length, Is.GreaterThanOrEqualTo(4),
                    "The Chinese notice should show separate readable story beats.");
                Assert.That(notice.Notice.rectTransform.sizeDelta.y, Is.GreaterThanOrEqualTo(110f));
                Canvas canvas = root.GetComponentsInChildren<Canvas>(true)
                    .Single(item => item.gameObject.name == "M10 third world notice canvas");
                SoundwellSmokeHost.CaptureStoryCanvas(canvas, "m10-linchao-third-omen-zh");

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                yield return null;
                Assert.That(notice.Notice.text, Does.Contain("I am the bridge; I am here."));
                Assert.That(notice.Notice.text.Split('\n').Length, Is.GreaterThanOrEqualTo(4),
                    "The English notice should preserve deliberate line breaks.");
                SoundwellSmokeHost.CaptureStoryCanvas(canvas, "m10-linchao-third-omen-en");

                notice.AcknowledgeButton.onClick.Invoke();
                Assert.That(M10WorldNoticeThreeFacts.TryReadForCurrentArrival(
                    read, out bool observed), Is.True);
                Assert.That(observed, Is.True);
                Assert.That(host.ActiveWorldNoticeView, Is.Null);
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