using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rpg.Tests.Editor
{
    public sealed class M03LuJunViewTests
    {
        private GameObject root;
        private M03LuJunView view;
        private string priorLanguage;

        [SetUp]
        public void SetUp()
        {
            priorLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M03 Lu Jun test root");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (root != null) Object.DestroyImmediate(root);
            SoundwellUiLocalization.SetLanguage(priorLanguage);
        }

        [Test]
        public void AskingForShipNeverPromptsSecrecyAndOffersCivilianAlternative()
        {
            int callbacks = 0;
            M03LuJunEncounter result = null;
            view = new M03LuJunView(root.transform, M02BridgeEvidence.WarReportDraft,
                false, e => { callbacks++; result = e; });
            view.AdvanceButton.onClick.Invoke();
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));
            Assert.That(view.Encounter.Secrecy, Is.EqualTo(M03SecrecyAnswer.NotTriggered));
            Assert.That(view.GetChoiceButton(2).gameObject.activeSelf, Is.False);
            Assert.That(view.GetChoiceButton(3).gameObject.activeSelf, Is.False);
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));
            view.GetChoiceButton(4).onClick.Invoke();
            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.VoyagePathValue, Is.EqualTo("混合"));
        }

        [Test]
        public void AskingBridgeRequiresExplicitSecrecyDecision()
        {
            int callbacks = 0;
            view = new M03LuJunView(root.transform, M02BridgeEvidence.FerrymanLog,
                true, _ => callbacks++);
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            view.AdvanceButton.onClick.Invoke();
            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(view.Dialogue.text, Does.Contain("总帅令"));
            Assert.That(view.Encounter.Confession, Is.EqualTo(M03LuConfession.AskedWithEvidence));
            view.GetChoiceButton(1).onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ReplyingToSecrecyRequest));
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Encounter.Secrecy, Is.EqualTo(M03SecrecyAnswer.RefuseSecrecy));
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(view.Encounter.VoyagePathValue, Is.EqualTo("韩峤船坞"));
        }

        [Test]
        public void ShipOpeningCannotRetainFocusOnAnOfficialVoyageButton()
        {
            EventSystem previous = EventSystem.current;
            GameObject focusOwner = new GameObject("M03 focus regression EventSystem",
                typeof(EventSystem));
            EventSystem system = focusOwner.GetComponent<EventSystem>();
            EventSystem.current = system;
            try
            {
                int delivered = 0;
                view = new M03LuJunView(root.transform, M02BridgeEvidence.None,
                    true, _ => delivered++);
                system.SetSelectedGameObject(view.AdvanceButton.gameObject);
                view.AdvanceButton.onClick.Invoke();
                Assert.That(system.currentSelectedGameObject,
                    Is.SameAs(view.GetChoiceButton(0).gameObject));

                // Index 2 means "I need a ship" before the stage changes,
                // but "official ship" afterward when M02 has a valid stamp.
                system.SetSelectedGameObject(view.GetChoiceButton(2).gameObject);
                view.GetChoiceButton(2).onClick.Invoke();

                Assert.That(view.Encounter.Stage,
                    Is.EqualTo(M03LuJunStage.ChoosingVoyage));
                Assert.That(view.GetChoiceButton(2).gameObject.activeSelf, Is.True);
                Assert.That(system.currentSelectedGameObject,
                    Is.SameAs(view.GetChoiceButton(0).gameObject),
                    "A second Submit must require an explicit new voyage selection.");
                Assert.That(view.Encounter.Voyage, Is.Null);
                Assert.That(delivered, Is.Zero);
            }
            finally
            {
                EventSystem.current = previous;
                Object.DestroyImmediate(focusOwner);
            }
        }

        [Test]
        public void DisposalDropsResultAndRespondsSafelyToLocaleSwitch()
        {
            int callbacks = 0;
            view = new M03LuJunView(root.transform, M02BridgeEvidence.None, false, _ => callbacks++);
            view.LayoutForViewportAspect(9f/16f);
            view.LayoutForViewportAspect(32f/9f);
            view.Dispose();
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            view.AdvanceButton.onClick.Invoke();
            Assert.That(callbacks, Is.Zero);
        }
    }
}
