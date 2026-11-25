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
        public void OptionalMinerInformantRevealsAlternateLandingWithoutSkippingThreeNames()
        {
            view = new M03LuJunView(root.transform, M02BridgeEvidence.None,
                false, _ => Assert.Fail("The contact must not book a voyage."));
            Assert.That(view.InformantButton.gameObject.activeSelf, Is.True);
            Assert.That(view.InformantVisited, Is.False);
            view.InformantButton.onClick.Invoke();
            Assert.That(view.InformantVisited, Is.True);
            Assert.That(view.InformantButton.gameObject.activeSelf, Is.False);
            Assert.That(view.Dialogue.text, Does.Contain("alternate landing lead"));
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.VisitingVeterans));
            Assert.That(view.RollCallBeat, Is.Zero);
            Assert.That(view.AdvanceButton.GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("Return to the roll call"));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(view.Dialogue.text, Does.Contain("备用登陆情报"));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            view.AdvanceButton.onClick.Invoke();
            Assert.That(view.RollCallBeat, Is.Zero,
                "Returning from the contact cannot silently skip the first missing name.");
            Assert.That(view.Dialogue.text, Does.Contain("thirty names"));
            for (int beat = 1; beat <= 3; beat++)
            {
                view.AdvanceButton.onClick.Invoke();
                Assert.That(view.RollCallBeat, Is.EqualTo(beat));
                Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.VisitingVeterans));
            }
            view.AdvanceButton.onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingOpening));
            Assert.That(view.InformantButton.gameObject.activeSelf, Is.False);
            Assert.That(view.Encounter.Voyage, Is.Null);
        }

        [Test]
        public void MinerInformantLiveCanvasCapturesBilingualReportAndReturnsToRollCall()
        {
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            view = new M03LuJunView(root.transform, M02BridgeEvidence.None,
                false, _ => Assert.Fail("The optional informant cannot book passage."));
            Canvas canvas = root.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.gameObject.name, Is.EqualTo("M03 Lu Jun camp canvas"));
            Assert.That(view.InformantButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.InformantButton.GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("询问矿区线人"));
            Assert.That(view.Dialogue.resizeTextForBestFit, Is.False);\n            Assert.That(view.Dialogue.fontSize, Is.EqualTo(19));\n            Assert.That(view.Dialogue.rectTransform.rect.height, Is.EqualTo(240f).Within(0.1f));\n            Assert.That(view.Dialogue.text.Split('\n').Length, Is.EqualTo(4));\n            // Capture the real 960x540 runtime UGUI canvas, not a synthetic
            // text-only mock. The report and action row must remain legible.
            SoundwellSmokeHost.CaptureStoryCanvas(canvas, "m03-miner-informant-rollcall-zh");

            view.InformantButton.onClick.Invoke();
            Assert.That(view.InformantVisited, Is.True);
            Assert.That(view.RollCallBeat, Is.Zero);
            Assert.That(view.Dialogue.text, Does.Contain("备用登陆情报"));
            Assert.That(view.AdvanceButton.GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("回到点名"));
            SoundwellSmokeHost.CaptureStoryCanvas(canvas, "m03-miner-informant-report-zh");

            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            Assert.That(view.Dialogue.text, Does.Contain("alternate landing lead"));
            Assert.That(view.AdvanceButton.GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("Return to the roll call"));
            SoundwellSmokeHost.CaptureStoryCanvas(canvas, "m03-miner-informant-report-en");

            view.AdvanceButton.onClick.Invoke();
            Assert.That(view.RollCallBeat, Is.Zero);
            Assert.That(view.Encounter.Voyage, Is.Null);
            Assert.That(view.Dialogue.text, Does.Contain("thirty names"));
            SoundwellSmokeHost.CaptureStoryCanvas(canvas, "m03-miner-informant-return-en");
            for (int beat = 0; beat < 4; beat++)
                view.AdvanceButton.onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingOpening));
            Assert.That(view.Dialogue.resizeTextForBestFit, Is.True);
            Assert.That(view.Dialogue.rectTransform.rect.height, Is.EqualTo(151f).Within(0.1f));
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
            Assert.That(view.GetChoiceButton(2).gameObject.activeSelf, Is.True);
            Assert.That(view.GetChoiceButton(3).gameObject.activeSelf, Is.True);
            Assert.That(view.GetChoiceButton(2).interactable, Is.False);
            Assert.That(view.GetChoiceButton(3).interactable, Is.False);
            Assert.That(view.GetChoiceButton(2).GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("requires stamp"));
            Assert.That(view.GetChoiceButton(4).interactable, Is.True);
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));
            view.GetChoiceButton(4).onClick.Invoke();
            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.VoyagePathValue, Is.EqualTo("混合"));
        }

        [Test]
        public void StampedOfficialRoutesAreEnabledButUnstampedRoutesStayVisibleAndLocked()
        {
            view = new M03LuJunView(root.transform, M02BridgeEvidence.None,
                true, _ => { });
            for (int i = 0; i < 4; i++) view.AdvanceButton.onClick.Invoke();
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));
            Assert.That(view.GetChoiceButton(2).interactable, Is.True);
            Assert.That(view.GetChoiceButton(3).interactable, Is.True);
            Assert.That(view.GetChoiceButton(4).interactable, Is.True);
            view.Dispose();
            view = null;

            var unstamped = new M03LuJunView(root.transform, M02BridgeEvidence.None,
                false, _ => Assert.Fail("A locked route cannot be selected."));
            try
            {
                for (int i = 0; i < 4; i++) unstamped.AdvanceButton.onClick.Invoke();
                unstamped.GetChoiceButton(2).onClick.Invoke();
                Assert.That(unstamped.GetChoiceButton(2).gameObject.activeSelf, Is.True);
                Assert.That(unstamped.GetChoiceButton(3).gameObject.activeSelf, Is.True);
                Assert.That(unstamped.GetChoiceButton(2).interactable, Is.False);
                Assert.That(unstamped.GetChoiceButton(3).interactable, Is.False);
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(unstamped.GetChoiceButton(2).GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Does.Contain("须已盖印"));
                Assert.That(unstamped.GetChoiceButton(3).GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Does.Contain("须已盖印"));
                unstamped.GetChoiceButton(3).onClick.Invoke();
                Assert.That(unstamped.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));
                Assert.That(unstamped.Encounter.Voyage, Is.Null);
                Assert.That(unstamped.GetChoiceButton(4).interactable, Is.True);
            }
            finally
            {
                unstamped.Dispose();
            }
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
        public void SuccessfulVoyageHandoffShowsCompletedCopyWithoutSceneUnload()
        {
            int recorded = 0;
            view = new M03LuJunView(root.transform, M02BridgeEvidence.None,
                false, _ => recorded++);
            for (int beat = 0; beat < 4; beat++)
                view.AdvanceButton.onClick.Invoke();

            // Asking for passage never reveals the unasked bridge confession.
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));
            Assert.That(view.Dialogue.text, Does.Contain("Official passage requires"));

            // The host can persist the accepted route without immediately
            // destroying this UGUI view; finished text must appear at once.
            view.GetChoiceButton(4).onClick.Invoke();
            Assert.That(recorded, Is.EqualTo(1));
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.Complete));
            Assert.That(view.Encounter.Voyage, Is.EqualTo(M03VoyageChoice.MixedWithCivilian));
            Assert.That(view.Dialogue.text, Does.Contain("actual departure preparations"));
            Assert.That(view.Dialogue.text, Does.Not.Contain("Official passage requires"));
            for (int index = 0; index < 5; index++)
                Assert.That(view.GetChoiceButton(index).gameObject.activeSelf, Is.False);
            Assert.That(view.AdvanceButton.gameObject.activeSelf, Is.False);

            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(view.Dialogue.text, Does.Contain("接下来的准备才真正开始"));
            view.GetChoiceButton(4).onClick.Invoke();
            Assert.That(recorded, Is.EqualTo(1), "The completed route cannot be delivered twice.");
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
