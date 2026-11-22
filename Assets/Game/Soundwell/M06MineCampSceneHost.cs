using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Live M06 mine-camp scene owner entered only after the current M05
    /// arrival and Seran letter choice have been verified. Drives the existing
    /// exploration host from the shared Gameplay input map and owns the single
    /// Soundwell Cinemachine/Pixel Perfect camera for the 96x64 camp.
    /// </summary>
    public sealed class M06MineCampSceneHost : IDisposable
    {
        private readonly Transform parent;
        private M06MineCampExplorationHost exploration;
        private SoundwellCameraRig cameraRig;
        private GameObject cameraFrame;
        private Action onExit;
        private bool disposed;
        private bool ownsGameplayContext;

        public M06MineCampExplorationHost Exploration => exploration;
        public SoundwellCameraRig CameraRig => cameraRig;
        public bool IsDisposed => disposed;

        public M06MineCampSceneHost(
            Transform parent, Camera camera,
            Func<string, int> getInt, Action<string, int> setInt, Action onExit)
        {
            this.parent = parent != null ? parent :
                throw new ArgumentNullException(nameof(parent));
            this.onExit = onExit ?? throw new ArgumentNullException(nameof(onExit));
            if (getInt == null) throw new ArgumentNullException(nameof(getInt));
            if (setInt == null) throw new ArgumentNullException(nameof(setInt));

            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) ||
                !landing.HasValue ||
                !M05LettersFacts.TryRead(getInt, out _))
                throw new InvalidOperationException(
                    "M06 requires the verified current M05 landing and a readable Seran letter choice.");

            exploration = new M06MineCampExplorationHost(parent, getInt, setInt);
            try
            {
                camera = camera != null ? camera : Camera.main;
                if (camera == null)
                    throw new InvalidOperationException(
                        "M06 mine-camp exploration requires the Bootstrap Main Camera.");

                M06MineCampMapLayout layout = exploration.Layout;
                Transform mapRoot = exploration.ChunkRenderer.RootObject.transform;
                cameraFrame = new GameObject("M06 mine camp camera bounds");
                cameraFrame.transform.SetParent(parent, false);
                cameraFrame.transform.SetPositionAndRotation(
                    mapRoot.TransformPoint(new Vector3(
                        layout.Width * 0.5f, layout.Height * 0.5f, 0f)),
                    mapRoot.rotation);
                cameraFrame.transform.localScale = mapRoot.localScale;

                cameraRig = camera.gameObject.AddComponent<SoundwellCameraRig>();
                cameraRig.InitializeForWorldMap(
                    camera, exploration.HeroVisual.ActorObject.transform,
                    cameraFrame.transform,
                    new Vector2(layout.Width * 0.5f, layout.Height * 0.5f));

                SoundwellInputAdapter.SetGameplayContext();
                ownsGameplayContext = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (disposed) return;
            if (parent == null) { Exit(); return; }

            var frame = SoundwellInputAdapter.ReadGameplay();
            if (frame.Back && !exploration.IsDialogueOpen)
            {
                Exit();
                return;
            }
            if (exploration.IsDialogueOpen) return;

            exploration.StepMovement(frame.Movement, deltaSeconds);
            if (frame.Ring) exploration.TryInteractAtCurrentPosition();
        }

        public void Exit()
        {
            if (disposed) return;
            Action completed = onExit;
            onExit = null;
            Dispose();
            completed?.Invoke();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (ownsGameplayContext)
            {
                SoundwellInputAdapter.SetEntryContext();
                ownsGameplayContext = false;
            }
            cameraRig?.Dispose();
            if (cameraRig != null) DestroyOwned(cameraRig);
            cameraRig = null;
            exploration?.Dispose();
            exploration = null;
            if (cameraFrame != null) DestroyOwned(cameraFrame);
            cameraFrame = null;
            onExit = null;
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(value);
            else
                UnityEngine.Object.DestroyImmediate(value);
        }
    }
}