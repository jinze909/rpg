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
        [Order(1)]
        [UnityTest]
        public IEnumerator MemorialOpeningRequiresPhysicalApproachAndRecordsAuthoredActions()
        {
            InputTestFixture input = null;
            bool inputReady = false;
            bool testBodyCompleted = false;
            string cleanupFailure = null;
            // Keep all cross-yield locals declared before domain reload.
            // Only acquire live Unity object instances after PlayMode resumes.
            M00MemorialSquare memorial = null;
            SoundwellEntrance entry = null;
            Keyboard keys = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                // Let the normal runtime locale initializer run during PlayMode entry.
                // Select the test language only after the live Bootstrap owner exists.
                Debug.Log("RPG_M00_STEP: pre EnterPlayMode");
                yield return new EnterPlayMode(expectDomainReload: true);
                Debug.Log("RPG_M00_STEP: resumed after EnterPlayMode");
                yield return null;
                Debug.Log("RPG_M00_STEP: resumed after first frame");

                // Bootstrap initializes the production action asset in Awake. Install
                // the isolated test runtime first so its setup cannot invalidate the
                // action maps that the live entry owner has already cached.
                Debug.Log("RPG_M00_STEP: before InputTestFixture setup");
                input = new InputTestFixture();
                input.Setup();
                inputReady = true;
                Debug.Log("RPG_M00_STEP: after InputTestFixture setup");

                // The second lifecycle test passes using this exact sequence:
                // install the virtual keyboard before Bootstrap initializes
                // its action maps, then load the production scene in PlayMode.
                // A device-free isolated runtime during LoadSceneInPlayMode
                // triggered a NullReferenceException before any M00 assertion.
                Debug.Log("RPG_M00_STEP: before virtual Keyboard");
                keys = InputSystem.AddDevice<Keyboard>();
                Assert.That(keys, Is.Not.Null);
                Debug.Log("RPG_M00_STEP: before Bootstrap load");
                EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/Game/Scenes/Bootstrap.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));
                Debug.Log("RPG_M00_STEP: after Bootstrap load");
                float entryDeadline = Time.realtimeSinceStartup + 8f;
                while (entry == null && Time.realtimeSinceStartup < entryDeadline)
                {
                    yield return null;
                    entry = Object.FindFirstObjectByType<SoundwellEntrance>();
                }
                Assert.That(entry, Is.Not.Null, "M00 requires the live Bootstrap input/UI owner.");

                Assert.That(keys.added, Is.True,
                    "The virtual keyboard must survive the same Bootstrap load as the passing handoff test.");
                Assert.That(keys.eKey, Is.Not.Null,
                    "The M00 physical-approach test needs live keyboard controls.");
                SoundwellInputAdapter.EnsureEventSystem(entry.transform);
                SoundwellInputAdapter.SetEntryContext();
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Debug.Log("RPG_M00_LIFECYCLE_TEST: Bootstrap and isolated keyboard ready");

                // Open M00 only through the production S00 exit callback. This
                // keeps the test on the same live-owner lifecycle as the player
                // path instead of manufacturing a second component attachment.
                entry.OpenPrelude();
                yield return null;
                entry = Object.FindFirstObjectByType<SoundwellEntrance>();
                Assert.That(entry, Is.Not.Null,
                    "Opening S00 must preserve the live Bootstrap entry owner.");
                var currentPreludeField = typeof(SoundwellEntrance).GetField(
                    "currentPrelude", BindingFlags.Instance | BindingFlags.NonPublic);
                var thirdBellField = typeof(WhitewaterPrelude).GetField(
                    "thirdBellStarted", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(currentPreludeField, Is.Not.Null);
                Assert.That(thirdBellField, Is.Not.Null);
                var prelude = (WhitewaterPrelude)currentPreludeField.GetValue(entry);
                Assert.That(prelude, Is.Not.Null,
                    "The live entry owner must create S00 before M00 can be earned.");
                thirdBellField.SetValue(prelude, true);
                prelude.Close();
                yield return null;
                entry = Object.FindAnyObjectByType<SoundwellEntrance>(
                    FindObjectsInactive.Include);
                Assert.That(entry != null, Is.True,
                    "The earned third-bell handoff must preserve a live Bootstrap entry owner.");
                // A UnityTest continuation can resume with stale managed object
                // references after the S00 object is destroyed. Resolve the
                // authored M00 through the live Bootstrap hierarchy, exactly
                // as the already-passing handoff regression does, then assert
                // that the owner also retained the same M00 instance.
                memorial = entry.GetComponentInChildren<M00MemorialSquare>(true);
                Assert.That(memorial != null, Is.True,
                    "The earned third-bell exit must create an M00 child on the live owner.");
                Assert.That(entry.MemorialSquareOpen, Is.True,
                    "The live entry owner must retain the M00 reference after S00 closes.");
                Assert.That(entry.CurrentMemorial, Is.SameAs(memorial),
                    "The owner must track the visible authored M00 child, not a stale instance.");
                Assert.That(memorial.transform.parent, Is.EqualTo(entry.transform));
                yield return null;
                Debug.Log("RPG_M00_LIFECYCLE_TEST: authored M00 interaction scene ready");

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
                Assert.That(entry.MemorialSquareOpen, Is.False,
                    "The authored M00 return button must clear the live entry owner's M00 reference.");
                Assert.That(entry.enabled, Is.True,
                    "The Bootstrap entry owner stays enabled during the authored M00 test.");
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

        [Order(2)]
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