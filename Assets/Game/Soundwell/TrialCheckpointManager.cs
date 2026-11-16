using System;
using GamingIsLove.Makinom;
using UnityEngine;
using static Rpg.Soundwell.SoundwellSignalKeys;

namespace Rpg.Soundwell
{
    // Owns the existing safety point, lower-plank recovery and physical
    // beast knockback. No direct beast, mechanism or ORK actor references.
    public sealed class TrialCheckpointManager : MonoBehaviour
    {
        private static readonly Vector2 LowerBoardRecovery = new Vector2(2.75f, -0.25f);
        private VariableHandler variables;
        private Rigidbody2D player;
        private Transform safePoint;
        private Transform bell;
        private Vector2 bellHome;

        public void Configure(VariableHandler state, Rigidbody2D actor,
            Transform safe, Transform bellActor, Vector2 initialBell)
        {
            variables = state ?? throw new ArgumentNullException(nameof(state));
            player = actor != null ? actor : throw new ArgumentNullException(nameof(actor));
            safePoint = safe != null ? safe : throw new ArgumentNullException(nameof(safe));
            bell = bellActor != null ? bellActor : throw new ArgumentNullException(nameof(bellActor));
            bellHome = initialBell;
        }

        // The visible broken plank is parented under the trial root. Resolve
        // its collision in local space, like its SpriteRenderer marker; the
        // legacy static rule continues to represent the origin-aligned trial.
        public bool ShouldRecoverAtVisibleLowerPlank(
            bool directCombatStarted, bool gateOpen, Vector2 actorWorldPosition)
        {
            Vector2 actorLocalPosition = transform.InverseTransformPoint(actorWorldPosition);
            return ShouldRecoverFromLowerPlank(
                directCombatStarted, gateOpen, actorLocalPosition);
        }

        public Vector2 LowerPlankRecoveryWorldPosition =>
            transform.TransformPoint(LowerBoardRecovery);

        public void ResetFlagsAndPosition()
        {
            player.linearVelocity = Vector2.zero;
            player.position = safePoint.position;
            bell.position = bellHome;
            // These are the scene-local Makinom flags cleared by the original
            // Retry. EntryOutcomeKey is intentionally NOT reset here.
            foreach (string key in RetryResetKeys)
                variables.Set(key, 0);
        }

        // Public pure rule retained by the old SoundwellTrial compatibility API.
        // ORK turn-based movement is never knocked back by an exploration plank.
        public static bool ShouldRecoverFromLowerPlank(
            bool directCombatStarted, bool gateOpen, Vector2 actorPosition) =>
            !directCombatStarted && gateOpen &&
            Vector2.Distance(actorPosition, SoundwellTrial.LowerBoardPosition) <= 0.48f;

        public bool RecoverLowerPlank(bool alreadyBroken)
        {
            if (!alreadyBroken)
                variables.Set(LowerPlankBrokenKey, 1);
            player.linearVelocity = Vector2.zero;
            player.position = LowerPlankRecoveryWorldPosition;
            return !alreadyBroken;
        }

        public void RecoverFromBeastContact()
        {
            variables.Set(BeastContactKey,
                Mathf.Min(999, variables.GetInt(BeastContactKey) + 1));
            player.linearVelocity = Vector2.zero;
            player.position = safePoint.position;
        }
    }
}
