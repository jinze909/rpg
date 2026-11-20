using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M08DockTruthTests
    {
        [Test]
        public void AbsentShenOrUnfinishedCommissionNeverStartsNightDockConversation()
        {
            foreach (bool present in new[] { false, true })
            foreach (bool winch in new[] { false, true })
            {
                var session = new M08DockTruthSession(present, winch);
                Assert.That(session.CanConverse, Is.EqualTo(present && winch));
                Assert.That(session.TryHearTruth(), Is.EqualTo(present && winch));
            }
        }

        [Test]
        public void AllThreeRepliesAreDistinctAndCannotBeOverwritten()
        {
            foreach (M08DockTruthResponse choice in new[] {
                M08DockTruthResponse.AngerAtBeingShielded,
                M08DockTruthResponse.StaySilent,
                M08DockTruthResponse.AdmitSharedFear })
            {
                var session = new M08DockTruthSession(true, true);
                Assert.That(session.TryRespond(choice), Is.False);
                Assert.That(session.TryAcknowledge(_ => true), Is.False);
                Assert.That(session.TryHearTruth(), Is.True);
                Assert.That(session.TryHearTruth(), Is.False);
                Assert.That(session.TryRespond((M08DockTruthResponse)99), Is.False);
                Assert.That(session.TryRespond(choice), Is.True);
                Assert.That(session.SelectedResponse, Is.EqualTo(choice));
                Assert.That(session.TryRespond(M08DockTruthResponse.StaySilent), Is.False);
                Assert.That(session.TryAcknowledge(null), Is.False);
                M08DockTruthResponse recorded = 0;
                Assert.That(session.TryAcknowledge(response => {
                    recorded = response;
                    return true;
                }), Is.True);
                Assert.That(recorded, Is.EqualTo(choice));
                Assert.That(session.IsCompleted, Is.True);
                Assert.That(session.TryAcknowledge(_ => true), Is.False);
            }
        }

        [Test]
        public void RefusedAndInterruptedHostWriteCanRetryTheSameResponse()
        {
            var session = new M08DockTruthSession(true, true);
            session.TryHearTruth();
            session.TryRespond(M08DockTruthResponse.AdmitSharedFear);
            int calls = 0;
            Assert.That(session.TryAcknowledge(_ => { calls++; return false; }), Is.False);
            Assert.Throws<InvalidOperationException>(() =>
                session.TryAcknowledge(_ => {
                    calls++;
                    throw new InvalidOperationException("Makinom write interrupted");
                }));
            Assert.That(session.Stage, Is.EqualTo(M08DockTruthStage.AwaitingHost));
            Assert.That(session.SelectedResponse,
                Is.EqualTo(M08DockTruthResponse.AdmitSharedFear));
            Assert.That(session.TryAcknowledge(_ => { calls++; return true; }), Is.True);
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public void LocalizedDockViewOffersCanonChoicesAndRetriesRefusedSave()
        {
            var root = new GameObject("M08 night dock view test");
            var session = new M08DockTruthSession(true, true);
            M08DockTruthView view = null;
            int writes = 0;
            string previousLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                view = new M08DockTruthView(root.transform, session, response => {
                    Assert.That(response,
                        Is.EqualTo(M08DockTruthResponse.AngerAtBeingShielded));
                    return ++writes > 1;
                });
                Assert.That(view.Body.text, Does.Contain("断缆"));
                Assert.That(view.HearButton.interactable, Is.True);
                Assert.That(view.FinishButton.interactable, Is.False);
                view.HearButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M08DockTruthStage.AwaitingResponse));
                Assert.That(view.Body.text, Does.Contain("工兵刀"));
                view.FirstResponseButton.onClick.Invoke();
                Assert.That(session.SelectedResponse,
                    Is.EqualTo(M08DockTruthResponse.AngerAtBeingShielded));
                Assert.That(view.Body.text, Does.Contain("往后我不替你瞒了"));
                view.FinishButton.onClick.Invoke();
                Assert.That(session.IsCompleted, Is.False);
                Assert.That(view.FinishButton.interactable, Is.True);
                view.FinishButton.onClick.Invoke();
                Assert.That(session.IsCompleted, Is.True);
                Assert.That(writes, Is.EqualTo(2));
                view.FinishButton.onClick.Invoke();
                Assert.That(writes, Is.EqualTo(2));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(view.Body.text, Does.Contain("Neither of you"));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                SoundwellUiLocalization.SetLanguage(previousLanguage);
            }
        }

        [Test]
        public void AbsentShenCannotOpenVisualsOrImplyReconciliation()
        {
            var root = new GameObject("M08 absent Shen test");
            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                    new M08DockTruthView(root.transform,
                        new M08DockTruthSession(false, true), _ => true));
                Assert.Throws<InvalidOperationException>(() =>
                    new M08DockTruthView(root.transform,
                        new M08DockTruthSession(true, false), _ => true));
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
