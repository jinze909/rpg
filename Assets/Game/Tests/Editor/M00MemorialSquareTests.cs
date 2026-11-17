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
            string cleanupFailure = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                // Let the normal runtime locale initializer run during PlayMode entry.
                // Select the test language only after the live Bootstrap owner exists.
                yield return new EnterPlayMode(expectDomainReload: true);
                yield return null;
                input = new InputTestFixture();
                input.Setup();
                inputReady = true;
                // Acquire Unity object references only after the PlayMode domain reload.
                M00MemorialSquare memorial = null;
                SoundwellEntrance entry = null;
                Keyboard keys = InputSystem.AddDevice<Keyboard>();
                Debug.Log("RPG_M00_LIFECYCLE_TEST: keyboard added " + (keys != null));
                Assert.That(keys, Is.Not.Null, "The M00 physical-approach test needs a live keyboard.");
                Assert.That(keys.eKey, Is.Not.Null);
                Debug.Log("RPG_M00_LIFECYCLE_TEST: authored copy and keyboard ready");

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
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Debug.Log("RPG_M00_LIFECYCLE_TEST: Bootstrap entry found");
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
                Assert.That(entry.enabled, Is.True,
                    "Entry remains enabled as the owner; the gameplay input context disables its Entry action map.");
                yield return null;
                Debug.Log("RPG_M00_LIFECYCLE_TEST: S00 opened");
                var prelude = (WhitewaterPrelude)currentPrelude.GetValue(entry);
                Assert.That(prelude, Is.Not.Null);
                thirdBell.SetValue(prelude, true);
                prelude.Close();
                Debug.Log("RPG_M00_LIFECYCLE_TEST: earned M00 handoff invoked");

                // The exit callback creates M00 under the live entry owner.
                // Let Unity finish the frame's deferred Destroy/activation work,
                // then read only that owner's child hierarchy (not a global search).
                Assert.That(entry != null && entry.gameObject.activeInHierarchy, Is.True,
                    "The S00 exit callback must keep its Bootstrap owner alive.");
                yield return null;
                Assert.That(entry != null && entry.gameObject.activeInHierarchy, Is.True,
                    "The Bootstrap owner must survive deferred S00 destruction.");
                Assert.That(entry.enabled, Is.True,
                    "Entry remains enabled as the owner while M00 owns the input context.");
                Assert.That(SceneManager.GetActiveScene().name,
                    Is.EqualTo(SoundwellEntrance.BootstrapSceneName),
                    "The M00 handoff must stay inside Bootstrap rather than reloading the trial.");
                memorial = (M00MemorialSquare)currentMemorial.GetValue(entry);
                Assert.That(entry.MemorialSquareOpen, Is.True,
                    "The earned third bell must hand off to M00.");
                Assert.That(memorial, Is.Not.Null,
                    "The earned third bell must open the authored M00 memorial scene.");
                Assert.That(entry.MemorialSquareOpen, Is.True);
                Assert.That(memorial.transform.parent, Is.EqualTo(entry.transform),
                    "M00 must be owned by the real entry controller.");
                Debug.Log("RPG_M00_LIFECYCLE_TEST: memorial remains under live owner");
                yield return null;
                Assert.That(memorial != null, Is.True, "M00 must remain active after the handoff frame.");

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
                Debug.Log("RPG_M00_LIFECYCLE_TEST: responsive view checked");

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
                Debug.Log("RPG_M00_LIFECYCLE_TEST: statue interaction completed");

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
                Debug.Log("RPG_M00_LIFECYCLE_TEST: old-woman interaction completed");

                float forgottenDeadline = Time.realtimeSinceStartup + 3f;
                while (!memorial.ChildLineText.Contains("...the bridge") &&
                       Time.realtimeSinceStartup < forgottenDeadline)
                    yield return null;
                Assert.That(memorial != null, Is.True, "M00 was destroyed before Xiaodou forgot the line.");
                Assert.That(memorial.ChildLineText, Does.Contain("...the bridge"));
                Debug.Log("RPG_M00_LIFECYCLE_TEST: Xiaodou forgets the line");

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
                Debug.Log("RPG_M00_LIFECYCLE_TEST: Xiaodou prompt completed");
                memorial.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "M00 return to entry")
                    .onClick.Invoke();
                yield return null;
                Assert.That(entry.MemorialSquareOpen, Is.False);
                Assert.That(entry.enabled, Is.True,
                    "Closing M00 returns to the entry screen without disabling its owner.");
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
                    M00MemorialSquare activeMemorial = Object.FindAnyObjectByType<M00MemorialSquare>(
                        FindObjectsInactive.Include);
                    if (activeMemorial != null)
                    {
                        GameObject memorialObject = activeMemorial.gameObject;
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
                Assert.That(entrance.enabled, Is.True);
                yield return null;
                var prelude = (WhitewaterPrelude)currentPrelude.GetValue(entrance);
                Assert.That(prelude, Is.Not.Null);
                thirdBell.SetValue(prelude, false);
                prelude.Close();
                yield return null;
                Assert.That(entrance.enabled, Is.True,
                    "An early S00 exit must leave the entry owner active.");
                Assert.That(entrance.MemorialSquareOpen, Is.False,
                    "Leaving S00 before the earned third bell must not jump to M00.");

                entrance.OpenPrelude();
                Assert.That(entrance.enabled, Is.True);
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
                Assert.That(entrance.enabled, Is.True,
                    "Closing M00 must reactivate the entry controller.");
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