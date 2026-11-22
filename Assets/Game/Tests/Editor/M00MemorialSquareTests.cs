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
                AssertAuthoredImage(memorial, "M00 player", "ferry_guide_idle");
                AssertAuthoredImage(memorial, "M00 distant harbor water", "day_water");
                AssertAuthoredImage(memorial, "M00 memorial square paving", "square_paving");
                AssertAuthoredImage(memorial, "M00 memorial stone", "memorial_polished_stele");
                AssertAuthoredImage(memorial, "M00 memorial statue", "memorial_statue");
                AssertAuthoredImage(memorial, "M00 small stage", "memorial_small_stage");
                AssertAuthoredImage(memorial, "M00 old woman wiping Hao Lie’s name", "memorial_woman_wiping");
                AssertAuthoredImage(memorial, "M00 blue-cuffed watcher left", "blue_cuff_veteran");
                AssertAuthoredImage(memorial, "M00 Xiaodou in oversized uniform", "xiaodou_costumed_sugar_figure");
                var squareCanvas = memorial.GetComponentsInChildren<Canvas>(true)
                    .Single(canvas => canvas.gameObject.name == "M00 memorial square canvas");
                SoundwellSmokeHost.CaptureStoryCanvas(squareCanvas, "m00-memorial-square");

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
                AssertStageDialogueDoesNotOverlapNarration(memorial);

                float forgottenDeadline = Time.realtimeSinceStartup + 3f;
                while (!memorial.ChildLineText.Contains("...the bridge") &&
                       Time.realtimeSinceStartup < forgottenDeadline)
                    yield return null;
                Assert.That(memorial.ChildLineText, Does.Contain("...the bridge"));
                AssertStageDialogueDoesNotOverlapNarration(memorial);

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
                AssertStageDialogueDoesNotOverlapNarration(memorial);
                AssertChoicePanelDoesNotCoverPlayableWorld(memorial);
                SoundwellSmokeHost.CaptureStoryCanvas(squareCanvas, "m00-memorial-square-choices");

                memorial.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "M00 choice 0")
                    .onClick.Invoke();
                yield return null;
                Assert.That(memorial.XiaodouPrompted, Is.True);
                AssertAuthoredImage(memorial, "M00 Xiaodou in oversized uniform", "xiaodou_prompted");
                Assert.That(memorial.Phase, Is.EqualTo(5));
                AssertStageDialogueDoesNotOverlapNarration(memorial);
                AssertChoicePanelDoesNotCoverPlayableWorld(memorial);
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
                Assert.That(memorial.CurrentInstructions, Does.Contain("the last page of names"));
                AssertMemorialFitsViewport(memorial);
                SoundwellSmokeHost.CaptureStoryCanvas(squareCanvas, "m00-memorial-square-roll-call");
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

        private static void AssertAuthoredImage(M00MemorialSquare memorial, string objectName, string spriteName)
        {
            Image image = memorial.GetComponentsInChildren<Image>(true)
                .Single(child => child.name == objectName);
            Assert.That(image.isActiveAndEnabled, Is.True, objectName);
            Assert.That(image.raycastTarget, Is.False, objectName);
            Assert.That(image.sprite, Is.Not.Null, objectName);
            Assert.That(image.sprite.name, Is.EqualTo(spriteName));
            Assert.That(UnityEditor.AssetDatabase.GetAssetPath(image.sprite),
                Does.StartWith("Assets/Game/Art/"));
        }

        private static void AssertChoicePanelDoesNotCoverPlayableWorld(M00MemorialSquare memorial)
        {
            RectTransform authored = memorial.GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.gameObject.name == "M00 responsive authored view");
            RectTransform panel = authored.Find("M00 choice panel") as RectTransform;
            RectTransform paving = authored.Find("M00 memorial square paving") as RectTransform;
            Bounds panelBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(authored, panel);
            Bounds pavingBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(authored, paving);
            Assert.That(panelBounds.min.y, Is.GreaterThan(pavingBounds.max.y),
                "M00 choice controls must stay above the playable paving in the non-walkable water band.");
            Assert.That(panel.GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(panel.Find("M00 choice panel backing").GetComponent<Image>().raycastTarget, Is.False);

            foreach (string actorName in new[]
            {
                "M00 player",
                "M00 memorial statue",
                "M00 old woman wiping Hao Lie’s name",
                "M00 Xiaodou in oversized uniform"
            })
            {
                RectTransform actor = memorial.GetComponentsInChildren<RectTransform>(true)
                    .Single(rect => rect.gameObject.name == actorName);
                Bounds actorBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(authored, actor);
                Assert.That(panelBounds.Intersects(actorBounds), Is.False,
                    "M00 choice panel must not cover " + actorName + ".");
            }

            Text narration = memorial.GetComponentsInChildren<Text>(true)
                .Single(label => label.text == memorial.CurrentNarration);
            Bounds narrationBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                authored, narration.rectTransform);
            Assert.That(panelBounds.Intersects(narrationBounds), Is.False,
                "M00 choice panel must not cover the scene narration.");

            foreach (Button button in memorial.GetComponentsInChildren<Button>(true)
                .Where(button => button.gameObject.name.StartsWith("M00 choice ") ||
                    button.gameObject.name == "M00 begin roll call"))
            {
                Bounds buttonBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    authored, button.GetComponent<RectTransform>());
                Assert.That(panelBounds.Contains(buttonBounds.min) &&
                    panelBounds.Contains(buttonBounds.max), Is.True,
                    button.gameObject.name + " must remain inside its reserved panel.");
            }
        }

        private static void AssertStageDialogueDoesNotOverlapNarration(M00MemorialSquare memorial)
        {
            RectTransform authored = memorial.GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.gameObject.name == "M00 responsive authored view");
            Text narration = authored.Find("M00 scene narration").GetComponent<Text>();
            Text childLine = authored.Find("M00 Xiaodou stage line").GetComponent<Text>();
            Text crowdLine = authored.Find("M00 crowd reaction").GetComponent<Text>();
            Bounds narrationBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                authored, narration.rectTransform);
            Bounds childBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                authored, childLine.rectTransform);
            Assert.That(childLine.gameObject.activeSelf, Is.True);
            Assert.That(childBounds.Intersects(narrationBounds), Is.False,
                "The stage line must have its own readable lane below scene narration.");
            Assert.That(childBounds.max.y, Is.LessThan(narrationBounds.min.y),
                "The stage line must remain fully below the narration box.");

            RectTransform stage = authored.Find("M00 small stage") as RectTransform;
            RectTransform child = authored.Find("M00 Xiaodou in oversized uniform") as RectTransform;
            RectTransform leftWatcher = authored.Find("M00 blue-cuffed watcher left") as RectTransform;
            RectTransform rightWatcher = authored.Find("M00 blue-cuffed watcher right") as RectTransform;
            foreach (RectTransform actor in new[] { stage, child, leftWatcher, rightWatcher })
            {
                Bounds actorBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    authored, actor);
                Assert.That(childBounds.Intersects(actorBounds), Is.False,
                    "The stage line must not cover " + actor.name + ".");
            }

            if (!crowdLine.gameObject.activeSelf)
                return;
            Bounds crowdBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                authored, crowdLine.rectTransform);
            Assert.That(crowdBounds.Intersects(narrationBounds), Is.False,
                "The crowd reaction must not overlap scene narration.");
            Assert.That(crowdBounds.Intersects(childBounds), Is.False,
                "The crowd reaction must occupy a separate line below Xiaodou's dialogue.");
            Assert.That(crowdBounds.max.y, Is.LessThan(childBounds.min.y),
                "The crowd reaction must remain below the child's line.");
            foreach (RectTransform actor in new[] { stage, child, leftWatcher, rightWatcher })
            {
                Bounds actorBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    authored, actor);
                Assert.That(crowdBounds.Intersects(actorBounds), Is.False,
                    "The crowd reaction must not cover " + actor.name + ".");
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
            Image footer = authored.Find("M00 objective footer").GetComponent<Image>();
            Image footerBacking = footer.transform.Find("M00 objective footer backing").GetComponent<Image>();
            Text instructions = footerBacking.GetComponentInChildren<Text>();
            RectTransform paving = (RectTransform)authored.Find("M00 memorial square paving");
            RectTransform rollCallRect = memorial.GetComponentsInChildren<Button>(true)
                .Single(button => button.gameObject.name == "M00 begin roll call")
                .GetComponent<RectTransform>();
            RectTransform choicePanelRect = authored.Find("M00 choice panel") as RectTransform;
            RectTransform stageLineRect = authored.Find("M00 Xiaodou stage line") as RectTransform;
            RectTransform crowdLineRect = authored.Find("M00 crowd reaction") as RectTransform;
            RectTransform[] choiceRects = memorial.GetComponentsInChildren<Button>(true)
                .Where(button => button.gameObject.name.StartsWith("M00 choice "))
                .Select(button => button.GetComponent<RectTransform>()).ToArray();
            Assert.That(instructions.text, Is.EqualTo(memorial.CurrentInstructions));
            Assert.That(instructions.isActiveAndEnabled, Is.True);
            Assert.That(instructions.raycastTarget || footer.raycastTarget || footerBacking.raycastTarget,
                Is.False, "The footer cannot intercept navigation or Submit.");
            Assert.That(instructions.color.grayscale - footerBacking.color.grayscale, Is.GreaterThan(0.5f));

            Bounds footerBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                authored, footer.rectTransform);
            Assert.That(footerBounds.max.y, Is.LessThan(
                RectTransformUtility.CalculateRelativeRectTransformBounds(authored, paving).min.y),
                "The objective strip must sit below the playable paving.");
            Assert.That(footerBounds.max.y, Is.LessThan(
                RectTransformUtility.CalculateRelativeRectTransformBounds(authored, rollCallRect).min.y),
                "The objective strip must leave the roll-call button unobstructed.");
            Bounds choicePanelBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                authored, choicePanelRect);
            Bounds pavingBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(authored, paving);
            Assert.That(choicePanelBounds.min.y, Is.GreaterThan(pavingBounds.max.y),
                "The choice panel must not cover the playable paving.");
            var visibleRects = new[] { playerRect, titleRect, footer.rectTransform,
                rollCallRect, choicePanelRect, stageLineRect, crowdLineRect }
                .Concat(choiceRects).ToArray();
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

            // The current opening now leads to Lu Jun's camp instead of
            // returning to Entry as soon as Qi Que's night conversation ends.
            // Finish the real UGUI handoff and assert the chosen voyage is
            // persisted, rather than weakening the old Entry assertion.
            Button campRoll = NamedButton(memorial, "M03 Lu Jun advance");
            Assert.That(campRoll.gameObject.activeInHierarchy, Is.True);
            for (int beat = 0; beat < 6 && campRoll.gameObject.activeInHierarchy; beat++)
                campRoll.onClick.Invoke();
            Assert.That(campRoll.gameObject.activeInHierarchy, Is.False,
                "The shore roll must finish before Lu Jun offers dialogue choices.");
            NamedButton(memorial, "M03 Lu Jun option 2").onClick.Invoke();
            Button officialRoute = NamedButton(memorial, "M03 Lu Jun option 2");
            Assert.That(officialRoute.gameObject.activeInHierarchy, Is.True,
                "The stamped M02 approval must unlock Han Qiao's official route.");
            officialRoute.onClick.Invoke();
            yield return null;
            Assert.That(M03LuJunFacts.TryRead(out M03LuConfession confession,
                out M03SecrecyAnswer secrecy, out M03VoyageChoice? voyage), Is.True);
            Assert.That(confession, Is.EqualTo(M03LuConfession.NotAsked));
            Assert.That(secrecy, Is.EqualTo(M03SecrecyAnswer.NotTriggered));
            Assert.That(voyage, Is.EqualTo(M03VoyageChoice.HanQiaoOfficial));

            // M03 has now handed off to the real M04 dockside farewell.
            // The opening must remain active until the player actually
            // takes Xiaodou's sweet, chooses a reply, and says goodbye.
            Button farewellAdvance = NamedButton(memorial, "M04 Xiaodou advance");
            Assert.That(farewellAdvance.gameObject.activeInHierarchy, Is.True);
            Assert.That(variables.GetInt(M04XiaodouFarewellFacts.RealLineKey), Is.Zero,
                "A new voyage must not inherit the prior departure's answer.");

            farewellAdvance.onClick.Invoke();
            Assert.That(farewellAdvance.gameObject.activeInHierarchy, Is.False,
                "Taking the sweet is not the same as choosing what to teach.");
            Assert.That(variables.GetInt(M04XiaodouFarewellFacts.RealLineKey), Is.Zero);
            Button teachRealLine = NamedButton(memorial, "M04 Xiaodou choice 0");
            Assert.That(teachRealLine.gameObject.activeInHierarchy, Is.True);
            teachRealLine.onClick.Invoke();
            Assert.That(variables.GetInt(M04XiaodouFarewellFacts.RealLineKey), Is.Zero,
                "An unfinished farewell must not persist its answer.");
            Assert.That(farewellAdvance.gameObject.activeInHierarchy, Is.True);
            farewellAdvance.onClick.Invoke();
            yield return null;
            Assert.That(variables.GetInt(M04XiaodouFarewellFacts.RealLineKey), Is.EqualTo(1),
                "The true line must be recorded before the cup farewell.");

            Button cupAdvance = NamedButton(memorial, "M04 Qi cup continue");
            Assert.That(cupAdvance.gameObject.activeInHierarchy, Is.True);
            Assert.That(variables.GetInt(M04QiQueCupFacts.CupCarriedKey), Is.Zero,
                "The new voyage must not keep an old carried-cup flag.");
            cupAdvance.onClick.Invoke();
            Assert.That(cupAdvance.gameObject.activeInHierarchy, Is.False,
                "Inspecting the cup is not the same as accepting it.");
            NamedButton(memorial, "M04 Qi cup choice 1").onClick.Invoke();
            Assert.That(variables.GetInt(M04QiQueCupFacts.CupCarriedKey), Is.Zero);
            cupAdvance.onClick.Invoke();
            yield return null;
            Assert.That(variables.GetInt(M04QiQueCupFacts.CupCarriedKey), Is.EqualTo(1),
                "Only the completed physical handover writes the cup fact.");

            // The new live M04 opening continues to a route-specific dock
            // preflight. It may not return to Entry until the player has
            // actually heard all three checks; preflight itself cannot sail.
            Button preflight = NamedButton(memorial, "M04 dock preflight advance");
            Assert.That(preflight.gameObject.activeInHierarchy, Is.True);
            Assert.That(variables.GetInt(M04DepartureFacts.ConfirmedKey), Is.Zero);
            Assert.That(variables.GetInt(M04DepartureFacts.SailedKey), Is.Zero);
            preflight.onClick.Invoke();
            Assert.That(preflight.gameObject.activeInHierarchy, Is.True);
            preflight.onClick.Invoke();
            Assert.That(preflight.gameObject.activeInHierarchy, Is.True);
            preflight.onClick.Invoke();
            yield return null;
            Assert.That(variables.GetInt(M04DepartureFacts.SailedKey), Is.Zero,
                "Reading the preflight cannot create a cast-off fact.");
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

                var languageButton = GameObject.Find("Soundwell entry Language button")
                    ?.GetComponent<UnityEngine.UI.Button>();
                Assert.That(languageButton, Is.Not.Null);
                Assert.That(languageButton.IsActive(), Is.True);
                entrance.OpenPrelude();
                yield return null;
                Assert.That(languageButton.IsActive(), Is.False,
                    "Opening Whitewater must disable invisible entry controls.");
                var prelude = (WhitewaterPrelude)currentPrelude.GetValue(entrance);
                Assert.That(prelude, Is.Not.Null);
                thirdBell.SetValue(prelude, false);
                prelude.Close();
                yield return null;
                Assert.That(entrance.MemorialSquareOpen, Is.False,
                    "Leaving S00 before the earned third bell must not jump to M00.");
                Assert.That(languageButton.IsActive(), Is.True,
                    "Returning to Bootstrap must restore its controls.");

                entrance.OpenPrelude();
                yield return null;
                prelude = (WhitewaterPrelude)currentPrelude.GetValue(entrance);
                Assert.That(prelude, Is.Not.Null);
                thirdBell.SetValue(prelude, true);
                prelude.Close();
                yield return null;
                Assert.That(entrance.MemorialSquareOpen, Is.True);
                Assert.That(languageButton.IsActive(), Is.False,
                    "The memorial must keep the entry controls inactive.");
                var memorial = entrance.CurrentMemorialSquare;
                Assert.That(memorial, Is.Not.Null);
                memorial.Close();
                Assert.That(languageButton.IsActive(), Is.True);
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
