using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Presentation only: one Animator drives the whole placeholder silhouette
    /// and face, never the Rigidbody2D, ORK combatant, or gameplay state.
    /// Exploration and encounter events select its authored Unity clips.
    /// </summary>
    public sealed class SoundwellPlayerVisualAnimation : MonoBehaviour
    {
        private static readonly string[] States = { "Idle", "Move", "Action", "Hit" };
        private Animator animator;
        private int activeState = -1;
        private float oneShotUntil = float.NegativeInfinity;
        private float hitReactionUntil = float.NegativeInfinity;

        public Animator Controller => animator;
        public string CurrentState => activeState < 0 ? string.Empty : States[activeState];

        public void Initialize()
        {
            if (animator != null || GetComponent<Animator>() != null)
                throw new InvalidOperationException("The player placeholder must have exactly one Animator.");

            var controller = Resources.Load<RuntimeAnimatorController>("Soundwell/PlaceholderPlayer");
            if (controller == null)
                throw new InvalidOperationException("Run the M4 Unity prepare/publish stage to save the animation controller.");

            animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            // ORK may suspend Time.timeScale while a turn resolves. The
            // placeholder's short action/hit clips are feedback, not combat
            // timing, so keep them playing during a paused battle clock.
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            ResetAnimation();
        }

        public void UpdateMotion(bool moving)
        {
            if (Time.unscaledTime < oneShotUntil)
                return;
            SetState(moving ? 1 : 0);
        }

        public void PlayAction()
        {
            // Repeated bell/interaction input is allowed, but restarting the
            // visual one-shot on every press makes its first frame stutter
            // indefinitely. Preserve the original visual deadline and let
            // gameplay input continue through the trial as before.
            if (Time.unscaledTime < hitReactionUntil ||
                (activeState == 2 && Time.unscaledTime < oneShotUntil))
                return;
            oneShotUntil = Time.unscaledTime + 0.40f;
            SetState(2, true);
        }

        public void PlayHit()
        {
            hitReactionUntil = Time.unscaledTime + 0.32f;
            oneShotUntil = hitReactionUntil;
            SetState(3, true);
        }

        public void ResetAnimation()
        {
            oneShotUntil = float.NegativeInfinity;
            hitReactionUntil = float.NegativeInfinity;
            SetState(0, true);
        }

        private void SetState(int state, bool restart = false)
        {
            if (!restart && activeState == state)
                return;
            if (animator == null || !animator.HasState(0, Animator.StringToHash(States[state])))
                throw new InvalidOperationException("M4 animator is missing state " + States[state]);
            activeState = state;
            animator.Play(States[state], 0, 0f);
        }
    }
}
