using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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
        // 16 PPU matches the persisted wall sprites; do not rescale ORK physics.
        public const int PixelsPerUnit = 16;
        public const int ReferenceWidth = 320;
        public const int ReferenceHeight = 180;
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

            if (camera.GetComponent<PixelPerfectCamera>() != null ||
                Array.Exists(camera.GetComponents<MonoBehaviour>(), component =>
                    component != null && component.GetType().FullName == "UnityEngine.U2D.PixelPerfectCamera"))
                throw new InvalidOperationException("Only the native URP 2D Pixel Perfect Camera may control Soundwell.");
            sceneCamera = camera;
            player = leader;
            var pixelCamera = camera.gameObject.AddComponent<PixelPerfectCamera>();
            pixelCamera.assetsPPU = PixelsPerUnit;
            pixelCamera.refResolutionX = ReferenceWidth;
            pixelCamera.refResolutionY = ReferenceHeight;
            pixelCamera.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;
            pixelCamera.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;

            var focus = new GameObject("Soundwell Cinemachine framing target");
            focus.transform.SetParent(sceneRoot, false);
            framingTarget = focus.transform;
            RefreshFraming();

            var vcamObject = new GameObject("Soundwell Cinemachine 3 Camera");
            vcamObject.transform.SetParent(sceneRoot, false);
            vcamObject.transform.position = framingTarget.position + new Vector3(0f, 0f, -10f);
            virtualCamera = vcamObject.AddComponent<CinemachineCamera>();
            // Cinemachine 3 must cooperate with URP's pixel lens, not fight it.
            vcamObject.AddComponent<CinemachinePixelPerfect>();
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
            // The 320x180 / 16 PPU 16:9 view has half-height 5.625.
            // Narrow viewports expand the composition to keep both exits.
            float desiredSize = Mathf.Max(ReferenceHeight / (2f * PixelsPerUnit), 8.5f / aspect);
            const float trialHalfWidth = 8.1f;
            const float edgeMargin = 0.12f;
            float availablePan = Mathf.Max(0f, desiredSize * aspect - trialHalfWidth - edgeMargin);
            float horizontalPan = Mathf.Min(0.85f, availablePan);
            // Quantize the visual framing target, not player/ORK positions.
            float x = Mathf.Clamp(player.position.x * 0.14f, -horizontalPan, horizontalPan);
            float y = Mathf.Clamp(player.position.y * 0.15f, -0.5f, 0.5f);
            framingTarget.position = new Vector3(
                Mathf.Round(x * PixelsPerUnit) / PixelsPerUnit,
                Mathf.Round(y * PixelsPerUnit) / PixelsPerUnit, 0f);

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
