using GamingIsLove.ORKFramework;
using UnityEngine;

namespace Rpg.Soundwell
{
    // Retains the serialized SoundwellTrial MonoBehaviour GUID in the scene.
    // Trial systems own the mechanisms, beast and checkpoint independently.
    public sealed class SoundwellTrial : TrialFlowController
    {
        // Reflection compatibility for the existing ORK gate-reach regression.
        internal static bool HasLiveGateActorInReach(Combatant member)
        {
            if (member == null || member.GameObject == null ||
                !member.GameObject.activeInHierarchy)
                return false;
            return Vector2.Distance(member.GameObject.transform.position,
                new Vector2(-0.55f, 0f)) <= 0.8f;
        }
    }

    public readonly struct SoundwellControlFrame
    {
        public Vector2 Movement { get; }
        public bool Ring { get; }
        public bool Wrap { get; }
        public bool Retry { get; }
        public bool Back { get; }
        public bool Carry { get; }
        public bool Gate { get; }

        private SoundwellControlFrame(Vector2 movement, bool ring, bool wrap,
            bool retry, bool back, bool carry, bool gate)
        {
            Movement = Vector2.ClampMagnitude(movement, 1f);
            Ring = ring;
            Wrap = wrap;
            Retry = retry;
            Back = back;
            Carry = carry;
            Gate = gate;
        }

        public static bool EnterRequested(bool keyboardF2, bool gamepadSouth) =>
            keyboardF2 || gamepadSouth;

        // Pure mapping for independent, hardware-free Play Mode assertions.
        public static SoundwellControlFrame FromActions(Vector2 movement, bool ring, bool wrap,
            bool retry, bool back, bool carry, bool gate) =>
            new SoundwellControlFrame(movement, ring, wrap, retry, back, carry, gate);

        // Pure mapping helper retained for hardware-free regression assertions.
        public static SoundwellControlFrame FromButtons(Vector2 movement,
            bool keyboardRing, bool keyboardWrap, bool keyboardRetry, bool keyboardBack,
            bool gamepadSouth, bool gamepadEast, bool gamepadWest, bool gamepadNorth,
            bool keyboardCarry = false, bool gamepadLeftShoulder = false,
            bool keyboardGate = false, bool gamepadRightShoulder = false) =>
            FromActions(movement, keyboardRing || gamepadSouth, keyboardWrap || gamepadWest,
                keyboardRetry || gamepadNorth, keyboardBack || gamepadEast,
                keyboardCarry || gamepadLeftShoulder, keyboardGate || gamepadRightShoulder);
    }

}
