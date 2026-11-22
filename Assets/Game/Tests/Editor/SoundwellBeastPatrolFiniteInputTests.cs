using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class SoundwellBeastPatrolFiniteInputTests
    {
        [Test]
        public void NonFiniteSpeedsDeltaTimeAndPlayerPositionsNeverPoisonBeastPatrol()
        {
            SoundwellLargeMapLayout layout = SoundwellLargeMapLayout.Build();
            var acoustic = new SoundwellAcousticBreakTracker();
            var patrol = new SoundwellBeastPatrolTracker(
                layout, acoustic,
                patrolSpeedTilesPerSecond: float.NaN,
                chaseSpeedTilesPerSecond: float.PositiveInfinity);

            Assert.That(patrol.PatrolSpeedTilesPerSecond, Is.EqualTo(3.2f));
            Assert.That(patrol.ChaseSpeedTilesPerSecond, Is.EqualTo(4.6f));

            Vector2 start = patrol.Position;
            int encounters = 0;
            patrol.EncounterTriggered += () => encounters++;

            patrol.Step(float.NaN, start, playerMoved: true);
            patrol.Step(float.PositiveInfinity, start, playerMoved: true);
            Assert.That(patrol.Position, Is.EqualTo(start));
            Assert.That(patrol.State, Is.EqualTo(SoundwellBeastAlertState.Patrolling));
            Assert.That(encounters, Is.Zero);

            // Non-finite player position must not alert or poison Position, while patrol continues.
            patrol.Step(0.1f, new Vector2(float.NaN, float.PositiveInfinity), playerMoved: true);
            Assert.That(float.IsNaN(patrol.Position.x) || float.IsInfinity(patrol.Position.x), Is.False);
            Assert.That(float.IsNaN(patrol.Position.y) || float.IsInfinity(patrol.Position.y), Is.False);
            Assert.That(patrol.State, Is.EqualTo(SoundwellBeastAlertState.Patrolling));
            Assert.That(encounters, Is.Zero);
        }

        [Test]
        public void NonFiniteBellInvestigateDurationFallsBackToFiniteDefaultAndResumesPatrol()
        {
            SoundwellLargeMapLayout layout = SoundwellLargeMapLayout.Build();
            var acoustic = new SoundwellAcousticBreakTracker();
            var patrol = new SoundwellBeastPatrolTracker(layout, acoustic);

            patrol.NotifyBronzeBellRung(layout.BronzeBellTile, float.PositiveInfinity);
            Assert.That(patrol.State, Is.EqualTo(SoundwellBeastAlertState.InvestigatingBell));

            // Advance enough time to reach the bell and exhaust the 4.5s default hold.
            Vector2 farPlayer = new Vector2(10.5f, 30.5f);
            for (int i = 0; i < 200; i++)
                patrol.Step(0.1f, farPlayer, playerMoved: false);

            Assert.That(patrol.State, Is.EqualTo(SoundwellBeastAlertState.Patrolling));
            Assert.That(float.IsNaN(patrol.Position.x) || float.IsInfinity(patrol.Position.x), Is.False);
        }
    }
}
