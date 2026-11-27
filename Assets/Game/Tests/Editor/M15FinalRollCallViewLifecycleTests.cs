using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M15FinalRollCallViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void PartialChildTeardownDisposesViewAndBlocksClickLayoutAndLocaleRefresh()
        {
            var root = new GameObject("M15 view partial teardown");
            string previousLanguage = SoundwellUiLocalization.Language;
            M15FinalRollCallView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                var session = new M15FinalRollCallSession(false, 3);
                int calls = 0;
                view = new M15FinalRollCallView(root.transform, session, (_, __, ___) => calls++);

                var readerButton = view.ProtagonistReaderButton;
                UnityEngine.Object.DestroyImmediate(view.Heading.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => readerButton.onClick.Invoke());
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.SelectReader));
                Assert.That(view.Panel == null, Is.True);

                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
                Assert.DoesNotThrow(() => SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidViewportAspectsAreIgnoredWithoutDistortingPanelScale()
        {
            var root = new GameObject("M15 view invalid aspects");
            M15FinalRollCallView view = null;
            try
            {
                var session = new M15FinalRollCallSession(false, 2);
                view = new M15FinalRollCallView(root.transform, session, (_, __, ___) => { });
                view.LayoutForViewportAspect(16f / 9f);
                Vector3 baselineScale = view.Panel.localScale;

                foreach (float invalidAspect in new[]
                    { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(invalidAspect));
                    Assert.That(view.Panel.localScale, Is.EqualTo(baselineScale));
                }
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EffectiveInputLockPreservesPrivateIdentityAndSteleChoices()
        {
            var root = new GameObject("M15 roll call effective input lock");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            M15FinalRollCallView view = null;
            try
            {
                var session = new M15FinalRollCallSession(true, 0);
                int saves = 0;
                view = new M15FinalRollCallView(root.transform, session, (reader, identity, stele) =>
                {
                    saves++;
                    Assert.That(session.TryCompleteRollCall(reader, identity, stele,
                        read, (k, v) => values[k] = v), Is.True);
                });
                var group = root.AddComponent<CanvasGroup>();

                group.interactable = false;
                Assert.That(view.ProtagonistReaderButton.IsInteractable(), Is.False);
                view.ProtagonistReaderButton.onClick.Invoke();
                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.SelectReader));
                group.interactable = true;
                view.Panel.gameObject.SetActive(false);
                view.HanQiaoReaderButton.onClick.Invoke();
                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.SelectReader));
                view.Panel.gameObject.SetActive(true);
                view.AuntJiangReaderButton.onClick.Invoke();

                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.SelectIdentityResponse));
                group.interactable = false;
                view.PublicTrueNameButton.onClick.Invoke();
                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.SelectIdentityResponse));
                group.interactable = true;
                view.Panel.gameObject.SetActive(false);
                view.PublicTrueNameButton.onClick.Invoke();
                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.SelectIdentityResponse));
                view.Panel.gameObject.SetActive(true);
                view.PrivateCustodianButton.onClick.Invoke();

                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.SelectSteleInscription));
                group.interactable = false;
                view.KeepOriginalSteleButton.onClick.Invoke();
                Assert.That(saves, Is.Zero);
                group.interactable = true;
                view.Panel.gameObject.SetActive(false);
                view.AddReturnedAliveSteleButton.onClick.Invoke();
                Assert.That(saves, Is.Zero);
                view.Panel.gameObject.SetActive(true);
                Assert.That(view.CarveNamesSteleButton.IsInteractable(), Is.False);
                view.CarveNamesSteleButton.onClick.Invoke();
                Assert.That(saves, Is.Zero);
                Assert.That(values.Count, Is.Zero);

                view.LeaveBlankSteleButton.onClick.Invoke();
                Assert.That(saves, Is.EqualTo(1));
                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.Completed));
                Assert.That(M15FinalRollCallFacts.TryRead(read, out M15FinalRollCallRecord? saved), Is.True);
                Assert.That(saved.Value.Reader, Is.EqualTo(M15RollCallReader.AuntJiang));
                Assert.That(saved.Value.IdentityResponse, Is.EqualTo(M15FinalIdentityResponse.PrivateCustodianResponse));
                Assert.That(saved.Value.PensionClosure, Is.EqualTo(M15PensionClosure.UnchangedOrPrivate));
                Assert.That(saved.Value.SteleInscription, Is.EqualTo(M15SteleInscription.LeaveBlank));
                view.KeepOriginalSteleButton.onClick.Invoke();
                Assert.That(saves, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReentrantSteleSubmitClickIsIgnoredDuringSubmissionCallback()
        {
            var root = new GameObject("M15 view reentrant submit");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M15FinalRollCallView view = null;
            try
            {
                var session = new M15FinalRollCallSession(false, 2);
                int invocations = 0;
                view = new M15FinalRollCallView(root.transform, session, (reader, identity, stele) =>
                {
                    invocations++;
                    Assert.That(view.KeepOriginalSteleButton.interactable, Is.False);
                    view.KeepOriginalSteleButton.onClick.Invoke();
                    Assert.That(session.TryCompleteRollCall(reader, identity, stele, read, write), Is.True);
                });

                view.ProtagonistReaderButton.onClick.Invoke();
                view.PublicTrueNameButton.onClick.Invoke();
                view.KeepOriginalSteleButton.onClick.Invoke();

                Assert.That(invocations, Is.EqualTo(1));
                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.Completed));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposingViewInsideThrowingCallbackDoesNotThrowOnFinallyRefresh()
        {
            var root = new GameObject("M15 view dispose in callback");
            M15FinalRollCallView view = null;
            try
            {
                var session = new M15FinalRollCallSession(false, 1);
                view = new M15FinalRollCallView(root.transform, session, (_, __, ___) =>
                {
                    view.Dispose();
                    throw new InvalidOperationException("Simulated M15 teardown during submit");
                });

                view.ProtagonistReaderButton.onClick.Invoke();
                view.PublicTrueNameButton.onClick.Invoke();

                Assert.Throws<InvalidOperationException>(() => view.KeepOriginalSteleButton.onClick.Invoke());
                Assert.That(view.IsDisposed, Is.True);
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        [Test]
        public void BlankNewLineChoicePreservesOriginalInscriptionInBothLanguages()
        {
            var root = new GameObject("M15 blank new inscription line");
            string previousLanguage = SoundwellUiLocalization.Language;
            M15FinalRollCallView view = null;
            try
            {
                var values = new Dictionary<string, int>();
                var session = new M15FinalRollCallSession(true, 2);
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                view = new M15FinalRollCallView(root.transform, session, (reader, identity, stele) =>
                {
                    Assert.That(session.TryCompleteRollCall(reader, identity, stele,
                        Reader(values), (k, v) => values[k] = v), Is.True);
                });
                view.ProtagonistReaderButton.onClick.Invoke();
                view.PrivateCustodianButton.onClick.Invoke();
                Assert.That(view.Body.text, Does.Contain("The original inscription remains"));
                Assert.That(view.LeaveBlankSteleButton.GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Is.EqualTo("Reserve a blank new line"));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Body.text, Does.Contain("旧碑原文保留"));
                Assert.That(view.LeaveBlankSteleButton.GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Is.EqualTo("预留新行，不刻字"));
                view.LeaveBlankSteleButton.onClick.Invoke();
                Assert.That(session.CompletedRecord.Value.SteleInscription,
                    Is.EqualTo(M15SteleInscription.LeaveBlank));
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
