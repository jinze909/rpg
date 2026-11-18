using System;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M02HarborDeskTests
    {
        [Test]
        public void M01PublicIntentDoesNotStampOfficialRegistrationByItself()
        {
            var desk = new M02HarborDesk(M01PensionChoice.RegisterLivingIdentity);
            Assert.That(desk.LivingRegistryStamped, Is.False);
            Assert.That(desk.CanPresentLivingPapers, Is.True);
            desk.Begin();
            Assert.That(desk.LivingRegistryStamped, Is.False);
            desk.Act(M02HarborDeskAction.PresentLivingRegistration);
            Assert.That(desk.LivingRegistryStamped, Is.True);
            Assert.That(desk.CanPresentLivingPapers, Is.False);
            var outcome = desk.Finish();
            Assert.That(outcome.OfficialShipOffered, Is.True);
        }

        [TestCase(M01PensionChoice.KeepIdentityUnregistered)]
        [TestCase(M01PensionChoice.RequestMissingReturnReview)]
        [TestCase(M01PensionChoice.LeaveWithoutDecision)]
        public void WithoutPublicChoiceOfficialRegistrationIsNotGranted(
            M01PensionChoice prior)
        {
            var desk = new M02HarborDesk(prior);
            desk.Begin();
            Assert.That(desk.CanPresentLivingPapers, Is.False);
            Assert.Throws<InvalidOperationException>(() =>
                desk.Act(M02HarborDeskAction.PresentLivingRegistration));
            Assert.That(desk.Finish().OfficialShipOffered, Is.False);
        }

        [Test]
        public void DebtIsDiscoveredOnlyWhenPlayerAsksAboutQiQueShip()
        {
            var desk = new M02HarborDesk(M01PensionChoice.LeaveWithoutDecision);
            desk.Begin();
            desk.Act(M02HarborDeskAction.AskAboutLuJunEscort);
            Assert.That(desk.QiQueDebtDiscovered, Is.False);
            Assert.That(desk.Act(M02HarborDeskAction.AskAboutQiQueShip), Is.True);
            Assert.That(desk.Act(M02HarborDeskAction.AskAboutQiQueShip), Is.False);
            Assert.That(desk.Finish().QiQueDebtDiscovered, Is.True);
            Assert.That(M02HarborDeskDialogue.Result(
                M02HarborDeskAction.AskAboutQiQueShip, true), Does.Contain("下个月初五"));
        }

        [Test]
        public void TransferAssistanceDoesNotAutomaticallyApproveMissingReturn()
        {
            var desk = new M02HarborDesk(M01PensionChoice.RequestMissingReturnReview);
            desk.Begin();
            desk.Act(M02HarborDeskAction.AssistTransferList);
            var result = desk.Finish();
            Assert.That(result.TransferAssistanceAccepted, Is.True);
            Assert.That(result.LivingRegistryStamped, Is.False);
            Assert.That(result.OfficialShipOffered, Is.False);
            Assert.That(M02HarborDeskDialogue.Result(
                M02HarborDeskAction.AssistTransferList, false),
                Does.Contain("does not approve"));
        }

        [Test]
        public void SkippingAllOptionalDialogueLeavesNoInventedFacts()
        {
            var desk = new M02HarborDesk(M01PensionChoice.RegisterLivingIdentity);
            desk.Begin();
            var result = desk.Finish();
            Assert.That(result.LivingRegistryStamped, Is.False);
            Assert.That(result.QiQueDebtDiscovered, Is.False);
            Assert.That(result.TransferAssistanceAccepted, Is.False);
        }

        [Test]
        public void InvalidActionsAndCompletedSessionCannotMutateHistory()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new M02HarborDesk((M01PensionChoice)99));
            var desk = new M02HarborDesk(M01PensionChoice.RegisterLivingIdentity);
            Assert.Throws<InvalidOperationException>(() =>
                desk.Act(M02HarborDeskAction.AskAboutQiQueShip));
            desk.Begin();
            Assert.Throws<InvalidOperationException>(() => desk.Begin());
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                desk.Act((M02HarborDeskAction)99));
            desk.Finish();
            Assert.Throws<InvalidOperationException>(() =>
                desk.Act(M02HarborDeskAction.PresentLivingRegistration));
            Assert.Throws<InvalidOperationException>(() => desk.Finish());
        }
    }
}
