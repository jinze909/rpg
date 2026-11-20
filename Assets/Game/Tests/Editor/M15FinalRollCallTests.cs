using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M15FinalRollCallTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void ThreeCallRuleStopsOnFirstAnswerAndUnnamedXiaoqiUsesNumberSeventyTwo()
        {
            Assert.That(M15FinalRollCallSession.CallsSpokenForEntry(answeredInPerson: true), Is.EqualTo(1));
            Assert.That(M15FinalRollCallSession.CallsSpokenForEntry(answeredInPerson: false), Is.EqualTo(3));

            var unnamed = new M15FinalRollCallSession(bridgeTruthSealed: false, consentedVerifiedNamesCount: 3);
            Assert.That(unnamed.XiaoqiHasCustomName, Is.False);
            Assert.That(unnamed.FormatXiaoqiRollCallEntry(zh: true), Does.Contain("七十二"));

            var named = new M15FinalRollCallSession(bridgeTruthSealed: false, consentedVerifiedNamesCount: 3, "陈安");
            Assert.That(named.XiaoqiHasCustomName, Is.True);
            Assert.That(named.FormatXiaoqiRollCallEntry(zh: true), Is.EqualTo("陈安"));
        }

        [Test]
        public void PublicResponseSettlesPensionWhilePrivateResponsePreservesConcealedChoice()
        {
            var pubSave = new Dictionary<string, int>();
            Func<string, int> readPub = Reader(pubSave);
            Action<string, int> writePub = (k, v) => pubSave[k] = v;

            // Public response without StoppedAndReconciled pension must be rejected by facts guard.
            Assert.That(M15FinalRollCallFacts.TryRecord(
                new M15FinalRollCallRecord(
                    M15RollCallReader.HanQiao,
                    M15FinalIdentityResponse.PublicTrueName,
                    M15PensionClosure.UnchangedOrPrivate,
                    M15SteleInscription.KeepOriginal,
                    0,
                    true),
                readPub, writePub), Is.False);

            var pubSession = new M15FinalRollCallSession(bridgeTruthSealed: false, consentedVerifiedNamesCount: 4);
            Assert.That(pubSession.TryCompleteRollCall(
                M15RollCallReader.HanQiao,
                M15FinalIdentityResponse.PublicTrueName,
                M15SteleInscription.CarveConsentedVerifiedNames,
                readPub, writePub), Is.True);

            Assert.That(M15FinalRollCallFacts.TryRead(readPub, out M15FinalRollCallRecord? savedPub), Is.True);
            Assert.That(savedPub.HasValue, Is.True);
            Assert.That(savedPub.Value.PensionClosure, Is.EqualTo(M15PensionClosure.StoppedAndReconciled));
            Assert.That(savedPub.Value.XiaoqiCalledByNumberSeventyTwo, Is.True);

            // Private custodian response under sealed truth allows anonymous returned-alive line.
            var privSave = new Dictionary<string, int>();
            Func<string, int> readPriv = Reader(privSave);
            Action<string, int> writePriv = (k, v) => privSave[k] = v;

            var privSession = new M15FinalRollCallSession(bridgeTruthSealed: true, consentedVerifiedNamesCount: 0);
            Assert.That(privSession.CanChooseSteleInscription(M15SteleInscription.CarveConsentedVerifiedNames), Is.False);
            Assert.That(privSession.CanChooseSteleInscription(M15SteleInscription.AddReturnedAliveLine), Is.True);
            Assert.That(privSession.TryCompleteRollCall(
                M15RollCallReader.AuntJiang,
                M15FinalIdentityResponse.PrivateCustodianResponse,
                M15SteleInscription.AddReturnedAliveLine,
                readPriv, writePriv), Is.True);

            Assert.That(M15FinalRollCallFacts.TryRead(readPriv, out M15FinalRollCallRecord? savedPriv), Is.True);
            Assert.That(savedPriv.Value.IdentityResponse, Is.EqualTo(M15FinalIdentityResponse.PrivateCustodianResponse));
            Assert.That(savedPriv.Value.PensionClosure, Is.EqualTo(M15PensionClosure.UnchangedOrPrivate));
        }

        [Test]
        public void InterruptedSaveRetriesCleanlyAndResetClearsPersistedRollCall()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = new M15FinalRollCallSession(bridgeTruthSealed: false, consentedVerifiedNamesCount: 2);
            int failOnce = 0;
            Assert.Throws<InvalidOperationException>(() =>
                session.TryCompleteRollCall(
                    M15RollCallReader.Protagonist,
                    M15FinalIdentityResponse.PublicTrueName,
                    M15SteleInscription.KeepOriginal,
                    read, (k, v) =>
                    {
                        if (k == M15FinalRollCallFacts.SteleInscriptionKey && failOnce++ == 0)
                            throw new InvalidOperationException("Interrupted M15 write");
                        write(k, v);
                    }));
            Assert.That(session.IsCompleted, Is.False);
            Assert.That(M15FinalRollCallFacts.TryRead(read, out M15FinalRollCallRecord? partial), Is.True);
            Assert.That(partial, Is.Null);

            Assert.That(session.TryCompleteRollCall(
                M15RollCallReader.Protagonist,
                M15FinalIdentityResponse.PublicTrueName,
                M15SteleInscription.KeepOriginal,
                read, write), Is.True);
            Assert.That(session.IsCompleted, Is.True);

            Assert.That(M15FinalRollCallFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M15FinalRollCallFacts.TryRead(read, out M15FinalRollCallRecord? reset), Is.True);
            Assert.That(reset, Is.Null);

            Assert.That(M15FinalRollCallFacts.TryRead(null, out _), Is.False);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                Assert.That(M15FinalRollCallFacts.TryRead(out _), Is.False);
        }
    }
}
