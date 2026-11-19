using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M03LuJunEncounterTests
    {
        [Test]
        public void AskingAboutBridgeWithEvidenceRevealsConfessionThenSecrecyRequest()
        {
            var encounter = new M03LuJunEncounter(
                M02BridgeEvidence.FerrymanLog | M02BridgeEvidence.ThirdBellSignal, true);
            encounter.Begin();
            encounter.ObserveRollCall();
            encounter.Ask(M03LuJunOpening.AskWhoCutBridge);
            Assert.That(encounter.Confession, Is.EqualTo(M03LuConfession.AskedWithEvidence));
            Assert.That(encounter.Stage, Is.EqualTo(M03LuJunStage.RespondingToConfession));
            Assert.That(M03LuJunCopy.Narration(encounter, true),
                Does.Contain("一万四千").And.Contain("十二年没睡好"));
            Assert.That(M03LuJunCopy.Narration(encounter, false),
                Does.Contain("The Third Brigade was to hold its ground").And.Contain("line up and surrender"));
            encounter.Respond(M03ConfessionResponse.YouChoseForThem);
            Assert.That(encounter.Stage, Is.EqualTo(M03LuJunStage.ReplyingToSecrecyRequest));
            encounter.AnswerSecrecy(M03SecrecyAnswer.RefuseSecrecy);
            encounter.ChooseVoyage(M03VoyageChoice.HanQiaoOfficial);
            Assert.That(encounter.VoyagePathValue, Is.EqualTo("韩峤船坞"));
            Assert.That(encounter.Secrecy, Is.EqualTo(M03SecrecyAnswer.RefuseSecrecy));
        }

        [Test]
        public void AskingWithoutEvidenceIsEarlyButDoesNotRequirePriorClues()
        {
            var encounter = new M03LuJunEncounter(M02BridgeEvidence.None, false);
            encounter.Begin();
            encounter.ObserveRollCall();
            encounter.Ask(M03LuJunOpening.AskWhoCutBridge);
            Assert.That(encounter.Confession, Is.EqualTo(M03LuConfession.EarlyQuestion));
            encounter.Respond(M03ConfessionResponse.RemainSilent);
            encounter.AnswerSecrecy(M03SecrecyAnswer.Undecided);
            encounter.ChooseVoyage(M03VoyageChoice.LuJunEscort);
            Assert.That(encounter.VoyagePathValue, Is.EqualTo("陆钧护航"));
            Assert.That(encounter.Secrecy, Is.EqualTo(M03SecrecyAnswer.Undecided));
        }

        [Test]
        public void AskingForShipOrNameNeverForcesBridgeSecretOrSecrecy()
        {
            foreach (M03LuJunOpening opening in new[]
            {
                M03LuJunOpening.FindMyName,
                M03LuJunOpening.NeedAShip
            })
            {
                var encounter = new M03LuJunEncounter(M02BridgeEvidence.WarReportDraft, false);
                encounter.Begin();
                encounter.ObserveRollCall();
                encounter.Ask(opening);
                Assert.That(encounter.Confession, Is.EqualTo(M03LuConfession.NotAsked));
                Assert.That(encounter.Secrecy, Is.EqualTo(M03SecrecyAnswer.NotTriggered));
                Assert.That(encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));
                Assert.Throws<InvalidOperationException>(() =>
                    encounter.AnswerSecrecy(M03SecrecyAnswer.PromiseSecrecy));
                encounter.ChooseVoyage(M03VoyageChoice.QiQueMerchant);
                Assert.That(encounter.VoyagePathValue, Is.EqualTo("祁雀商路"));
            }
        }

        [Test]
        public void NameAndShipOpeningsHaveDistinctAuthoredRepliesWithoutInventedConfession()
        {
            var names = new M03LuJunEncounter(M02BridgeEvidence.WarReportDraft, false);
            names.Begin();
            names.ObserveRollCall();
            names.Ask(M03LuJunOpening.FindMyName);
            Assert.That(M03LuJunCopy.Narration(names, true), Does.Contain("我没划"));
            Assert.That(M03LuJunCopy.Narration(names, false), Does.Contain("I never struck"));
            Assert.That(M03LuJunCopy.Narration(names, false), Does.Not.Contain("tideway"));
            Assert.That(names.Confession, Is.EqualTo(M03LuConfession.NotAsked));
            Assert.That(names.Secrecy, Is.EqualTo(M03SecrecyAnswer.NotTriggered));

            var passage = new M03LuJunEncounter(M02BridgeEvidence.None, false);
            passage.Begin();
            passage.ObserveRollCall();
            passage.Ask(M03LuJunOpening.NeedAShip);
            Assert.That(M03LuJunCopy.Narration(passage, true), Does.Contain("护卫"));
            Assert.That(M03LuJunCopy.Narration(passage, false), Does.Contain("tideway chart"));
            Assert.That(M03LuJunCopy.Narration(passage, false), Does.Not.Contain("I never struck"));
            Assert.That(passage.Confession, Is.EqualTo(M03LuConfession.NotAsked));
            Assert.That(passage.Secrecy, Is.EqualTo(M03SecrecyAnswer.NotTriggered));
            Assert.That(passage.CanChoose(M03VoyageChoice.HanQiaoOfficial), Is.False);
            passage.ChooseVoyage(M03VoyageChoice.QiQueMerchant);
            Assert.That(passage.VoyagePathValue, Is.EqualTo("祁雀商路"));
        }

        [Test]
        public void NoStampedOfficialRegistrationCannotGrantAnOfficialShip()
        {
            var encounter = new M03LuJunEncounter(M02BridgeEvidence.None, false);
            encounter.Begin();
            encounter.ObserveRollCall();
            encounter.Ask(M03LuJunOpening.NeedAShip);
            foreach (M03VoyageChoice official in new[]
            {
                M03VoyageChoice.HanQiaoOfficial,
                M03VoyageChoice.MixedWithOfficial
            })
            {
                Assert.That(encounter.CanChoose(official), Is.False);
                Assert.Throws<InvalidOperationException>(() =>
                    encounter.ChooseVoyage(official));
            }
            Assert.That(encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));
            encounter.ChooseVoyage(M03VoyageChoice.MixedWithCivilian);
            Assert.That(encounter.VoyagePathValue, Is.EqualTo("混合"));
            Assert.That(encounter.Confession, Is.EqualTo(M03LuConfession.NotAsked));
        }

        [Test]
        public void StampedRegistrationDoesNotForceOfficialRouteOrPublicFlag()
        {
            var encounter = new M03LuJunEncounter(M02BridgeEvidence.None, true);
            encounter.Begin();
            encounter.ObserveRollCall();
            encounter.Ask(M03LuJunOpening.FindMyName);
            Assert.That(encounter.CanChoose(M03VoyageChoice.MixedWithOfficial), Is.True);
            encounter.ChooseVoyage(M03VoyageChoice.MixedWithOfficial);
            Assert.That(encounter.VoyagePathValue, Is.EqualTo("混合"));
        }

        [Test]
        public void ConfessionReactionsGetDistinctBilingualRepliesBeforeSecrecy()
        {
            var reactions = new[]
            {
                M03ConfessionResponse.YouShouldHaveToldThem,
                M03ConfessionResponse.YouChoseForThem,
                M03ConfessionResponse.RemainSilent
            };
            var zh = new[] { "我每晚都在想", "还在替他们决定", "没有催你" };
            var en = new[] { "why I waited", "still deciding for them", "without speaking" };

            for (int i = 0; i < reactions.Length; i++)
            {
                var encounter = new M03LuJunEncounter(M02BridgeEvidence.None, false);
                encounter.Begin();
                encounter.ObserveRollCall();
                encounter.Ask(M03LuJunOpening.AskWhoCutBridge);
                encounter.Respond(reactions[i]);
                Assert.That(encounter.Stage, Is.EqualTo(M03LuJunStage.ReplyingToSecrecyRequest));
                Assert.That(encounter.Secrecy, Is.EqualTo(M03SecrecyAnswer.NotTriggered));
                Assert.That(M03LuJunCopy.Narration(encounter, true),
                    Does.Contain(zh[i]).And.Contain("别告诉他们"));
                Assert.That(M03LuJunCopy.Narration(encounter, false),
                    Does.Contain(en[i]).And.Contain("Please don't tell them"));
            }
        }

        [Test]
        public void SecrecyChoicesGetDistinctBilingualFarewellsWithoutGrantingOfficialPassage()
        {
            var answers = new[]
            {
                M03SecrecyAnswer.PromiseSecrecy,
                M03SecrecyAnswer.Undecided,
                M03SecrecyAnswer.RefuseSecrecy
            };
            var zh = new[] { "你什么也不欠我", "决定了告诉我", "如果你决定说" };
            var en = new[] { "owe me nothing", "when you decide", "If you decide to tell them" };

            for (int i = 0; i < answers.Length; i++)
            {
                var encounter = new M03LuJunEncounter(M02BridgeEvidence.None, false);
                encounter.Begin();
                encounter.ObserveRollCall();
                encounter.Ask(M03LuJunOpening.AskWhoCutBridge);
                encounter.Respond(M03ConfessionResponse.RemainSilent);
                encounter.AnswerSecrecy(answers[i]);
                Assert.That(encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));
                Assert.That(M03LuJunCopy.Narration(encounter, true),
                    Does.Contain(zh[i]).And.Contain("官船必须已有正式盖印"));
                Assert.That(M03LuJunCopy.Narration(encounter, false),
                    Does.Contain(en[i]).And.Contain("Official passage requires"));
                Assert.That(encounter.CanChoose(M03VoyageChoice.HanQiaoOfficial), Is.False);
            }
        }

        [Test]
        public void InterruptedM03WriteCannotExposePreviousVoyageOrPartialConfession()
        {
            var store = new Dictionary<string, int>();
            Action<string, int> save = (key, value) => store[key] = value;
            Func<string, int> read = key =>
                store.TryGetValue(key, out int value) ? value : 0;

            var previous = new M03LuJunEncounter(M02BridgeEvidence.None, true);
            previous.Begin();
            previous.ObserveRollCall();
            previous.Ask(M03LuJunOpening.NeedAShip);
            previous.ChooseVoyage(M03VoyageChoice.HanQiaoOfficial);
            Assert.That(M03LuJunFacts.TryRecord(previous, save), Is.True);
            Assert.That(M03LuJunFacts.TryRead(read, out M03LuConfession oldConfession,
                out M03SecrecyAnswer oldSecrecy, out M03VoyageChoice? oldRoute), Is.True);
            Assert.That(oldRoute, Is.EqualTo(M03VoyageChoice.HanQiaoOfficial));

            var changed = new M03LuJunEncounter(M02BridgeEvidence.WarReportDraft, false);
            changed.Begin();
            changed.ObserveRollCall();
            changed.Ask(M03LuJunOpening.AskWhoCutBridge);
            changed.Respond(M03ConfessionResponse.RemainSilent);
            changed.AnswerSecrecy(M03SecrecyAnswer.RefuseSecrecy);
            changed.ChooseVoyage(M03VoyageChoice.QiQueMerchant);
            Assert.Throws<InvalidOperationException>(() =>
                M03LuJunFacts.TryRecord(changed, (key, value) =>
                {
                    if (key == M03LuJunFacts.SecrecyKey)
                        throw new InvalidOperationException("Simulated interrupted M03 write");
                    save(key, value);
                }));

            Assert.That(store[M03LuJunFacts.VoyageKey], Is.Zero);
            Assert.That(M03LuJunFacts.TryRead(read, out M03LuConfession partialConfession,
                out M03SecrecyAnswer partialSecrecy, out M03VoyageChoice? partialRoute), Is.True);
            Assert.That(partialRoute, Is.Null);
            Assert.That(partialConfession, Is.EqualTo(M03LuConfession.NotAsked));
            Assert.That(partialSecrecy, Is.EqualTo(M03SecrecyAnswer.NotTriggered));

            var keys = new List<string>();
            Assert.That(M03LuJunFacts.TryRecord(changed, (key, value) =>
            {
                keys.Add(key);
                save(key, value);
            }), Is.True);
            Assert.That(keys[0], Is.EqualTo(M03LuJunFacts.VoyageKey));
            Assert.That(keys[keys.Count - 1], Is.EqualTo(M03LuJunFacts.VoyageKey));
            Assert.That(M03LuJunFacts.TryRead(read, out M03LuConfession confession,
                out M03SecrecyAnswer secrecy, out M03VoyageChoice? route), Is.True);
            Assert.That(confession, Is.EqualTo(M03LuConfession.AskedWithEvidence));
            Assert.That(secrecy, Is.EqualTo(M03SecrecyAnswer.RefuseSecrecy));
            Assert.That(route, Is.EqualTo(M03VoyageChoice.QiQueMerchant));
        }

        [Test]
        public void CorruptSecrecyAndConfessionCombinationsCannotBecomeSavedStoryHistory()
        {
            var saved = new Dictionary<string, int>();
            Action<string, int> write = (key, value) => saved[key] = value;

            void RejectPair(int confessionCode, int secrecyCode)
            {
                var corrupted = new Dictionary<string, int>(saved)
                {
                    [M03LuJunFacts.ConfessionKey] = confessionCode,
                    [M03LuJunFacts.SecrecyKey] = secrecyCode
                };
                Func<string, int> read = key =>
                    corrupted.TryGetValue(key, out int value) ? value : 0;
                Assert.That(M03LuJunFacts.TryRead(read, out M03LuConfession confession,
                    out M03SecrecyAnswer secrecy, out M03VoyageChoice? route), Is.True);
                Assert.That(route, Is.Null, "Inconsistent M03 history must not grant a voyage.");
                Assert.That(confession, Is.EqualTo(M03LuConfession.NotAsked));
                Assert.That(secrecy, Is.EqualTo(M03SecrecyAnswer.NotTriggered));
            }

            var unasked = new M03LuJunEncounter(M02BridgeEvidence.None, false);
            unasked.Begin();
            unasked.ObserveRollCall();
            unasked.Ask(M03LuJunOpening.NeedAShip);
            unasked.ChooseVoyage(M03VoyageChoice.QiQueMerchant);
            Assert.That(M03LuJunFacts.TryRecord(unasked, write), Is.True);
            Func<string, int> validRead = key =>
                saved.TryGetValue(key, out int value) ? value : 0;
            Assert.That(M03LuJunFacts.TryRead(validRead, out M03LuConfession original,
                out M03SecrecyAnswer originalSecrecy, out M03VoyageChoice? originalRoute), Is.True);
            Assert.That(originalRoute, Is.EqualTo(M03VoyageChoice.QiQueMerchant));
            Assert.That(original, Is.EqualTo(M03LuConfession.NotAsked));
            Assert.That(originalSecrecy, Is.EqualTo(M03SecrecyAnswer.NotTriggered));

            // Values are stored as enum + 1; both of these are individually
            // valid codes but were never reachable together in actual play.
            RejectPair((int)M03LuConfession.NotAsked + 1,
                (int)M03SecrecyAnswer.PromiseSecrecy + 1);
            RejectPair((int)M03LuConfession.AskedWithEvidence + 1,
                (int)M03SecrecyAnswer.NotTriggered + 1);

            var heard = new M03LuJunEncounter(M02BridgeEvidence.FerrymanLog, false);
            heard.Begin();
            heard.ObserveRollCall();
            heard.Ask(M03LuJunOpening.AskWhoCutBridge);
            heard.Respond(M03ConfessionResponse.RemainSilent);
            heard.AnswerSecrecy(M03SecrecyAnswer.RefuseSecrecy);
            heard.ChooseVoyage(M03VoyageChoice.MixedWithCivilian);
            Assert.That(M03LuJunFacts.TryRecord(heard, write), Is.True);
            Assert.That(M03LuJunFacts.TryRead(validRead, out M03LuConfession heardConfession,
                out M03SecrecyAnswer heardSecrecy, out M03VoyageChoice? heardRoute), Is.True);
            Assert.That(heardConfession, Is.EqualTo(M03LuConfession.AskedWithEvidence));
            Assert.That(heardSecrecy, Is.EqualTo(M03SecrecyAnswer.RefuseSecrecy));
            Assert.That(heardRoute, Is.EqualTo(M03VoyageChoice.MixedWithCivilian));

            RejectPair((int)M03LuConfession.NotAsked + 1,
                (int)M03SecrecyAnswer.RefuseSecrecy + 1);
        }

        [Test]
        public void InvalidTransitionsAndIncompletePersistenceAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new M03LuJunEncounter((M02BridgeEvidence)128, false));
            var encounter = new M03LuJunEncounter(M02BridgeEvidence.None, false);
            Assert.Throws<InvalidOperationException>(() => encounter.ObserveRollCall());
            Assert.Throws<InvalidOperationException>(() => M03LuJunFacts.TryRecord(encounter));
            encounter.Begin();
            Assert.Throws<InvalidOperationException>(() => encounter.Begin());
            encounter.ObserveRollCall();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                encounter.Ask((M03LuJunOpening)99));
            encounter.Ask(M03LuJunOpening.AskWhoCutBridge);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                encounter.Respond((M03ConfessionResponse)99));
            encounter.Respond(M03ConfessionResponse.YouShouldHaveToldThem);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                encounter.AnswerSecrecy(M03SecrecyAnswer.NotTriggered));
            encounter.AnswerSecrecy(M03SecrecyAnswer.PromiseSecrecy);
            Assert.Throws<InvalidOperationException>(() =>
                encounter.ChooseVoyage(M03VoyageChoice.HanQiaoOfficial));
            encounter.ChooseVoyage(M03VoyageChoice.QiQueMerchant);
            Assert.Throws<InvalidOperationException>(() =>
                encounter.ChooseVoyage(M03VoyageChoice.LuJunEscort));
            Assert.Throws<ArgumentNullException>(() => M03LuJunFacts.TryRecord(null));
        }
    }
}
