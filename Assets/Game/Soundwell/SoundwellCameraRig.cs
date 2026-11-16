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
        [SerializeField] private Vector2 trialHalfExtents = new Vector2(8.1f, 3.2f);
        [SerializeField] private Vector2 followFactors = new Vector2(0.14f, 0.15f);
        [SerializeField] private Vector2 maxFollowOffsets = new Vector2(0.85f, 0.5f);
        [SerializeField, Min(0f)] private float trialEdgeMargin = 0.12f;
        private Camera sceneCamera;
        private Transform player;
        private Rigidbody2D playerBody;
        private Transform trialRoot;
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
            trialRoot = sceneRoot;
            // The checkpoint can update physics position before its next
            // rendered Transform. Camera presentation follows that real body.
            playerBody = leader.GetComponent<Rigidbody2D>();
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

            // The visible trial corners are authored at local +/-8.1 X and
            // +/-3.2 Y. For a relocated, rotated or scaled room, those edges
            // must be enclosed in WORLD screen axes, not the old local X
            // assumption. Cinemachine still owns the camera pose and lens.
            float aspect = sceneCamera.aspect > 0f ? sceneCamera.aspect : 16f / 9f;
            float trialHalfWidth = Mathf.Max(0.01f, trialHalfExtents.x);
            float trialHalfHeight = Mathf.Max(0.01f, trialHalfExtents.y);
            float edgeMargin = Mathf.Max(0f, trialEdgeMargin);
            Vector3 trialX = trialRoot.TransformVector(new Vector3(trialHalfWidth, 0f, 0f));
            Vector3 trialY = trialRoot.TransformVector(new Vector3(0f, trialHalfHeight, 0f));
            float worldHalfWidth = Mathf.Abs(trialX.x) + Mathf.Abs(trialY.x);
            float worldHalfHeight = Mathf.Abs(trialX.y) + Mathf.Abs(trialY.y);
            // Fit the rotated visible corners, but preserve the established
            // unrotated 8.5-unit square viewport. The older blanket 0.85
            // slack enlarged that square view to 9.07 and regressed the
            // live ORK battle's camera contract.
            // Horizontal and vertical padding include the 0.12 edge margin.
            float desiredSize = Mathf.Max(ReferenceHeight / (2f * PixelsPerUnit),
                worldHalfHeight + edgeMargin + 0.5f,
                (worldHalfWidth + edgeMargin + 0.28f) / aspect);
            float availablePanX = Mathf.Max(0f,
                desiredSize * aspect - worldHalfWidth - edgeMargin);
            float availablePanY = Mathf.Max(0f,
                desiredSize - worldHalfHeight - edgeMargin);

            Vector2 sourceWorld = playerBody != null ? playerBody.position :
                (Vector2)player.position;
            Vector3 sourceLocal = trialRoot.InverseTransformPoint(sourceWorld);
            Vector3 followWorld = trialRoot.TransformVector(new Vector3(
                Mathf.Clamp(sourceLocal.x * followFactors.x,
                    -Mathf.Max(0f, maxFollowOffsets.x), Mathf.Max(0f, maxFollowOffsets.x)),
                Mathf.Clamp(sourceLocal.y * followFactors.y,
                    -Mathf.Max(0f, maxFollowOffsets.y), Mathf.Max(0f, maxFollowOffsets.y)), 0f));
            // Quantize and clamp BOTH screen-axis pans after transforming to
            // world space: a local X follow becomes world Y at 90 degrees.
            // Clamping before rotation let a far exit leave the screen.
            float x = PixelSafeHorizontalPan(followWorld.x, availablePanX);
            float y = PixelSafeHorizontalPan(followWorld.y, availablePanY);
            framingTarget.position = trialRoot.position + new Vector3(x, y, 0f);

            if (virtualCamera == null)
                return;
            var lens = virtualCamera.Lens;
            if (Mathf.Approximately(lens.OrthographicSize, desiredSize))
                return;
            lens.OrthographicSize = desiredSize;
            virtualCamera.Lens = lens;
        }

        // Keep camera framing on whole 16-PPU pixels AND inside the lens's
        // available world-space pan at arbitrary aspect ratios.
        internal static float PixelSafeHorizontalPan(float target, float maxPan)
        {
            int maxPixels = Mathf.Max(0, Mathf.FloorToInt(maxPan * PixelsPerUnit));
            int pixels = Mathf.Clamp(Mathf.RoundToInt(target * PixelsPerUnit),
                -maxPixels, maxPixels);
            return (float)pixels / PixelsPerUnit;
        }

        private void LateUpdate()
        {
            RefreshFraming();
        }
    }
}
