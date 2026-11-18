using System;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M01DockBriefingTests
    {
        [Test]
        public void EveryOpeningReplyLeadsToCakeBeforeTheRouteMap()
        {
            // Cover all three branches without adding unmatched parameterized
            // case names to the exact Unity test acceptance manifest.
            foreach (M01DockReply reply in new[]
            {
                M01DockReply.AskAboutOldDebt,
                M01DockReply.TeaseTheUglyCup,
                M01DockReply.AskForShipToRedSalt
            })
            {
                var briefing = new M01DockBriefing();
                briefing.Begin();
                briefing.Reply(reply);
                Assert.That(briefing.Stage, Is.EqualTo(M01DockStage.OfferingCake));
                Assert.That(briefing.OpeningReply, Is.EqualTo(reply));
                Assert.That(briefing.InspectedLead, Is.Null);
                Assert.That(M01DockBriefingDialogue.Describe(briefing, true).Text,
                    Does.Contain("桂花糕"), "reply: " + reply);
                Assert.That(M01DockBriefingDialogue.Describe(briefing, false).Text,
                    Does.Contain("owes the shipyard for repairs"), "reply: " + reply);
                briefing.ContinueAfterCake();
                Assert.That(briefing.Stage, Is.EqualTo(M01DockStage.ReviewingRoutes));
            }
        }

        [Test]
        public void AllThreeLeadsCanBeComparedWithoutLockingThePlayerIntoOne()
        {
            var briefing = AtRoutes();
            foreach (M01VoyageLead lead in new[]
            {
                M01VoyageLead.HanQiaoOfficialDock,
                M01VoyageLead.LuJunOldGuard,
                M01VoyageLead.QiQueMerchantRoute
            })
            {
                Assert.That(briefing.Inspect(lead).Lead, Is.EqualTo(lead));
                Assert.That(briefing.Stage, Is.EqualTo(M01DockStage.ReviewingRoutes));
                Assert.That(M01DockBriefingDialogue.Describe(briefing, false).Text,
                    Does.Contain(M01DockBriefing.TermsFor(lead).Describe(false)));
            }
            briefing.EndConversation();
            Assert.That(briefing.Finished, Is.True);
            Assert.That(M01DockBriefingDialogue.Describe(briefing, false).Finished, Is.True);
        }

        [Test]
        public void OfficialPapersRequireApprovedLivingRegistryNotJustM01Intent()
        {
            var official = M01DockBriefing.TermsFor(M01VoyageLead.HanQiaoOfficialDock);
            Assert.That(official.NeedsLivingRegistration, Is.True);
            Assert.That(official.RequiresPublicBrigadeFlag, Is.False);
            Assert.That(official.Describe(false), Does.Contain("not official approval"));
        }

        [Test]
        public void OldGuardFlagIsPublicAndMerchantShipIsLimited()
        {
            var guard = M01DockBriefing.TermsFor(M01VoyageLead.LuJunOldGuard);
            var merchant = M01DockBriefing.TermsFor(M01VoyageLead.QiQueMerchantRoute);
            Assert.That(guard.RequiresPublicBrigadeFlag, Is.True);
            Assert.That(guard.HasSmallCapacity, Is.False);
            Assert.That(guard.Describe(true), Does.Contain("公开挂第三旅旧军旗"));
            Assert.That(merchant.NeedsLivingRegistration, Is.False);
            Assert.That(merchant.HasSmallCapacity, Is.True);
            Assert.That(merchant.Describe(true), Does.Contain("十几个人"));
            Assert.That(merchant.Describe(false), Does.Contain("repairs"));
        }

        [Test]
        public void BothLanguagesExposeThreeRouteChoicesWithoutAwardingPassage()
        {
            var briefing = AtRoutes();
            var cn = M01DockBriefingDialogue.Describe(briefing, true);
            var en = M01DockBriefingDialogue.Describe(briefing, false);
            Assert.That(cn.Choices, Has.Length.EqualTo(3));
            Assert.That(en.Choices, Has.Length.EqualTo(3));
            Assert.That(cn.Text, Does.Contain("没有一条船"));
            Assert.That(en.Text, Does.Contain("no passage has been secured"));
            Assert.That(briefing.InspectedLead, Is.Null);
            Assert.That(cn.CanContinue, Is.True);
        }

        [Test]
        public void IllegalTransitionsAndInvalidValuesCannotBypassTheConversation()
        {
            var briefing = new M01DockBriefing();
            Assert.Throws<InvalidOperationException>(() => briefing.Reply(M01DockReply.AskAboutOldDebt));
            Assert.Throws<InvalidOperationException>(() => briefing.ContinueAfterCake());
            Assert.Throws<InvalidOperationException>(() => briefing.Inspect(M01VoyageLead.LuJunOldGuard));
            briefing.Begin();
            Assert.Throws<InvalidOperationException>(() => briefing.Begin());
            Assert.Throws<ArgumentOutOfRangeException>(() => briefing.Reply((M01DockReply)99));
            Assert.That(briefing.Stage, Is.EqualTo(M01DockStage.AwaitingOpeningReply));
            briefing.Reply(M01DockReply.TeaseTheUglyCup);
            briefing.ContinueAfterCake();
            Assert.Throws<ArgumentOutOfRangeException>(() => briefing.Inspect((M01VoyageLead)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => M01DockBriefing.TermsFor((M01VoyageLead)99));
            briefing.EndConversation();
            Assert.Throws<InvalidOperationException>(() => briefing.Inspect(M01VoyageLead.QiQueMerchantRoute));
            Assert.Throws<InvalidOperationException>(() => briefing.EndConversation());
        }

        private static M01DockBriefing AtRoutes()
        {
            var briefing = new M01DockBriefing();
            briefing.Begin();
            briefing.Reply(M01DockReply.AskForShipToRedSalt);
            briefing.ContinueAfterCake();
            return briefing;
        }
    }
}
