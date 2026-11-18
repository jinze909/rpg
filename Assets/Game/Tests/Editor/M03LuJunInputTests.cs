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
                input.Press(gamepad.buttonSouth);
                yield return null;
                yield return null;
                input.Release(gamepad.buttonSouth);
                yield return null;
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
    }
}
