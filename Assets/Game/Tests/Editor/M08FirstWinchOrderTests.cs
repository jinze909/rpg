using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M08FirstWinchOrderTests
    {
        [Test]
        public void WorkshopOrderRequiresBothRealPartsAndNeverDuplicatesThem()
        {
            var session = new M08FirstWinchOrderSession();
            Assert.That(session.TryAssemble(), Is.False);
            Assert.That(session.TryAcquirePart((M08WinchPart)99), Is.False);
            Assert.That(session.TryAcquirePart(M08WinchPart.DockCable), Is.True);
            Assert.That(session.TryAcquirePart(M08WinchPart.DockCable), Is.False);
            Assert.That(session.TryTestLift(), Is.False);
            Assert.That(session.Stage, Is.EqualTo(M08WinchOrderStage.SourceParts));
            Assert.That(session.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
            Assert.That(session.Stage, Is.EqualTo(M08WinchOrderStage.Assemble));
            Assert.That(session.TryAcquirePart(M08WinchPart.ScrapGear), Is.False);
            Assert.That(session.WorkshopDepositEarned, Is.False);
        }

        [Test]
        public void FailedLiftIsRecoverableWithoutDuplicateRewards()
        {
            var session = ReadyToTest();
            Assert.That(session.TryTestLift(), Is.True);
            Assert.That(session.Stage, Is.EqualTo(M08WinchOrderStage.RepairLooseCable));
            Assert.That(session.RecoverableFaults, Is.EqualTo(1));
            Assert.That(session.TestAttempts, Is.EqualTo(1));
            Assert.That(session.WorkshopDepositEarned, Is.False);
            Assert.That(session.TryTestLift(), Is.False);
            Assert.That(session.TryRepairLooseCable(), Is.True);
            Assert.That(session.CableTension, Is.EqualTo(
                M08FirstWinchOrderSession.MinimumSafeTension));
            Assert.That(session.TryTestLift(), Is.True);
            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.TestAttempts, Is.EqualTo(2));
            Assert.That(session.TryTestLift(), Is.False);
            Assert.That(session.TryRepairLooseCable(), Is.False);
            Assert.That(session.TestAttempts, Is.EqualTo(2));
        }

        [Test]
        public void InspectionBeforeLiftCanSucceedOnFirstAttempt()
        {
            var session = ReadyToTest();
            Assert.That(session.TryTightenCable(), Is.True);
            Assert.That(session.TryTightenCable(), Is.False);
            Assert.That(session.TryTestLift(), Is.True);
            Assert.That(session.Stage, Is.EqualTo(M08WinchOrderStage.Completed));
            Assert.That(session.RecoverableFaults, Is.Zero);
            Assert.That(session.TestAttempts, Is.EqualTo(1));
        }

        [Test]
        public void ViewDisplaysLocalizedStepsAndRetriesFailedHostReward()
        {
            var root = new GameObject("M08 winch UI test");
            var session = new M08FirstWinchOrderSession();
            M08FirstWinchOrderView view = null;
            int deliveries = 0;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                view = new M08FirstWinchOrderView(root.transform, session, completed =>
                {
                    Assert.That(completed.WorkshopDepositEarned, Is.True);
                    deliveries++;
                    if (deliveries == 1)
                        throw new InvalidOperationException("Host interrupted before save commit");
                });
                Assert.That(root.transform.childCount, Is.EqualTo(1));
                Assert.That(view.FinishButton.interactable, Is.False);
                view.ThirdButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M08WinchOrderStage.SourceParts));
                view.PrimaryButton.onClick.Invoke();
                view.SecondaryButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M08WinchOrderStage.Assemble));
                view.PrimaryButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M08WinchOrderStage.TestLift));
                view.PrimaryButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M08WinchOrderStage.RepairLooseCable));
                Assert.That(session.WorkshopDepositEarned, Is.False);
                view.PrimaryButton.onClick.Invoke();
                view.PrimaryButton.onClick.Invoke();
                Assert.That(session.IsCompleted, Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    view.FinishButton.onClick.Invoke());
                Assert.That(view.FinishButton.interactable, Is.True);
                view.FinishButton.onClick.Invoke();
                Assert.That(deliveries, Is.EqualTo(2));
                view.FinishButton.onClick.Invoke();
                Assert.That(deliveries, Is.EqualTo(2));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                view.Refresh();
                Assert.That(view.Body.text, Does.Contain("完工"));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            }
        }

        private static M08FirstWinchOrderSession ReadyToTest()
        {
            var session = new M08FirstWinchOrderSession();
            Assert.That(session.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
            Assert.That(session.TryAcquirePart(M08WinchPart.DockCable), Is.True);
            Assert.That(session.TryAssemble(), Is.True);
            return session;
        }
    }
}
