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
        private PixelPerfectCamera createdPixelCamera;
        private CinemachineBrain createdBrain;

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
            createdPixelCamera = pixelCamera;
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

            createdBrain = sceneCamera.gameObject.AddComponent<CinemachineBrain>();
        }

        // Broken or uninitialized serialized floats must not produce an
        // invalid Cinemachine lens or move the focus to NaN coordinates.
        // Valid authored values remain unchanged.
        private static float FiniteOrDefault(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;

        public void RefreshFraming()
        {
            if (sceneCamera == null || player == null || framingTarget == null)
                return;

            // The visible trial corners are authored at local +/-8.1 X and
            // +/-3.2 Y. For a relocated, rotated or scaled room, those edges
            // must be enclosed in WORLD screen axes, not the old local X
            // assumption. Cinemachine still owns the camera pose and lens.
            float aspect = FiniteOrDefault(sceneCamera.aspect, 16f / 9f);
            if (aspect <= 0f)
                aspect = 16f / 9f;
            float trialHalfWidth = Mathf.Max(0.01f,
                FiniteOrDefault(trialHalfExtents.x, 8.1f));
            float trialHalfHeight = Mathf.Max(0.01f,
                FiniteOrDefault(trialHalfExtents.y, 3.2f));
            float edgeMargin = Mathf.Max(0f,
                FiniteOrDefault(trialEdgeMargin, 0.12f));
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
            float followX = FiniteOrDefault(followFactors.x, 0.14f);
            float followY = FiniteOrDefault(followFactors.y, 0.15f);
            float maxFollowX = Mathf.Max(0f,
                FiniteOrDefault(maxFollowOffsets.x, 0.85f));
            float maxFollowY = Mathf.Max(0f,
                FiniteOrDefault(maxFollowOffsets.y, 0.5f));
            Vector3 followWorld = trialRoot.TransformVector(new Vector3(
                Mathf.Clamp(sourceLocal.x * followX, -maxFollowX, maxFollowX),
                Mathf.Clamp(sourceLocal.y * followY, -maxFollowY, maxFollowY), 0f));
            // Quantize and clamp BOTH screen-axis pans after transforming to
            // world space: a local X follow becomes world Y at 90 degrees.
            // Clamping before rotation let a far exit leave the screen.
            // The scene root can move by a fractional world pixel when the
            // authored trial is translated or rotated. Snap the FINAL world
            // target, not just its local follow delta, without allowing the
            // camera to pan beyond the visible room's world-axis margins.
            Vector3 roomOrigin = trialRoot.position;
            float x = PixelSafeWorldPan(roomOrigin.x, followWorld.x, availablePanX);
            float y = PixelSafeWorldPan(roomOrigin.y, followWorld.y, availablePanY);
            framingTarget.position = new Vector3(x, y, roomOrigin.z);

            if (virtualCamera == null)
                return;
            var lens = virtualCamera.Lens;
            if (Mathf.Approximately(lens.OrthographicSize, desiredSize))
                return;
            lens.OrthographicSize = desiredSize;
            virtualCamera.Lens = lens;
        }

        // Choose a whole WORLD pixel inside the actual world-axis pan range.
        // Quantizing only the follow delta leaves half-pixel camera targets
        // when a scene root is moved to a fractional world coordinate.
        internal static float PixelSafeWorldPan(float origin, float delta, float maxPan)
        {
            if (float.IsNaN(origin) || float.IsInfinity(origin))
                return 0f;
            float limit = Mathf.Max(0f, FiniteOrDefault(maxPan, 0f));
            float safeDelta = FiniteOrDefault(delta, 0f);
            int minPixel = Mathf.CeilToInt((origin - limit) * PixelsPerUnit);
            int maxPixel = Mathf.FloorToInt((origin + limit) * PixelsPerUnit);
            if (minPixel > maxPixel)
                return origin; // No complete pixel fits; never crop the room.
            int targetPixel = Mathf.RoundToInt((origin + safeDelta) * PixelsPerUnit);
            return (float)Mathf.Clamp(targetPixel, minPixel, maxPixel) / PixelsPerUnit;
        }

        // Preserve the existing isolated relative-pan contract for smoke
        // validation and non-translated callers. The live camera above uses
        // PixelSafeWorldPan so relocated rooms snap in world coordinates.
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

        private void OnDestroy()
        {
            if (createdBrain != null)
            {
                if (Application.isPlaying)
                    Destroy(createdBrain);
                else
                    DestroyImmediate(createdBrain);
                createdBrain = null;
            }
            if (createdPixelCamera != null)
            {
                if (Application.isPlaying)
                    Destroy(createdPixelCamera);
                else
                    DestroyImmediate(createdPixelCamera);
                createdPixelCamera = null;
            }
            sceneCamera = null;
        }
    }
}
