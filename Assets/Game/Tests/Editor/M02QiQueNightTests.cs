using System;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M02QiQueNightTests
    {
        [Test]
        public void KnowingDebtTriggersTheMortgagedShipAdmission()
        {
            var night = new M02QiQueNight(true);
            night.Begin();
            night.Greet();
            Assert.That(night.Stage, Is.EqualTo(M02QiQueNightStage.ConfrontingDebt));
            Assert.That(M02QiQueNightDialogue.Describe(night, true),
                Does.Contain("船还押在他管的账下"));
            night.DiscussDebt();
            Assert.That(night.DebtConfronted, Is.True);
            Assert.That(night.Stage, Is.EqualTo(M02QiQueNightStage.OfferingCake));
            night.AcceptCake();
            Assert.That(night.ChartStudied, Is.True);
            night.Leave();
            Assert.That(night.Stage, Is.EqualTo(M02QiQueNightStage.Finished));
        }

        [Test]
        public void MissingDebtDiscoveryNeverInventsTheConfrontation()
        {
            var night = new M02QiQueNight(false);
            night.Begin();
            night.Greet();
            Assert.That(night.Stage, Is.EqualTo(M02QiQueNightStage.OfferingCake));
            Assert.That(night.DebtConfronted, Is.False);
            Assert.Throws<InvalidOperationException>(() => night.DiscussDebt());
            night.AcceptCake();
            Assert.That(M02QiQueNightDialogue.Describe(night, false),
                Does.Contain("not booked a ship"));
            night.Leave();
            Assert.That(night.DebtAlreadyKnown, Is.False);
            Assert.That(night.DebtConfronted, Is.False);
        }

        [Test]
        public void InvalidOrRepeatedTransitionsCannotSkipPrerequisites()
        {
            var night = new M02QiQueNight(true);
            Assert.Throws<InvalidOperationException>(() => night.Greet());
            Assert.Throws<InvalidOperationException>(() => night.Leave());
            night.Begin();
            Assert.Throws<InvalidOperationException>(() => night.Begin());
            Assert.Throws<InvalidOperationException>(() => night.AcceptCake());
            night.Greet();
            Assert.Throws<InvalidOperationException>(() => night.Leave());
            night.DiscussDebt();
            night.AcceptCake();
            night.Leave();
            Assert.Throws<InvalidOperationException>(() => night.Leave());
        }

        [Test]
        public void NeitherBranchBooksPassageOrClaimsPensionApproval()
        {
            foreach (bool debtKnown in new[] { true, false })
            {
                var night = new M02QiQueNight(debtKnown);
                night.Begin();
                night.Greet();
                if (debtKnown)
                    night.DiscussDebt();
                night.AcceptCake();
                Assert.That(M02QiQueNightDialogue.Describe(night, true),
                    Does.Contain("还没订船或确定航路"));
                night.Leave();
            }
        }
    }
}
