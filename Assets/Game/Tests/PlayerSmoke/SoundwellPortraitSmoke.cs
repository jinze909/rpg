#if RPG_TEST_PROFILE && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rpg.Soundwell
{
    internal sealed class SoundwellPortraitSmoke : MonoBehaviour
    {
        private const int Width = 320;
        private const int Height = 800;
        private string screenshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-rpg-portrait-hud");
            if (index < 0 || index + 1 >= args.Length)
                return;

            Application.runInBackground = true;
            Screen.SetResolution(Width, Height, FullScreenMode.Windowed);
            var root = new GameObject("Soundwell portrait regression");
            DontDestroyOnLoad(root);
            root.AddComponent<SoundwellPortraitSmoke>().screenshot = args[index + 1];
        }

        private IEnumerator Start()
        {
            yield return WaitFor(() => Screen.width == Width && Screen.height == Height);
            if (Screen.width != Width || Screen.height != Height)
            {
                Fail($"Expected {Width}x{Height}, got {Screen.width}x{Screen.height}.");
                yield break;
            }
            yield return FindFirstObjectByType<SoundwellEntrance>().OpenTrial();
            yield return WaitFor(() => FindFirstObjectByType<OrkPartyBridge>()?.IsReady == true &&
                FindFirstObjectByType<SoundwellTrial>()?.IsReady == true);
            var trial = FindFirstObjectByType<SoundwellTrial>();
            var party = FindFirstObjectByType<OrkPartyBridge>();
            if (trial == null || !trial.IsReady || party == null || !party.IsReady)
            {
                Fail("The trial party did not initialize.");
                yield break;
            }
            yield return null;
            if (!CanHitBackButton())
            {
                Fail("The exploration Back button is outside the viewport or blocked.");
                yield break;
            }
            trial.PlayerActor.GetComponent<Rigidbody2D>().position = new Vector2(-0.55f, 0f);
            Physics2D.SyncTransforms();
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            yield return WaitFor(() => party.BattleInProgress);
            if (!party.BattleInProgress)
            {
                Fail("The gate interaction did not start the ORK encounter.");
                yield break;
            }

            yield return new WaitForEndOfFrame();
            var readout = GameObject.Find("Soundwell ORK battle readout")
                ?.GetComponent<UnityEngine.UI.Text>();
            if (readout == null || string.IsNullOrEmpty(readout.text) ||
                !FitScreen(readout.rectTransform, out _) || !CanHitBackButton())
            {
                Fail("The battle readout or Retreat button is outside the viewport.");
                yield break;
            }
            // The player runtime is built with a restricted Unity module set.
            // Read back the presented frame without referencing ScreenCapture.
            var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
            image.Apply(false);
            File.WriteAllBytes(screenshot, image.EncodeToPNG());
            Destroy(image);
            Debug.Log("RPG_SOUNDWELL_PORTRAIT_HUD_VALIDATED");
            Application.Quit(0);
        }

        private static bool CanHitBackButton()
        {
            var button = GameObject.Find("Soundwell Back button")?.GetComponent<UnityEngine.UI.Button>();
            var input = EventSystem.current;
            if (button == null || !button.interactable || input == null ||
                !FitScreen((RectTransform)button.transform, out Vector2 center))
                return false;
            var hits = new List<RaycastResult>();
            input.RaycastAll(new PointerEventData(input) { position = center }, hits);
            return hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button;
        }

        private static bool FitScreen(RectTransform rect, out Vector2 center)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            center = (corners[0] + corners[2]) * 0.5f;
            return corners[0].x >= 0f && corners[0].y >= 0f &&
                corners[2].x <= Screen.width && corners[2].y <= Screen.height;
        }

        private static IEnumerator WaitFor(Func<bool> ready)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!ready() && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private static void Fail(string reason)
        {
            Debug.LogError("Soundwell portrait regression failed. " + reason);
            Application.Quit(2);
        }
    }
}
#endif
