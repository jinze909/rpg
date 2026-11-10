using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// The only Soundwell camera controller. Cinemachine owns the real
    /// Camera's pose and orthographic lens; UI presentation may only read it.
    /// Tracks a small, clamped fraction of player motion so the entire
    /// 14 x 6.4 prototype room and labels stay visible while exploring.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class SoundwellCameraRig : MonoBehaviour
    {
        private Camera sceneCamera;
        private Transform player;
        private Transform framingTarget;
        private CinemachineCamera virtualCamera;

        public CinemachineCamera VirtualCamera => virtualCamera;
        public Transform FramingTarget => framingTarget;

        public void Initialize(Camera camera, Transform leader, Transform sceneRoot)
        {
            if (sceneCamera != null || camera == null || leader == null || sceneRoot == null)
                throw new InvalidOperationException("Soundwell camera rig must be initialized exactly once.");
            if (!camera.orthographic || camera.GetComponent<CinemachineBrain>() != null)
                throw new InvalidOperationException("Soundwell requires one orthographic Camera without a competing Brain.");

            sceneCamera = camera;
            player = leader;

            var focus = new GameObject("Soundwell Cinemachine framing target");
            focus.transform.SetParent(sceneRoot, false);
            framingTarget = focus.transform;
            RefreshFraming();

            var vcamObject = new GameObject("Soundwell Cinemachine 3 Camera");
            vcamObject.transform.SetParent(sceneRoot, false);
            vcamObject.transform.position = framingTarget.position + new Vector3(0f, 0f, -10f);
            virtualCamera = vcamObject.AddComponent<CinemachineCamera>();
            var follow = vcamObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(0f, 0f, -10f);
            virtualCamera.Follow = framingTarget;
            virtualCamera.Priority = 100;
            RefreshFraming();

            sceneCamera.gameObject.AddComponent<CinemachineBrain>();
        }

        public void RefreshFraming()
        {
            if (sceneCamera == null || player == null || framingTarget == null)
                return;

            // Keep BOTH trial ends in frame on square and portrait displays.
            // The old static camera showed x=-8.1 and x=+8.1 even at 1:1;
            // blindly following the player by 0.85 units would crop the far
            // end because the square viewport has only 0.3 units of slack.
            // Widescreen has enough slack for the full gentle follow range.
            // Only Cinemachine owns the real camera's pose and lens.
            float aspect = sceneCamera.aspect > 0f ? sceneCamera.aspect : 16f / 9f;
            float desiredSize = Mathf.Max(5.5f, 8.4f / aspect);
            const float trialHalfWidth = 8.1f;
            const float edgeMargin = 0.12f;
            float availablePan = Mathf.Max(0f, desiredSize * aspect - trialHalfWidth - edgeMargin);
            float horizontalPan = Mathf.Min(0.85f, availablePan);
            framingTarget.position = new Vector3(
                Mathf.Clamp(player.position.x * 0.14f, -horizontalPan, horizontalPan),
                Mathf.Clamp(player.position.y * 0.15f, -0.5f, 0.5f),
                0f);

            if (virtualCamera == null)
                return;
            var lens = virtualCamera.Lens;
            if (Mathf.Approximately(lens.OrthographicSize, desiredSize))
                return;
            lens.OrthographicSize = desiredSize;
            virtualCamera.Lens = lens;
        }

        private void LateUpdate()
        {
            RefreshFraming();
        }
    }
}
