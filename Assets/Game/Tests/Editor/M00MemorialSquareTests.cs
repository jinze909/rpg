using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M00MemorialSquareTests
    {
        [UnityTest]
        public IEnumerator MemorialOpeningRequiresPhysicalApproachAndRecordsAuthoredActions()
        {
            InputTestFixture input = null;
            bool inputReady = false;
            bool testBodyCompleted = false;
            string originalLanguage = null;
            string cleanupFailure = null;
            M00MemorialSquare memorial = null;
            SoundwellEntrance entry = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                yield return new EnterPlayMode(expectDomainReload: true);
                yield return null;
                input = new InputTestFixture();
                input.Setup();
                inputReady = true;
                originalLanguage = SoundwellUiLocalization.Language;
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(WhitewaterPrelude.SceneTitle, Is.EqualTo("Whitewater Crossing"),
                    "The player sees the v1.2 scene title, not the internal S00 story ID.");
                Keyboard keys = InputSystem.AddDevice<Keyboard>();

                // Enter M00 through the same earned S00 -> M00 transition used
                // by the player. Do not construct the scene component in an
                // unrelated root: its parent, entry callback and input handoff
                // are part of the actual playable path being verified.
                EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/Game/Scenes/Bootstrap.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));
                float entryDeadline = Time.realtimeSinceStartup + 8f;
                while (entry == null && Time.realtimeSinceStartup < entryDeadline)
                {
                    yield return null;
                    entry = Object.FindFirstObjectByType<SoundwellEntrance>();
                }
                Assert.That(entry, Is.Not.Null, "M00 requires the live Bootstrap input/UI owner.");
                // Keep the production entry owner alive. SetUiContext below disables
                // its Entry action map while S00/M00 owns input.

                var currentPrelude = typeof(SoundwellEntrance).GetField("currentPrelude",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var thirdBell = typeof(WhitewaterPrelude).GetField("thirdBellStarted",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var currentMemorial = typeof(SoundwellEntrance).GetField("currentMemorial",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(currentPrelude, Is.Not.Null);
                Assert.That(thirdBell, Is.Not.Null);
                Assert.That(currentMemorial, Is.Not.Null);

                entry.OpenPrelude();
                yield return null;
                var prelude = (WhitewaterPrelude)currentPrelude.GetValue(entry);
                Assert.That(prelude, Is.Not.Null);
                thirdBell.SetValue(prelude, true);
                prelude.Close();

                // The exit callback creates and begins M00 synchronously under this
                // owner. Read its authoritative reference instead of racing a global
                // object search during the S00 -> M00 handoff.
                memorial = (M00MemorialSquare)currentMemorial.GetValue(entry);
                Assert.That(entry.MemorialSquareOpen, Is.True,
                    "The earned third bell must hand off to M00 before the callback returns.");
                Assert.That(memorial, Is.Not.Null,
                    "The earned third bell must open the authored M00 memorial scene.");
                yield return null;
                Assert.That(entry.MemorialSquareOpen, Is.True);
                Assert.That(memorial.transform.parent, Is.EqualTo(entry.transform),
                    "M00 must be owned by the real entry controller.");
                yield return null;

                // The real M00 UI must remain reachable after S00. Portrait
                // used to clip the player; 5:1 hid instructions and return.
                RectTransform authored = memorial.GetComponentsInChildren<RectTransform>(true)
                    .Single(rect => rect.gameObject.name == "M00 responsive authored view");
                RectTransform playerRect = memorial.GetComponentsInChildren<RectTransform>(true)
                    .Single(rect => rect.gameObject.name == "M00 player");
                RectTransform titleRect = memorial.GetComponentsInChildren<Text>(true)
                    .Single(label => label.text == "Your Name Carved in Stone").rectTransform;
                RectTransform guidanceRect = memorial.GetComponentsInChildren<Text>(true)
                    .Single(label => label.text == memorial.CurrentInstructions).rectTransform;
                RectTransform returnRect = memorial.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "M00 return to entry")
                    .GetComponent<RectTransform>();
                RectTransform choiceRect = memorial.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "M00 choice 3")
                    .GetComponent<RectTransform>();
                var visibleRects = new[] { playerRect, titleRect, guidanceRect,
                    returnRect, choiceRect };
                foreach (float aspect in new[] { 9f / 16f, 32f / 9f, 5f, 16f / 9f })
                {
                    memorial.LayoutForViewportAspect(aspect);
                    float scale = authored.localScale.x;
                    float halfWidth = 0.5f * Mathf.Sqrt(960f * 540f * aspect);
                    float halfHeight = 0.5f * Mathf.Sqrt(960f * 540f / aspect);
                    Assert.That(scale, Is.GreaterThan(0f));
                    Assert.That(authored.localScale.y, Is.EqualTo(scale).Within(0.0001f));
                    foreach (RectTransform rect in visibleRects)
                    {
                        Assert.That((Mathf.Abs(rect.anchoredPosition.x) +
                                rect.sizeDelta.x * 0.5f) * scale,
                            Is.LessThanOrEqualTo(halfWidth - 6f),
                            rect.name + " clipped horizontally at " + aspect);
                        Assert.That((Mathf.Abs(rect.anchoredPosition.y) +
                                rect.sizeDelta.y * 0.5f) * scale,
                            Is.LessThanOrEqualTo(halfHeight - 6f),
                            rect.name + " clipped vertically at " + aspect);
                    }
                }
                memorial.LayoutForViewportAspect(
                    (float)Screen.width / Mathf.Max(1, Screen.height));

                Assert.That(memorial.Phase, Is.Zero);
                Assert.That(memorial.GetComponentsInChildren<Text>(true)
                    .Any(label => label.text == "Your Name Carved in Stone"), Is.True,
                    "The player-facing M00 title must not expose its development ID.");
                Assert.That(memorial.CanInspectStatue, Is.False,
                    "The player must walk to the memorial before reading it.");
                input.Press(keys.eKey);
                yield return null;
                input.Release(keys.eKey);
                yield return null;
                Assert.That(memorial.Phase, Is.Zero,
                    "An out-of-range interaction cannot reveal the epitaph.");

                input.Press(keys.dKey);
                float statueDeadline = Time.realtimeSinceStartup + 5f;
                while (!memorial.CanInspectStatue &&
                       Time.realtimeSinceStartup < statueDeadline)
                    yield return null;
                input.Release(keys.dKey);
                yield return null;
                Assert.That(memorial.CanInspectStatue, Is.True);
                input.Press(keys.eKey);
                yield return null;
                input.Release(keys.eKey);
                yield return null;
                Assert.That(memorial.Phase, Is.EqualTo(1));
                Assert.That(memorial.CurrentNarration, Does.Contain("Third Brigade"));

                memorial.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "M00 choice 0")
                    .onClick.Invoke();
                yield return null;
                Assert.That(memorial.Phase, Is.EqualTo(2));
                Assert.That(memorial.StatueReaction, Is.Zero);

                input.Press(keys.dKey);
                float womanDeadline = Time.realtimeSinceStartup + 4f;
                while (!memorial.CanInspectOldWoman &&
                       Time.realtimeSinceStartup < womanDeadline)
                    yield return null;
                input.Release(keys.dKey);
                yield return null;
                Assert.That(memorial.CanInspectOldWoman, Is.True);
                input.Press(keys.eKey);
                yield return null;
                input.Release(keys.eKey);
                yield return null;
                Assert.That(memorial.OldWomanStoneObserved, Is.True,
                    "The memorial fact is earned only by inspecting the woman.");
                Assert.That(memorial.CurrentNarration, Does.Contain("3rd Squad"),
                    "M00's English roster must match the authored third squad of twelve in RC-05, never a platoon.");
                Assert.That(memorial.CurrentNarration, Does.Not.Contain("3rd Platoon"));
                Assert.That(memorial.Phase, Is.EqualTo(3));

                float forgottenDeadline = Time.realtimeSinceStartup + 3f;
                while (!memorial.ChildLineText.Contains("...the bridge") &&
                       Time.realtimeSinceStartup < forgottenDeadline)
                    yield return null;
                Assert.That(memorial.ChildLineText, Does.Contain("...the bridge"));

                input.Press(keys.aKey);
                float stageDeadline = Time.realtimeSinceStartup + 5f;
                while (!memorial.CanPromptChild &&
                       Time.realtimeSinceStartup < stageDeadline)
                    yield return null;
                input.Release(keys.aKey);
                yield return null;
                Assert.That(memorial.CanPromptChild, Is.True,
                    "Xiaodou can only be prompted after the child actually forgets the line.");
                input.Press(keys.eKey);
                yield return null;
                input.Release(keys.eKey);
                yield return null;
                Assert.That(memorial.Phase, Is.EqualTo(4));

                memorial.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "M00 choice 0")
                    .onClick.Invoke();
                yield return null;
                Assert.That(memorial.XiaodouPrompted, Is.True);
                Assert.That(memorial.Phase, Is.EqualTo(5));
                Assert.That(memorial.CurrentNarration, Does.Contain("Who has spare rope?"),
                    "M00 must echo the exact P0 rope call instead of using an altered English paraphrase.");
                Assert.That(memorial.CurrentNarration, Does.Not.Contain("Who brought the spare rope?"));
                memorial.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "M00 return to entry")
                    .onClick.Invoke();
                yield return null;
                Assert.That(entry.MemorialSquareOpen, Is.False);
                Assert.That(SoundwellInputAdapter.IsEntryContextActive, Is.True);
                memorial = null;
                testBodyCompleted = true;
            }
            finally
            {
                // Keep teardown from masking the real PlayMode assertion/exception.
                // Each operation is isolated so the next run reports the exact failing step.
                try
                {
                    if (memorial != null)
                    {
                        GameObject memorialObject = memorial.gameObject;
                        if (memorialObject != null)
                            Object.Destroy(memorialObject);
                    }
                }
                catch (System.Exception exception)
                {
                    cleanupFailure = "M00 object cleanup failed: " + exception;
                }

                try
                {
                    if (!string.IsNullOrEmpty(originalLanguage))
                        SoundwellUiLocalization.SetLanguage(originalLanguage);
                }
                catch (System.Exception exception)
                {
                    cleanupFailure = cleanupFailure ?? ("M00 locale cleanup failed: " + exception);
                }

                try
                {
                    if (inputReady && input != null)
                        input.TearDown();
                }
                catch (System.Exception exception)
                {
                    cleanupFailure = cleanupFailure ?? ("M00 InputTestFixture cleanup failed: " + exception);
                }
            }
            yield return new ExitPlayMode();
            if (testBodyCompleted && !string.IsNullOrEmpty(cleanupFailure))
                Assert.Fail(cleanupFailure);
        }

        [UnityTest]
        public IEnumerator MemorialOpeningIsChainedOnlyAfterTheEarnedThirdBell()
        {
            // Do not keep an InputTestFixture across EnterPlayMode's domain
            // reload; a pre-reload object can turn into a null test runtime.
            InputTestFixture input = null;
            bool inputReady = false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                yield return new EnterPlayMode(expectDomainReload: true);
                yield return null;
                input = new InputTestFixture();
                input.Setup();
                inputReady = true;
                InputSystem.AddDevice<Keyboard>();
                EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/Game/Scenes/Bootstrap.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));
                float entranceDeadline = Time.realtimeSinceStartup + 8f;
                SoundwellEntrance entrance = null;
                while (entrance == null && Time.realtimeSinceStartup < entranceDeadline)
                {
                    yield return null;
                    entrance = Object.FindFirstObjectByType<SoundwellEntrance>();
                }
                Assert.That(entrance, Is.Not.Null);

                var currentPrelude = typeof(SoundwellEntrance).GetField("currentPrelude",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var thirdBell = typeof(WhitewaterPrelude).GetField("thirdBellStarted",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(currentPrelude, Is.Not.Null);
                Assert.That(thirdBell, Is.Not.Null);

                entrance.OpenPrelude();
                yield return null;
                var prelude = (WhitewaterPrelude)currentPrelude.GetValue(entrance);
                Assert.That(prelude, Is.Not.Null);
                thirdBell.SetValue(prelude, false);
                prelude.Close();
                yield return null;
                Assert.That(entrance.MemorialSquareOpen, Is.False,
                    "Leaving S00 before the earned third bell must not jump to M00.");

                entrance.OpenPrelude();
                yield return null;
                prelude = (WhitewaterPrelude)currentPrelude.GetValue(entrance);
                Assert.That(prelude, Is.Not.Null);
                thirdBell.SetValue(prelude, true);
                prelude.Close();
                yield return null;
                Assert.That(entrance.MemorialSquareOpen, Is.True);
                var memorial = Object.FindFirstObjectByType<M00MemorialSquare>();
                Assert.That(memorial, Is.Not.Null);
                memorial.Close();
                yield return null;
                Assert.That(entrance.MemorialSquareOpen, Is.False);
                Assert.That(SoundwellInputAdapter.IsEntryContextActive, Is.True);
            }
            finally
            {
                var memorial = Object.FindFirstObjectByType<M00MemorialSquare>();
                if (memorial != null)
                    memorial.Close();
                var prelude = Object.FindFirstObjectByType<WhitewaterPrelude>();
                if (prelude != null)
                    prelude.Close();
                if (inputReady && input != null)
                    input.TearDown();
            }
            yield return new ExitPlayMode();
        }
    }
}