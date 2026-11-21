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
        public void ConsentedDecoyKillWorksWithoutAyuanButNeverInventsCrewOrVictory()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            var session = new RC04DingYueReefSession();

            Assert.That(session.TryPrepareConsentedDecoyAmbush(
                true, true, true, true), Is.False,
                "A decoy cannot bypass the three real fog-listening rounds.");
            for (int i = 0; i < 3; i++)
                Assert.That(session.SubmitEchoDiscrimination(RC04EchoChoice.SlowHumanEcho), Is.True);

            Assert.That(session.TryPrepareConsentedDecoyAmbush(
                false, true, true, true), Is.False);
            Assert.That(session.TryPrepareConsentedDecoyAmbush(
                true, false, true, true), Is.False);
            Assert.That(session.TryPrepareConsentedDecoyAmbush(
                true, true, false, true), Is.False);
            Assert.That(session.TryPrepareConsentedDecoyAmbush(
                true, true, true, false), Is.False);
            Assert.That(session.DecoyAmbushPrepared, Is.False);
            Assert.That(values, Is.Empty);

            Assert.That(session.TryPrepareConsentedDecoyAmbush(
                true, true, true, true), Is.True);
            Assert.That(session.TryPlayAyuanVaryingSong(), Is.False,
                "No phantom A Yuan song may be added to the fake voice ambush.");
            Assert.That(session.TryResolveReef(
                RC04Resolution.KillMimicBeastWithPreparedDecoy, read, write), Is.False,
                "Preparation is not proof that the mimic beast was defeated.");
            Assert.That(session.TryConfirmDecoyBeastDefeat(false), Is.False);
            Assert.That(session.TryConfirmDecoyBeastDefeat(true), Is.True);
            Assert.That(session.TryConfirmDecoyBeastDefeat(true), Is.False);
            Assert.That(session.TryResolveReef(
                RC04Resolution.KillMimicBeastWithPreparedDecoy, read, write), Is.True);
            Assert.That(session.IsResolved, Is.True);

            Assert.That(RC04DingYueReefFacts.TryRead(read, out RC04ReefRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.AyuanSongUsed, Is.False);
            Assert.That(saved.Value.DecoyAmbushPrepared, Is.True);
            Assert.That(saved.Value.MimicBeastKilled, Is.True);
            Assert.That(saved.Value.Resolution,
                Is.EqualTo(RC04Resolution.KillMimicBeastWithPreparedDecoy));
            Assert.That(values[RC04DingYueReefFacts.DecoyPreparedKey], Is.EqualTo(1));
        }

        [Test]
        public void DecoySaveCannotBeForgedAndLegacySongSaveStillLoads()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            var forged = new RC04ReefRecord(
                3, false, RC04Resolution.KillMimicBeastWithPreparedDecoy);
            Assert.That(RC04DingYueReefFacts.TryRecord(forged, read, write), Is.False);
            Assert.That(values, Is.Empty);
            Assert.That(RC04DingYueReefFacts.IsValidRecord(
                new RC04ReefRecord(3, true,
                    RC04Resolution.KillMimicBeastWithPreparedDecoy,
                    decoyAmbushPrepared: true)), Is.False);

            // Existing confirmed saves made before the decoy field was added
            // have an implicit zero for that field and remain readable.
            values[RC04DingYueReefFacts.ConfirmedKey] = 1;
            values[RC04DingYueReefFacts.RoundsCompletedKey] = 3;
            values[RC04DingYueReefFacts.AyuanSongUsedKey] = 1;
            values[RC04DingYueReefFacts.ResolutionKey] =
                (int)RC04Resolution.KillMimicBeastWithAyuanSong;
            Assert.That(RC04DingYueReefFacts.TryRead(read,
                out RC04ReefRecord? legacy), Is.True);
            Assert.That(legacy.HasValue, Is.True);
            Assert.That(legacy.Value.MimicBeastKilled, Is.True);
            Assert.That(legacy.Value.DecoyAmbushPrepared, Is.False);

            values[RC04DingYueReefFacts.DecoyPreparedKey] = 2;
            Assert.That(RC04DingYueReefFacts.TryRead(read,
                out RC04ReefRecord? corrupt), Is.True);
            Assert.That(corrupt, Is.Null);
            Assert.That(RC04DingYueReefFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[RC04DingYueReefFacts.DecoyPreparedKey], Is.Zero);
        }

        [Test]
        public void TornResolutionIntentRejectsChangedChoicesAndLegacySavesStayReadable()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var original = new RC04ReefRecord(
                3, false, RC04Resolution.BringDingYueBack);
            var differentChoice = new RC04ReefRecord(
                3, false, RC04Resolution.LeaveWithSignalBeacon);
            var differentSong = new RC04ReefRecord(
                3, true, RC04Resolution.BringDingYueBack);

            Assert.Throws<InvalidOperationException>(() =>
                RC04DingYueReefFacts.TryRecord(original, read, (key, value) =>
                {
                    write(key, value);
                    if (key == RC04DingYueReefFacts.PendingSignatureKey)
                        throw new InvalidOperationException("Interrupted after intent");
                }));
            Assert.That(read(RC04DingYueReefFacts.PendingSignatureKey), Is.Not.Zero);
            Assert.That(RC04DingYueReefFacts.TryRead(read, out _), Is.False);
            Assert.That(RC04DingYueReefFacts.TryRecord(
                differentChoice, read, write), Is.False);
            Assert.That(RC04DingYueReefFacts.TryRecord(
                differentSong, read, write), Is.False);

            Assert.That(RC04DingYueReefFacts.TryRecord(original, read, write), Is.True);
            Assert.That(RC04DingYueReefFacts.TryRead(
                read, out RC04ReefRecord? completed), Is.True);
            Assert.That(completed.HasValue, Is.True);
            Assert.That(completed.Value.Resolution, Is.EqualTo(original.Resolution));

            // Older complete saves did not carry a decision signature.
            values[RC04DingYueReefFacts.PendingSignatureKey] = 0;
            Assert.That(RC04DingYueReefFacts.TryRead(
                read, out RC04ReefRecord? legacy), Is.True);
            Assert.That(legacy.HasValue, Is.True);

            Assert.That(RC04DingYueReefFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(read(RC04DingYueReefFacts.PendingSignatureKey), Is.Zero);
            values[RC04DingYueReefFacts.RoundsCompletedKey] = 3;
            Assert.That(RC04DingYueReefFacts.TryRead(read, out _), Is.False);
            Assert.That(RC04DingYueReefFacts.TryRecord(original, read, write), Is.False);
        }

        [Test]
        public void LostConfirmAcknowledgementCanResumeOnlyIdenticalReefOutcome()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int confirmations = 0;
            Action<string, int> unstable = (key, value) =>
            {
                values[key] = value;
                if (key == RC04DingYueReefFacts.ConfirmedKey &&
                    value == 1 && ++confirmations == 1)
                    throw new InvalidOperationException("Lost confirmation ack");
            };
            var session = new RC04DingYueReefSession();
            for (int i = 0; i < 3; i++)
                Assert.That(session.SubmitEchoDiscrimination(
                    RC04EchoChoice.SlowHumanEcho), Is.True);
            Assert.Throws<InvalidOperationException>(() =>
                session.TryResolveReef(
                    RC04Resolution.BringDingYueBack, read, unstable));
            Assert.That(session.IsResolved, Is.False);
            Assert.That(session.TryResolveReef(
                RC04Resolution.LeaveWithSignalBeacon, read, unstable), Is.False);
            Assert.That(session.TryResolveReef(
                RC04Resolution.BringDingYueBack, read, unstable), Is.True);
            Assert.That(session.IsResolved, Is.True);
            Assert.That(confirmations, Is.EqualTo(1));
        }

        [Test]
        public void SongAndDecoyStrategiesCannotBothBePrepared()
        {
            var session = new RC04DingYueReefSession();
            for (int i = 0; i < 3; i++)
                session.SubmitEchoDiscrimination(RC04EchoChoice.SlowHumanEcho);
            Assert.That(session.TryPlayAyuanVaryingSong(), Is.True);
            Assert.That(session.TryPrepareConsentedDecoyAmbush(
                true, true, true, true), Is.False);
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
