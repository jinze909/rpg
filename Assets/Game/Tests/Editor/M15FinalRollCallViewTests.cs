using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M15FinalRollCallViewTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void FullRollCallFlowCarvesConsentedNamesAndSettlesPensionOnce()
        {
            var root = new GameObject("M15 view public carve");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string previousLanguage = SoundwellUiLocalization.Language;
            M15FinalRollCallView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                var session = new M15FinalRollCallSession(
                    bridgeTruthSealed: false, consentedVerifiedNamesCount: 4);
                int saves = 0;
                view = new M15FinalRollCallView(root.transform, session, (reader, identity, stele) =>
                {
                    saves++;
                    Assert.That(session.TryCompleteRollCall(reader, identity, stele, read, write), Is.True);
                });

                Assert.That(view.Body.text, Does.Contain("七十二"));
                view.HanQiaoReaderButton.onClick.Invoke();
                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.SelectIdentityResponse));

                view.PublicTrueNameButton.onClick.Invoke();
                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.SelectSteleInscription));
                Assert.That(view.CarveNamesSteleButton.interactable, Is.True);

                view.CarveNamesSteleButton.onClick.Invoke();
                view.CarveNamesSteleButton.onClick.Invoke();
                Assert.That(saves, Is.EqualTo(1));
                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.Completed));
                Assert.That(M15FinalRollCallFacts.TryRead(read, out M15FinalRollCallRecord? saved), Is.True);
                Assert.That(saved.Value.Reader, Is.EqualTo(M15RollCallReader.HanQiao));
                Assert.That(saved.Value.PensionClosure, Is.EqualTo(M15PensionClosure.StoppedAndReconciled));
                Assert.That(saved.Value.SteleInscription, Is.EqualTo(M15SteleInscription.CarveConsentedVerifiedNames));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SealedTruthWithZeroConsentedNamesDisablesCarvingAndAllowsPrivateResponse()
        {
            var root = new GameObject("M15 view sealed private");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M15FinalRollCallView view = null;
            try
            {
                var session = new M15FinalRollCallSession(
                    bridgeTruthSealed: true, consentedVerifiedNamesCount: 0, xiaoqiCustomName: "陈安");
                view = new M15FinalRollCallView(root.transform, session, (reader, identity, stele) =>
                {
                    Assert.That(session.TryCompleteRollCall(reader, identity, stele, read, write), Is.True);
                });

                view.AuntJiangReaderButton.onClick.Invoke();
                view.PrivateCustodianButton.onClick.Invoke();
                Assert.That(view.CarveNamesSteleButton.interactable, Is.False);
                Assert.That(view.AddReturnedAliveSteleButton.interactable, Is.True);

                view.AddReturnedAliveSteleButton.onClick.Invoke();
                Assert.That(M15FinalRollCallFacts.TryRead(read, out M15FinalRollCallRecord? saved), Is.True);
                Assert.That(saved.Value.IdentityResponse, Is.EqualTo(M15FinalIdentityResponse.PrivateCustodianResponse));
                Assert.That(saved.Value.PensionClosure, Is.EqualTo(M15PensionClosure.UnchangedOrPrivate));
                Assert.That(saved.Value.XiaoqiCalledByNumberSeventyTwo, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedSteleSaveRetainsInscriptionButtonsForCleanRetry()
        {
            var root = new GameObject("M15 view retry");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M15FinalRollCallView view = null;
            try
            {
                var session = new M15FinalRollCallSession(false, 2);
                int attempts = 0;
                view = new M15FinalRollCallView(root.transform, session, (reader, identity, stele) =>
                {
                    attempts++;
                    if (attempts == 1)
                        throw new InvalidOperationException("Interrupted M15 write");
                    Assert.That(session.TryCompleteRollCall(reader, identity, stele, read, write), Is.True);
                });

                view.ProtagonistReaderButton.onClick.Invoke();
                view.PublicTrueNameButton.onClick.Invoke();

                Assert.Throws<InvalidOperationException>(() => view.KeepOriginalSteleButton.onClick.Invoke());
                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.SelectSteleInscription));
                Assert.That(view.KeepOriginalSteleButton.interactable, Is.True);

                view.KeepOriginalSteleButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.Completed));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocaleSwitchAspectScalingAndDisposalGuardsWorkCleanly()
        {
            var root = new GameObject("M15 view locale");
            string previousLanguage = SoundwellUiLocalization.Language;
            M15FinalRollCallView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = new M15FinalRollCallSession(false, 1);
                view = new M15FinalRollCallView(root.transform, session, (_, __, ___) => { });
                Assert.That(view.Heading.text, Does.Contain("FINAL ROLL CALL"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Heading.text, Does.Contain("最后一次点名"));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                var btn = view.ProtagonistReaderButton;
                view.Dispose();
                btn.onClick.Invoke();
                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.SelectReader));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
