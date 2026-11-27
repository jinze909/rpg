using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M14FinalPassageViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static M14FinalPassageSession CreateVerifiedSession()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M13DepartureRiskFacts.TryRecordDeparture(new M13DepartureRecord(
                M13ApproachRoute.RouteASupervisedCivilian,
                safetyPreparationComplete: true,
                qiQueWarningGiven: false,
                explicitHighRiskConsent: false,
                threeBellReverseSignalAgreed: false), read, write), Is.True);

            Assert.That(M14FinalPassageSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            return session;
        }

        private static M14FinalPassageView CreateView(
            Transform parent,
            M14FinalPassageSession session,
            Action<M14AyuanOutcome> onAyuanSubmitted)
        {
            return new M14FinalPassageView(
                parent,
                session,
                () => true,
                () => true,
                () => false,
                () => false,
                () => false,
                () => new M14CivilianCounts(16, 4, 2),
                onAyuanSubmitted);
        }

        private static void AdvanceToAyuan(M14FinalPassageView view)
        {
            view.DetainLuJunButton.onClick.Invoke();
            view.VerifyCivilianExitButton.onClick.Invoke();
        }

        [Test]
        public void DestroyingCoreChildControlDetachesLocaleAndBlocksClicks()
        {
            var root = new GameObject("M14 view partial teardown");
            string savedLanguage = SoundwellUiLocalization.Language;
            M14FinalPassageView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = CreateVerifiedSession();
                int submissions = 0;

                view = CreateView(root.transform, session, _ => submissions++);
                Assert.That(view.IsDisposed, Is.False);

                // Destroy a core child control while leaving canvas and panel alive.
                UnityEngine.Object.DestroyImmediate(view.WithdrawLuJunButton.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => view.DetainLuJunButton.onClick.Invoke());
                Assert.That(session.Phase, Is.EqualTo(M14Phase.RouteExecution));
                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
                Assert.DoesNotThrow(() => view.Dispose());
                Assert.That(submissions, Is.Zero);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EffectiveInputLockBlocksRouteCivilianVerificationAndAyuanSubmission()
        {
            var root = new GameObject("M14 final passage input lock");
            M14FinalPassageView view = null;
            try
            {
                var session = CreateVerifiedSession();
                int submissions = 0;
                view = CreateView(root.transform, session, outcome =>
                {
                    Assert.That(outcome, Is.EqualTo(M14AyuanOutcome.Safe));
                    submissions++;
                });
                var group = view.Panel.gameObject.AddComponent<CanvasGroup>();

                group.interactable = false;
                Assert.That(view.DetainLuJunButton.IsInteractable(), Is.False);
                view.RelieveLuJunButton.onClick.Invoke();
                view.DetainLuJunButton.onClick.Invoke();
                view.WithdrawLuJunButton.onClick.Invoke();
                Assert.That(session.Phase, Is.EqualTo(M14Phase.RouteExecution));

                group.interactable = true;
                view.Panel.gameObject.SetActive(false);
                view.DetainLuJunButton.onClick.Invoke();
                Assert.That(session.Phase, Is.EqualTo(M14Phase.RouteExecution));
                view.Panel.gameObject.SetActive(true);
                view.DetainLuJunButton.onClick.Invoke();
                Assert.That(session.Phase, Is.EqualTo(M14Phase.CivilianExitVerification));

                group.interactable = false;
                view.VerifyCivilianExitButton.onClick.Invoke();
                Assert.That(session.Phase, Is.EqualTo(M14Phase.CivilianExitVerification));
                group.interactable = true;
                view.Panel.gameObject.SetActive(false);
                view.VerifyCivilianExitButton.onClick.Invoke();
                Assert.That(session.Phase, Is.EqualTo(M14Phase.CivilianExitVerification));
                view.Panel.gameObject.SetActive(true);
                view.VerifyCivilianExitButton.onClick.Invoke();
                Assert.That(session.Phase, Is.EqualTo(M14Phase.AyuanPositionResolution));

                group.interactable = false;
                view.SettleAyuanSafeButton.onClick.Invoke();
                group.interactable = true;
                view.Panel.gameObject.SetActive(false);
                view.SettleAyuanSafeButton.onClick.Invoke();
                Assert.That(submissions, Is.Zero);
                view.Panel.gameObject.SetActive(true);
                view.SettleAyuanSafeButton.onClick.Invoke();
                Assert.That(submissions, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidViewportAspectsAreIgnoredWithoutThrowing()
        {
            var root = new GameObject("M14 view aspect guard");
            M14FinalPassageView view = null;
            try
            {
                var session = CreateVerifiedSession();
                view = CreateView(root.transform, session, _ => { });

                view.LayoutForViewportAspect(16f / 9f);
                Vector3 expectedScale = view.Panel.localScale;

                foreach (float badAspect in new[]
                {
                    0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity
                })
                {
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(badAspect));
                    Assert.That(view.Panel.localScale, Is.EqualTo(expectedScale));
                }

                Assert.That(view.IsDisposed, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReentrantAyuanSubmitClickIsBlockedWhileHostCallbackIsInFlight()
        {
            var root = new GameObject("M14 view reentrant submit");
            M14FinalPassageView view = null;
            try
            {
                var session = CreateVerifiedSession();
                int submissions = 0;

                view = CreateView(root.transform, session, _ =>
                {
                    submissions++;
                    Assert.That(view.SettleAyuanSafeButton.interactable, Is.False);
                    view.SettleAyuanSafeButton.onClick.Invoke();
                });

                AdvanceToAyuan(view);
                view.SettleAyuanSafeButton.onClick.Invoke();
                Assert.That(submissions, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ThrowingOnAyuanSubmitAfterDisposingViewDoesNotResurrectView()
        {
            var root = new GameObject("M14 view dispose-then-throw");
            string savedLanguage = SoundwellUiLocalization.Language;
            M14FinalPassageView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = CreateVerifiedSession();

                view = CreateView(root.transform, session, _ =>
                {
                    view.Dispose();
                    throw new InvalidOperationException("Host disposed view before throwing");
                });

                AdvanceToAyuan(view);
                Assert.Throws<InvalidOperationException>(() =>
                    view.SettleAyuanSafeButton.onClick.Invoke());
                Assert.That(view.IsDisposed, Is.True);
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        [Test]
        public void ReentrantRouteChoiceCannotReplaceOriginalDecision()
        {
            var root = new GameObject("M14 reentrant route choice");
            M14FinalPassageView view = null;
            bool nested = false;
            try
            {
                var session = CreateVerifiedSession();
                view = new M14FinalPassageView(root.transform, session, () =>
                {
                    if (!nested)
                    {
                        nested = true;
                        view.WithdrawLuJunButton.onClick.Invoke();
                    }
                    return true;
                }, () => true, () => false, () => false, () => false,
                    () => new M14CivilianCounts(16, 4, 2), _ => { });
                view.DetainLuJunButton.onClick.Invoke();
                Assert.That(nested, Is.True);
                Assert.That(session.LuJunDisposition, Is.EqualTo(M14LuJunDisposition.Detained));
                Assert.That(session.Phase, Is.EqualTo(M14Phase.CivilianExitVerification));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RetiredRouteViewCannotApplyProofOrReadNextCredential()
        {
            var root = new GameObject("M14 retired route choice");
            M14FinalPassageView view = null;
            int guaranteeReads = 0;
            try
            {
                var session = CreateVerifiedSession();
                view = new M14FinalPassageView(root.transform, session, () =>
                {
                    view.Dispose();
                    return true;
                }, () => { guaranteeReads++; return true; }, () => false,
                    () => false, () => false, () => new M14CivilianCounts(16, 4, 2), _ => { });
                Assert.DoesNotThrow(() => view.DetainLuJunButton.onClick.Invoke());
                Assert.That(view.IsDisposed, Is.True);
                Assert.That(guaranteeReads, Is.Zero);
                Assert.That(session.Phase, Is.EqualTo(M14Phase.RouteExecution));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReentrantCountsCannotReplaceOriginalCensusAndExceptionAllowsRetry()
        {
            var root = new GameObject("M14 reentrant counts");
            M14FinalPassageView view = null;
            bool throwOnce = false;
            bool nested = false;
            try
            {
                var session = CreateVerifiedSession();
                view = new M14FinalPassageView(root.transform, session, () => true,
                    () => true, () => false, () => false, () => false, () =>
                    {
                        if (throwOnce)
                        {
                            throwOnce = false;
                            throw new InvalidOperationException("census read failed");
                        }
                        if (nested)
                            return new M14CivilianCounts(99, 0, 0);
                        if (session.Phase == M14Phase.CivilianExitVerification &&
                            view.VerifyCivilianExitButton.gameObject.activeSelf)
                        {
                            nested = true;
                            view.VerifyCivilianExitButton.onClick.Invoke();
                        }
                        return new M14CivilianCounts(16, 4, 2);
                    }, _ => { });
                view.DetainLuJunButton.onClick.Invoke();
                throwOnce = true;
                Assert.Throws<InvalidOperationException>(() => view.VerifyCivilianExitButton.onClick.Invoke());
                Assert.That(session.Phase, Is.EqualTo(M14Phase.CivilianExitVerification));
                view.VerifyCivilianExitButton.onClick.Invoke();
                Assert.That(nested, Is.True);
                Assert.That(session.Phase, Is.EqualTo(M14Phase.AyuanPositionResolution));
                Assert.That(session.VerifiedDepartingCount, Is.EqualTo(16));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedControlDuringCensusReadCannotAdvanceOldSession()
        {
            var root = new GameObject("M14 destroyed census choice");
            M14FinalPassageView view = null;
            bool destroy = false;
            try
            {
                var session = CreateVerifiedSession();
                view = new M14FinalPassageView(root.transform, session, () => true,
                    () => true, () => false, () => false, () => false, () =>
                    {
                        if (destroy)
                            UnityEngine.Object.DestroyImmediate(view.VerifyCivilianExitButton.gameObject);
                        return new M14CivilianCounts(16, 4, 2);
                    }, _ => { });
                view.DetainLuJunButton.onClick.Invoke();
                destroy = true;
                Assert.DoesNotThrow(() => view.VerifyCivilianExitButton.onClick.Invoke());
                Assert.That(view.IsDisposed, Is.True);
                Assert.That(session.Phase, Is.EqualTo(M14Phase.CivilianExitVerification));
                Assert.That(session.CivilianExitVerified, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposedDuringCensusRefreshDoesNotAccessDestroyedControls()
        {
            var root = new GameObject("M14 disposed census refresh");
            M14FinalPassageView view = null;
            try
            {
                var session = CreateVerifiedSession();
                view = new M14FinalPassageView(root.transform, session, () => true,
                    () => true, () => false, () => false, () => false, () =>
                    {
                        view.Dispose();
                        return new M14CivilianCounts(16, 4, 2);
                    }, _ => { });
                Assert.DoesNotThrow(() => view.DetainLuJunButton.onClick.Invoke());
                Assert.That(view.IsDisposed, Is.True);
                Assert.That(session.Phase, Is.EqualTo(M14Phase.CivilianExitVerification));
                Assert.DoesNotThrow(() => view.Refresh());
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
