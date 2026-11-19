using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

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
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            SoundwellUiLocalization.SetLanguage(priorLanguage);
        }

        [Test]
        public void AskingForShipNeverPromptsSecrecyAndOffersCivilianAlternative()
        {
            int callbacks = 0;
            M03LuJunEncounter result = null;
            view = new M03LuJunView(root.transform, M02BridgeEvidence.WarReportDraft,
                false, e => { callbacks++; result = e; });
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.VisitingVeterans));
            for (int beat = 1; beat <= 3; beat++)
            {
                view.AdvanceButton.onClick.Invoke();
                Assert.That(view.RollCallBeat, Is.EqualTo(beat));
                Assert.That(view.Dialogue.text, Does.Contain("Qian Mancang"));
                Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.VisitingVeterans),
                    "The player must hear all three unanswered calls before approaching.");
            }
            Assert.That(view.Dialogue.text, Does.Contain("thirty-seven nights"));
            Assert.That(view.Dialogue.text, Does.Contain("\n"),
                "Each roll call should be displayed as separate readable lines.");
            Assert.That(view.Dialogue.text, Does.Not.Contain("\\n"),
                "Do not print literal backslash-n characters to the player.");
            view.AdvanceButton.onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingOpening));
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
            for (int beat = 0; beat < 4; beat++)
                view.AdvanceButton.onClick.Invoke();
            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(view.Dialogue.text, Does.Contain("总帅令"));
            Assert.That(view.Encounter.Confession, Is.EqualTo(M03LuConfession.AskedWithEvidence));
            view.GetChoiceButton(1).onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ReplyingToSecrecyRequest));
            Assert.That(view.Dialogue.text, Does.Contain("还在替他们决定"));
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Encounter.Secrecy, Is.EqualTo(M03SecrecyAnswer.RefuseSecrecy));
            Assert.That(view.Dialogue.text, Does.Contain("每一个名字后面"));
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(view.Encounter.VoyagePathValue, Is.EqualTo("韩峤船坞"));
        }

        [Test]
        public void InterruptedVoyageHostWriteKeepsRouteChoicesAndBlocksReentrantSubmit()
        {
            int attempts = 0;
            int persisted = 0;
            view = new M03LuJunView(root.transform, M02BridgeEvidence.FerrymanLog,
                false, completed =>
                {
                    attempts++;
                    Assert.That(completed.Stage, Is.EqualTo(M03LuJunStage.Complete));
                    Assert.That(completed.Voyage, Is.EqualTo(M03VoyageChoice.QiQueMerchant));
                    // A callback may induce another UGUI submit while writing.
                    view.GetChoiceButton(1).onClick.Invoke();
                    if (attempts == 1)
                        throw new InvalidOperationException("Interrupted Makinom route save");
                    persisted++;
                });

            for (int beat = 0; beat < 4; beat++)
                view.AdvanceButton.onClick.Invoke();
            view.GetChoiceButton(0).onClick.Invoke();
            view.GetChoiceButton(2).onClick.Invoke();
            // RefuseSecrecy is the third reply (index 2), not Undecided.
            view.GetChoiceButton(2).onClick.Invoke();
            view.GetChoiceButton(3).onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));
            Assert.That(view.Encounter.Secrecy, Is.EqualTo(M03SecrecyAnswer.RefuseSecrecy));

            Assert.Throws<InvalidOperationException>(() =>
                view.GetChoiceButton(1).onClick.Invoke());
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(persisted, Is.Zero);
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));
            Assert.That(view.Encounter.Voyage, Is.Null);
            Assert.That(view.GetChoiceButton(1).gameObject.activeSelf, Is.True);
            Assert.That(view.Encounter.Confession, Is.EqualTo(M03LuConfession.AskedWithEvidence));
            Assert.That(view.Encounter.Secrecy, Is.EqualTo(M03SecrecyAnswer.RefuseSecrecy));

            view.GetChoiceButton(1).onClick.Invoke();
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(persisted, Is.EqualTo(1));
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.Complete));
            Assert.That(view.Encounter.Voyage, Is.EqualTo(M03VoyageChoice.QiQueMerchant));
            Assert.That(view.GetChoiceButton(1).gameObject.activeSelf, Is.False);
            view.GetChoiceButton(1).onClick.Invoke();
            Assert.That(attempts, Is.EqualTo(2));
        }

        [Test]
        public void DestroyedCampCanvasCannotReceiveLayoutOrLocaleEventsOrFinishTheVoyage()
        {
            // Either an owner resize or a language change may be the first
            // callback after Unity destroys the M03 scene.
            foreach (bool layoutFirst in new[] { true, false })
            {
                var camp = new GameObject("Destroyed Lu Jun camp");
                try
                {
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                    int accepted = 0;
                    view = new M03LuJunView(camp.transform,
                        M02BridgeEvidence.WarReportDraft, false, _ => accepted++);
                    Assert.That(view.Encounter.Stage,
                        Is.EqualTo(M03LuJunStage.VisitingVeterans));

                    UnityEngine.Object.DestroyImmediate(camp);
                    camp = null;

                    if (layoutFirst)
                        Assert.DoesNotThrow(() => view.LayoutForViewportAspect(9f / 16f));
                    Assert.DoesNotThrow(() =>
                        SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(32f / 9f));
                    Assert.DoesNotThrow(() => view.Refresh());
                    Assert.DoesNotThrow(() => view.Dispose());
                    Assert.DoesNotThrow(() => view.Dispose());
                    Assert.DoesNotThrow(() =>
                        SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English));
                    Assert.That(accepted, Is.Zero);
                    Assert.That(view.RollCallBeat, Is.Zero);
                    Assert.That(view.Encounter.Voyage, Is.Null,
                        "Unloading the camp must never invent a route choice.");
                }
                finally
                {
                    view?.Dispose();
                    if (camp != null)
                        UnityEngine.Object.DestroyImmediate(camp);
                }
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
