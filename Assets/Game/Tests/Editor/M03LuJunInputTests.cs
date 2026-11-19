using System.Collections;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rpg.Tests.Editor
{
    public sealed class M03LuJunInputTests
    {
        [UnityTest]
        public IEnumerator ShipOpeningMovesGamepadFocusOffTheReusedOfficialVoyageButton()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return null;

            var input = new InputTestFixture();
            input.Setup();
            var owner = new GameObject("M03 Lu Jun runtime focus");
            M03LuJunView view = null;
            try
            {
                Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
                EventSystem events = SoundwellInputAdapter.EnsureEventSystem(owner.transform);
                SoundwellInputAdapter.SetUiContext();
                int delivered = 0;
                view = new M03LuJunView(owner.transform, M02BridgeEvidence.None,
                    true, _ => delivered++);

                // A fresh EventSystem must activate its input module before
                // the first press, just as it does on the normal entry route.
                yield return null;
                yield return null;
                Assert.That(events.currentInputModule, Is.TypeOf<InputSystemUIInputModule>());
                events.SetSelectedGameObject(view.AdvanceButton.gameObject);
                for (int beat = 1; beat <= 4; beat++)
                {
                    input.Press(gamepad.buttonSouth);
                    yield return null;
                    yield return null;
                    input.Release(gamepad.buttonSouth);
                    yield return null;
                    if (beat <= 3)
                    {
                        Assert.That(view.Encounter.Stage,
                            Is.EqualTo(M03LuJunStage.VisitingVeterans));
                        Assert.That(view.RollCallBeat, Is.EqualTo(beat));
                        Assert.That(events.currentSelectedGameObject,
                            Is.SameAs(view.AdvanceButton.gameObject));
                    }
                }
                Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingOpening));
                Assert.That(events.currentSelectedGameObject,
                    Is.SameAs(view.GetChoiceButton(0).gameObject));

                // This physical button changes meaning from "I need a ship"
                // to "official ship" after the opening answer has been chosen.
                events.SetSelectedGameObject(view.GetChoiceButton(2).gameObject);
                input.Press(gamepad.buttonSouth);
                yield return null;
                yield return null;
                input.Release(gamepad.buttonSouth);
                yield return null;

                Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));
                Assert.That(view.GetChoiceButton(2).gameObject.activeInHierarchy, Is.True);
                Assert.That(events.currentSelectedGameObject,
                    Is.SameAs(view.GetChoiceButton(0).gameObject));
                Assert.That(view.Encounter.Voyage, Is.Null);
                Assert.That(delivered, Is.Zero);
            }
            finally
            {
                view?.Dispose();
                Object.Destroy(owner);
                input.TearDown();
            }

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RefusingSecrecyWithGamepadDoesNotBookAShipUntilASeparateSubmit()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return null;

            var input = new InputTestFixture();
            input.Setup();
            var owner = new GameObject("M03 secrecy to voyage gamepad regression");
            M03LuJunView view = null;
            try
            {
                Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
                EventSystem events = SoundwellInputAdapter.EnsureEventSystem(owner.transform);
                SoundwellInputAdapter.SetUiContext();
                int delivered = 0;
                view = new M03LuJunView(owner.transform, M02BridgeEvidence.FerrymanLog,
                    true, _ => delivered++);

                // The roll call is covered by the preceding gamepad test.
                // Exercise the three distinct dialogue transitions with real
                // Input System Submit, where a reused button changes meaning.
                for (int i = 0; i < 4; i++)
                    view.AdvanceButton.onClick.Invoke();
                yield return null;
                yield return null;
                Assert.That(events.currentInputModule, Is.TypeOf<InputSystemUIInputModule>());
                Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingOpening));

                events.SetSelectedGameObject(view.GetChoiceButton(0).gameObject);
                input.Press(gamepad.buttonSouth);
                yield return null;
                yield return null;
                input.Release(gamepad.buttonSouth);
                yield return null;
                Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.RespondingToConfession));
                Assert.That(view.Encounter.Voyage, Is.Null);

                events.SetSelectedGameObject(view.GetChoiceButton(1).gameObject);
                input.Press(gamepad.buttonSouth);
                yield return null;
                yield return null;
                input.Release(gamepad.buttonSouth);
                yield return null;
                Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ReplyingToSecrecyRequest));
                Assert.That(view.Encounter.Secrecy, Is.EqualTo(M03SecrecyAnswer.NotTriggered));

                // Option 2 means "I cannot promise secrecy" here, but becomes
                // an official voyage button on the next screen.
                events.SetSelectedGameObject(view.GetChoiceButton(2).gameObject);
                input.Press(gamepad.buttonSouth);
                yield return null;
                yield return null;
                input.Release(gamepad.buttonSouth);
                yield return null;
                Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));
                Assert.That(view.Encounter.Secrecy, Is.EqualTo(M03SecrecyAnswer.RefuseSecrecy));
                Assert.That(view.Encounter.Voyage, Is.Null);
                Assert.That(delivered, Is.Zero,
                    "The secrecy response must not itself confirm a departure route.");
                Assert.That(events.currentSelectedGameObject,
                    Is.SameAs(view.GetChoiceButton(0).gameObject),
                    "New choices must start on a safe focus target, not the reused official ship.");

                input.Press(gamepad.buttonSouth);
                yield return null;
                yield return null;
                input.Release(gamepad.buttonSouth);
                yield return null;
                Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.Complete));
                Assert.That(view.Encounter.Voyage, Is.EqualTo(M03VoyageChoice.LuJunEscort));
                Assert.That(view.Encounter.Secrecy, Is.EqualTo(M03SecrecyAnswer.RefuseSecrecy));
                Assert.That(delivered, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                Object.Destroy(owner);
                input.TearDown();
            }

            yield return new ExitPlayMode();
        }

    }
}
