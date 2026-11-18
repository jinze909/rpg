using System.Collections;
using System.Linq;
using System.Reflection;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
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
            // Create the gameplay iterator after Unity reloads its domain.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return null;
            yield return ExerciseMemorialAfterPlayModeReload();
            yield return new ExitPlayMode();
        }

        private static IEnumerator ExerciseMemorialAfterPlayModeReload()
        {
            InputTestFixture input = null;
            bool inputReady = false;
            string originalLanguage = null;
            M00MemorialSquare memorial = null;
            SoundwellEntrance entry = null;
            try
            {
                input = new InputTestFixture();
                input.Setup();
                inputReady = true;
                originalLanguage = SoundwellUiLocalization.Language;
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Keyboard keys = InputSystem.AddDevice<Keyboard>();
                Gamepad gamepad = InputSystem.AddDevice<Gamepad>();

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

                var currentPrelude = typeof(SoundwellEntrance).GetField("currentPrelude",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var thirdBell = typeof(WhitewaterPrelude).GetField("thirdBellStarted",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(currentPrelude, Is.Not.Null);
                Assert.That(thirdBell, Is.Not.Null);

                entry.OpenPrelude();
                yield return null;
                var prelude = (WhitewaterPrelude)currentPrelude.GetValue(entry);
                Assert.That(prelude, Is.Not.Null);
                thirdBell.SetValue(prelude, true);
                prelude.Close();

                yield return null;
                Assert.That(entry.enabled, Is.True, "The Bootstrap owner must remain enabled during M00.");
                memorial = entry.CurrentMemorialSquare;
                Assert.That(memorial, Is.Not.Null,
                    "The earned third bell must open the authored M00 memorial scene.");
                Assert.That(entry.MemorialSquareOpen, Is.True);
                Assert.That(memorial.transform.parent, Is.EqualTo(entry.transform),
                    "M00 must be owned by the real entry controller.");
                yield return null;
                AssertMemorialFitsViewport(memorial);

                Assert.That(memorial.Phase, Is.Zero);
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
                var variables = Maki.Game.Variables;
                Assert.That(variables.GetInt(M00MemorialSquare.XiaodouPromptedKey), Is.EqualTo(1));
                variables.Set(M00RollCallFacts.IdentityRevealKey, 0);
                variables.Set(M00HanRansomFacts.CountKey, 0);
                variables.Set(M01HomecomingResponse.PensionChoiceKey, 0);

                Assert.That(EventSystem.current.currentSelectedGameObject.name,
                    Is.EqualTo("M00 begin roll call"));
                input.Press(gamepad.buttonSouth);
                yield return null;
                input.Release(gamepad.buttonSouth);
                yield return null;

                var rollCall = memorial.GetComponent<M00RollCallSession>();
                Assert.That(rollCall, Is.Not.Null);
                Assert.That(rollCall.Sequence.CallsMade, Is.EqualTo(1));
                Assert.That(rollCall.Sequence.Outcome, Is.EqualTo(M00RollCallOutcome.Pending),
                    "The Submit that opens the ritual cannot also answer the first call.");
                Assert.That(EventSystem.current.currentSelectedGameObject,
                    Is.EqualTo(rollCall.View.GetChoiceButton(0).gameObject));

                rollCall.View.GetChoiceButton(1).onClick.Invoke();
                float nextCallDeadline = Time.realtimeSinceStartup + 4f;
                while (rollCall.Sequence.CallsMade == 1 && Time.realtimeSinceStartup < nextCallDeadline)
                    yield return null;
                Assert.That(rollCall.Sequence.CallsMade, Is.EqualTo(2));
                rollCall.View.GetChoiceButton(0).onClick.Invoke();
                Assert.That(rollCall.Sequence.CallsMade, Is.EqualTo(2));
                rollCall.View.ContinueButton.onClick.Invoke();
                Assert.That(M00RollCallFacts.TryRead(out M00RollCallOutcome outcome), Is.True);
                Assert.That(outcome, Is.EqualTo(M00RollCallOutcome.AnsweredSecond));
                Assert.That(variables.GetInt(M00HanRansomFacts.CountKey), Is.Zero);

                Button meetingChoice = NamedButton(memorial, "M00 Han Qiao choice 0");
                Assert.That(EventSystem.current.currentSelectedGameObject,
                    Is.EqualTo(meetingChoice.gameObject));
                Assert.That(meetingChoice.GetComponentInChildren<Text>().text,
                    Is.EqualTo("The battle report says I died."));
                meetingChoice.onClick.Invoke();
                Assert.That(variables.GetInt(M00HanRansomFacts.CountKey), Is.EqualTo(41));
                NamedButton(memorial, "M00 Han Qiao continue").onClick.Invoke();
                NamedButton(memorial, "M00 Han Qiao choice 1").onClick.Invoke();
                NamedButton(memorial, "M00 Han Qiao continue").onClick.Invoke();

                Button westLaneProgress = NamedButton(memorial, "M01 West Lane continue");
                Assert.That(EventSystem.current.currentSelectedGameObject,
                    Is.EqualTo(westLaneProgress.gameObject));
                westLaneProgress.onClick.Invoke();
                Assert.That(westLaneProgress.GetComponentInParent<Canvas>()
                    .GetComponentsInChildren<Text>().Any(label =>
                        label.text.Contains("answered the roll call in the square today")), Is.True,
                    "Xiaodou recognizes the recorded public answer before the separate stage prompt.");
                westLaneProgress.onClick.Invoke();
                NamedButton(memorial, "M01 pension choice 2").onClick.Invoke();
                Assert.That(variables.GetInt(M01HomecomingResponse.PensionChoiceKey), Is.Zero);
                westLaneProgress.onClick.Invoke();
                yield return null;
                Assert.That(variables.GetInt(M01HomecomingResponse.PensionChoiceKey), Is.EqualTo(3),
                    "This records the request for review, while the narrative pension path stays unresolved.");
                Assert.That(M01HomecomingResponse.PensionPathFor(M01PensionChoice.RequestMissingReturnReview),
                    Is.EqualTo("未处理"));
                Assert.That(entry.MemorialSquareOpen, Is.True);
                yield return VisitHarborThroughTheWarehouse(memorial);
                Assert.That(entry.MemorialSquareOpen, Is.False);
                Assert.That(SoundwellInputAdapter.IsEntryContextActive, Is.True);
                memorial = null;
            }
            finally
            {
                // ExitPlayMode owns the remaining scene teardown.
                if (!string.IsNullOrEmpty(originalLanguage))
                    SoundwellUiLocalization.SetLanguage(originalLanguage);
                if (inputReady && input != null)
                    input.TearDown();
            }
        }

        private static void AssertMemorialFitsViewport(M00MemorialSquare memorial)
        {
            RectTransform authored = memorial.GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.gameObject.name == "M00 responsive authored view");
            RectTransform playerRect = memorial.GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.gameObject.name == "M00 player");
            RectTransform titleRect = memorial.GetComponentsInChildren<Text>(true)
                .Single(label => label.text == "Your Name Carved in Stone").rectTransform;
            RectTransform instructionsRect = memorial.GetComponentsInChildren<Text>(true)
                .Single(label => label.text == memorial.CurrentInstructions).rectTransform;
            RectTransform rollCallRect = memorial.GetComponentsInChildren<Button>(true)
                .Single(button => button.gameObject.name == "M00 begin roll call")
                .GetComponent<RectTransform>();
            RectTransform choiceRect = memorial.GetComponentsInChildren<Button>(true)
                .Single(button => button.gameObject.name == "M00 choice 3")
                .GetComponent<RectTransform>();
            var visibleRects = new[] { playerRect, titleRect, instructionsRect,
                rollCallRect, choiceRect };
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
                (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height));
        }

        private static IEnumerator VisitHarborThroughTheWarehouse(M00MemorialSquare memorial)
        {
            var variables = Maki.Game.Variables;
            variables.Set(M02OfficialDockFacts.LivingRegistrationStampedKey, 1);
            variables.Set(M02MissingReturnFacts.ApprovalKey, 1);
            variables.Set(M02BridgeEvidenceFacts.EvidenceKey, (int)M02BridgeEvidence.ThirdBellSignal);

            NamedButton(memorial, "M01 dock option 2").onClick.Invoke();
            NamedButton(memorial, "M01 dock continue").onClick.Invoke();
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(NamedButton(memorial, "M01 dock option 0").gameObject));
            for (int route = 0; route < 3; route++)
                NamedButton(memorial, "M01 dock option " + route).onClick.Invoke();
            Assert.That(variables.GetInt(M01HomecomingResponse.PensionChoiceKey), Is.EqualTo(3));

            NamedButton(memorial, "M01 dock continue").onClick.Invoke();
            yield return null;
            Assert.That(M02OfficialDockFacts.TryReadEffective(out bool official), Is.True);
            Assert.That(official, Is.False, "An earlier replay stamp cannot approve this visit.");
            Assert.That(M02BridgeEvidenceFacts.TryRead(out M02BridgeEvidence observed), Is.True);
            Assert.That(observed, Is.EqualTo(M02BridgeEvidence.None));
            Assert.That(memorial.GetComponentsInChildren<Button>().Any(button =>
                button.gameObject.name == "M02 office action 0"), Is.False,
                "Asking for missing-return review does not enable living registration.");
            NamedButton(memorial, "M02 office action 1").onClick.Invoke();
            NamedButton(memorial, "M02 harbor leave").onClick.Invoke();
            yield return null;
            Assert.That(M02QiQueDebtFacts.TryRead(out bool debtKnown), Is.True);
            Assert.That(debtKnown, Is.True);

            NamedButton(memorial, "M02 evidence 0").onClick.Invoke();
            M02BridgeEvidenceFacts.TryRead(out observed);
            Assert.That(observed, Is.EqualTo(M02BridgeEvidence.CleanCableCut),
                "An inspected clue must reach the story store before leaving the warehouse.");
            NamedButton(memorial, "M02 continue").onClick.Invoke();
            NamedButton(memorial, "M02 evidence 0").onClick.Invoke();
            NamedButton(memorial, "M02 continue").onClick.Invoke();
            yield return null;

            Assert.That(memorial.GetComponentsInChildren<Button>().Any(button =>
                button.gameObject.name == "M02 stamp approved review"), Is.False);
            NamedButton(memorial, "M02 ask for review").onClick.Invoke();
            M02OfficialDockFacts.TryReadEffective(out official);
            Assert.That(official, Is.False, "Requesting review never unlocks an official ship.");
            NamedButton(memorial, "M02 stamp approved review").onClick.Invoke();
            NamedButton(memorial, "M02 return to harbor").onClick.Invoke();
            yield return null;

            Assert.That(M02OfficialDockFacts.TryReadEffective(out official), Is.True);
            Assert.That(official, Is.True);
            Assert.That(M02MissingReturnFacts.TryReadApproved(out bool approved), Is.True);
            Assert.That(approved, Is.True);
            Assert.That(variables.GetInt(M02OfficialDockFacts.LivingRegistrationStampedKey), Is.Zero);
            Assert.That(variables.GetInt(M01HomecomingResponse.PensionChoiceKey),
                Is.EqualTo(3), "M02 must retain the actual choice made in West Lane.");
            M02BridgeEvidenceFacts.TryRead(out observed);
            Assert.That(observed, Is.EqualTo(M02BridgeEvidence.CleanCableCut |
                M02BridgeEvidence.WarReportDraft));

            // M02 now continues to the night warehouse before returning to Entry.
            // Asking Han Qiao about the loan must be the only reason Qi Que
            // discloses it; the office result has already been persisted.
            Button nightContinue = NamedButton(memorial, "M02 Qi Que continue");
            Assert.That(nightContinue.gameObject.activeInHierarchy, Is.True);
            Assert.That(memorial.GetComponentsInChildren<Text>().Any(text =>
                text.text.Contains("Qi Que is about to offer another osmanthus cake")), Is.True);
            nightContinue.onClick.Invoke();
            Assert.That(memorial.GetComponentsInChildren<Text>().Any(text =>
                text.text.Contains("mortgaged")), Is.True);
            Assert.That(memorial.GetComponentsInChildren<Button>().Any(button =>
                button.gameObject.name == "M02 office action 1"), Is.False,
                "The earlier office choices cannot remain actionable at night.");
            nightContinue.onClick.Invoke();
            nightContinue.onClick.Invoke();
            Assert.That(memorial.GetComponentsInChildren<Text>().Any(text =>
                text.text.Contains("not booked a ship")), Is.True,
                "Reading Qi Que's chart must not silently select the route.");
            nightContinue.onClick.Invoke();
            yield return null;
        }

        private static Button NamedButton(M00MemorialSquare memorial, string name)
        {
            return memorial.GetComponentsInChildren<Button>()
                .Single(button => button.gameObject.name == name);
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
                var memorial = entrance.CurrentMemorialSquare;
                Assert.That(memorial, Is.Not.Null);
                memorial.Close();
                Assert.That(entrance.OpenTrial(), Is.Null,
                    "A callback in the same frame as leaving M00 must not launch the trial.");
                entrance.OpenPrelude();
                Assert.That(currentPrelude.GetValue(entrance), Is.Null,
                    "Leaving M00 must not reopen the prologue on the same frame.");
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
