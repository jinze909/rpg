using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class SoundwellBeastPatrolTrackerTests
    {
        [Test]
        public void BeastCyclesThroughSoundwellPatrolWaypointsWhenPlayerIsQuietOrFar()
        {
            SoundwellLargeMapLayout layout = SoundwellLargeMapLayout.Build();
            var acoustic = new SoundwellAcousticBreakTracker();
            var patrol = new SoundwellBeastPatrolTracker(layout, acoustic);

            Vector2 farPlayer = new Vector2(10.5f, 30.5f);
            Assert.That(patrol.State, Is.EqualTo(SoundwellBeastAlertState.Patrolling));
            int startWaypoint = patrol.CurrentWaypointIndex;

            for (int i = 0; i < 120; i++)
                patrol.Step(0.1f, farPlayer, playerMoved: false);

            Assert.That(patrol.CurrentWaypointIndex, Is.Not.EqualTo(startWaypoint));
            Assert.That(patrol.State, Is.EqualTo(SoundwellBeastAlertState.Patrolling));
        }

        [Test]
        public void WetClothMuffleShrinksHearingRadiusAndPreventsMidRangeDetection()
        {
            SoundwellLargeMapLayout layout = SoundwellLargeMapLayout.Build();
            var acoustic = new SoundwellAcousticBreakTracker();
            var patrol = new SoundwellBeastPatrolTracker(layout, acoustic);

            // Player moving 5 tiles away is within BaseHearingRadiusTiles (7.0) -> Alerted.
            Vector2 midRangePlayer = patrol.Position + new Vector2(5.0f, 0f);
            Assert.That(patrol.ActiveHearingRadiusTiles,
                Is.EqualTo(SoundwellBeastPatrolTracker.BaseHearingRadiusTiles));
            patrol.Step(0.1f, midRangePlayer, playerMoved: true);
            Assert.That(patrol.State, Is.EqualTo(SoundwellBeastAlertState.Alerted));

            // With wet cloth applied on a fresh patrol, 5 tiles exceeds MuffledHearingRadiusTiles (2.5).
            var muffledAcoustic = new SoundwellAcousticBreakTracker();
            Assert.That(muffledAcoustic.ApplyAcousticCounter(CombatLockKind.WetClothMuffle), Is.True);
            var muffledPatrol = new SoundwellBeastPatrolTracker(layout, muffledAcoustic);
            Assert.That(muffledPatrol.ActiveHearingRadiusTiles,
                Is.EqualTo(SoundwellBeastPatrolTracker.MuffledHearingRadiusTiles));

            Vector2 fiveTilesAway = muffledPatrol.Position + new Vector2(5.0f, 0f);
            muffledPatrol.Step(0.1f, fiveTilesAway, playerMoved: true);
            Assert.That(muffledPatrol.State, Is.EqualTo(SoundwellBeastAlertState.Patrolling));
        }

        [Test]
        public void BronzeBellLuresBeastAndBrokenStateBlocksEncounterForNonViolentEscape()
        {
            SoundwellLargeMapLayout layout = SoundwellLargeMapLayout.Build();
            var acoustic = new SoundwellAcousticBreakTracker();
            var patrol = new SoundwellBeastPatrolTracker(layout, acoustic);
            int encounterCount = 0;
            patrol.EncounterTriggered += () => encounterCount++;

            // Ring bronze bell -> beast moves toward BronzeBellTile (80, 34).
            float startDistToBell = Vector2.Distance(
                patrol.Position, new Vector2(layout.BronzeBellTile.x + 0.5f, layout.BronzeBellTile.y + 0.5f));
            patrol.NotifyBronzeBellRung(layout.BronzeBellTile, investigateSeconds: 2.0f);
            Assert.That(patrol.State, Is.EqualTo(SoundwellBeastAlertState.InvestigatingBell));

            for (int i = 0; i < 40; i++)
                patrol.Step(0.1f, new Vector2(10.5f, 30.5f), playerMoved: false);

            float afterDistToBell = Vector2.Distance(
                patrol.Position, new Vector2(layout.BronzeBellTile.x + 0.5f, layout.BronzeBellTile.y + 0.5f));
            Assert.That(afterDistToBell, Is.LessThan(startDistToBell - 10f));

            // Break both acoustic locks -> beast enters StaggeredBroken and never triggers encounter at contact.
            Assert.That(acoustic.ApplyAcousticCounter(CombatLockKind.WetClothMuffle), Is.True);
            Assert.That(acoustic.ApplyAcousticCounter(CombatLockKind.DecoyBellRing), Is.True);
            Assert.That(acoustic.IsBeastBroken, Is.True);

            Vector2 frozenPos = patrol.Position;
            patrol.Step(0.2f, frozenPos, playerMoved: true);
            Assert.That(patrol.State, Is.EqualTo(SoundwellBeastAlertState.StaggeredBroken));
            Assert.That(patrol.Position, Is.EqualTo(frozenPos));
            Assert.That(encounterCount, Is.Zero,
                "A staggered/broken beast must preserve the non-violent escape window.");

            // After cycle reset, contact triggers ORK encounter handoff once.
            acoustic.ResetCycle();
            patrol.Step(0.1f, frozenPos, playerMoved: true);
            Assert.That(encounterCount, Is.EqualTo(1));

            Assert.Throws<ArgumentNullException>(() =>
                new SoundwellBeastPatrolTracker(null, acoustic));
            Assert.Throws<ArgumentNullException>(() =>
                new SoundwellBeastPatrolTracker(layout, null));
        }
    }
}
