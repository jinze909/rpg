using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RC04DingYueReefTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void ThreeSlowHumanEchoRoundsAreRequiredBeforeLandingOrResolving()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = new RC04DingYueReefSession();
            Assert.That(session.TidalWindowLocated, Is.False);
            Assert.That(session.SubmitEchoDiscrimination(RC04EchoChoice.FastMimicReply), Is.False);
            Assert.That(session.FalseMimicPicks, Is.EqualTo(1));
            Assert.That(session.CompletedListeningRounds, Is.Zero);

            Assert.That(session.SubmitEchoDiscrimination(RC04EchoChoice.SlowHumanEcho), Is.True);
            Assert.That(session.SubmitEchoDiscrimination(RC04EchoChoice.SlowHumanEcho), Is.True);
            Assert.That(session.TidalWindowLocated, Is.False);
            Assert.That(session.TryResolveReef(RC04Resolution.BringDingYueBack, read, write), Is.False);

            Assert.That(session.SubmitEchoDiscrimination(RC04EchoChoice.SlowHumanEcho), Is.True);
            Assert.That(session.TidalWindowLocated, Is.True);
            Assert.That(session.TryResolveReef(RC04Resolution.BringDingYueBack, read, write), Is.True);

            Assert.That(RC04DingYueReefFacts.TryRead(read, out RC04ReefRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.ListeningRoundsCompleted, Is.EqualTo(3));
            Assert.That(saved.Value.Resolution, Is.EqualTo(RC04Resolution.BringDingYueBack));
        }

        [Test]
        public void KillingMimicBeastRequiresAyuanVaryingSongAndSupportsSaveRetry()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = new RC04DingYueReefSession();
            for (int i = 0; i < 3; i++)
                Assert.That(session.SubmitEchoDiscrimination(RC04EchoChoice.SlowHumanEcho), Is.True);

            // Cannot record KillMimicBeast without playing A Yuan's varying song first.
            Assert.That(session.TryResolveReef(
                RC04Resolution.KillMimicBeastWithAyuanSong, read, write), Is.False);

            Assert.That(session.TryPlayAyuanVaryingSong(), Is.True);
            Assert.That(session.TryPlayAyuanVaryingSong(), Is.False);

            int failOnce = 0;
            Assert.Throws<InvalidOperationException>(() =>
                session.TryResolveReef(RC04Resolution.KillMimicBeastWithAyuanSong, read, (k, v) =>
                {
                    if (k == RC04DingYueReefFacts.ResolutionKey && failOnce++ == 0)
                        throw new InvalidOperationException("Interrupted RC04 write");
                    write(k, v);
                }));
            Assert.That(session.IsResolved, Is.False);

            Assert.That(session.TryResolveReef(
                RC04Resolution.KillMimicBeastWithAyuanSong, read, write), Is.True);
            Assert.That(RC04DingYueReefFacts.TryRead(read, out RC04ReefRecord? saved), Is.True);
            Assert.That(saved.Value.AyuanSongUsed, Is.True);
            Assert.That(saved.Value.Resolution, Is.EqualTo(RC04Resolution.KillMimicBeastWithAyuanSong));

            Assert.That(RC04DingYueReefFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(RC04DingYueReefFacts.TryRead(read, out RC04ReefRecord? reset), Is.True);
            Assert.That(reset, Is.Null);

            Assert.That(RC04DingYueReefFacts.TryRead(null, out _), Is.False);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                Assert.That(RC04DingYueReefFacts.TryRead(out _), Is.False);
        }
    }
}
