using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Rpg.Tests.Editor
{
    // The main S00 input regression follows Shen Li's rope. These two routes
    // must also require physical movement and a fresh gameplay interaction:
    // neither the authored choice nor a timer may automatically rescue Han Qiao.
    public sealed class WhitewaterRescueChoiceInputTests
    {
        [Test]
        public void UnwonOrkEncounterCannotAdvanceS00ViaStaleContinueCallback()
        {
            // The Continue button is displayed only after ORK victory, but a
            // stale or direct callback must still be denied by the owner.
            var root = new GameObject("unwon S00 ORK defence");
            try
            {
                var party = root.AddComponent<OrkPartyBridge>();
                var defence = root.AddComponent<WhitewaterDefenseEncounter>();
                var flags = System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic;
                var type = typeof(WhitewaterDefenseEncounter);
                var partyField = type.GetField("party", flags);
                var activeField = type.GetField("active", flags);
                var callbackField = type.GetField("onCompleted", flags);
                var continueMethod = type.GetMethod("Continue", flags);
                var retreatMethod = type.GetMethod("Retreat", flags);
                Assert.That(partyField, Is.Not.Null);
                Assert.That(activeField, Is.Not.Null);
                Assert.That(callbackField, Is.Not.Null);
                Assert.That(continueMethod, Is.Not.Null);
                Assert.That(retreatMethod, Is.Not.Null);
                partyField.SetValue(defence, party);
                activeField.SetValue(defence, true);
                int result = -1;
                callbackField.SetValue(defence,
                    new System.Action<bool>(won => result = won ? 1 : 0));

                Assert.That(party.EncounterWon, Is.False);
                continueMethod.Invoke(defence, null);
                Assert.That(result, Is.EqualTo(-1),
                    "Without an actual ORK win, Continue cannot grant the third bell.");
                Assert.That(defence.IsActive, Is.True,
                    "Rejecting a stale Continue must not silently end the encounter.");

                retreatMethod.Invoke(defence, null);
                Assert.That(result, Is.Zero,
                    "The existing voluntary retreat must remain available.");
                Assert.That(defence.IsActive, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SharedS00PixelRasterizerRetainsAuthoredCoatAndTransparentCorners()
        {
            var rasterizer = typeof(WhitewaterPrelude).Assembly.GetType(
                "Rpg.Soundwell.SoundwellPixelSpriteBuilder");
            Assert.That(rasterizer, Is.Not.Null);
            var create = rasterizer.GetMethod("Create16Colored",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(create, Is.Not.Null);
            var rows = new string[16];
            for (int i = 0; i < rows.Length; i++)
                rows[i] = "................";
            rows[0] = "ck..............";
            var coat = new Color32(73, 104, 186, 255);
            var args = new object[] { rows, "shared S00 test coat", coat, null };
            var sprite = (Sprite)create.Invoke(null, args);
            var texture = (Texture2D)args[3];
            try
            {
                Assert.That(texture, Is.Not.Null);
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16f));
                Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(texture.GetPixel(0, 15), Is.EqualTo((Color)coat));
                Assert.That(texture.GetPixel(1, 15),
                    Is.EqualTo((Color)new Color32(24, 32, 41, 255)));
                Assert.That(texture.GetPixel(3, 15).a, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void EvacueePickupRequiresOverlappingPixelFootprints()
        {
            // A clamp tool can reach 44px, but two 32px-wide actors cannot
            // start a physical escort while there is still a visible gap.
            var root = new GameObject("S00 evacuee contact check");
            var civilianObject = new GameObject("S00 wounded civilian", typeof(RectTransform));
            try
            {
                civilianObject.transform.SetParent(root.transform, false);
                var prelude = root.AddComponent<WhitewaterPrelude>();
                var civilian = civilianObject.GetComponent<RectTransform>();
                civilian.anchoredPosition = new Vector2(-72f, 12f);
                var flags = System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic;
                var type = typeof(WhitewaterPrelude);
                var phaseField = type.GetField("phase", flags);
                var walkerField = type.GetField("walker", flags);
                var evacueeField = type.GetField("evacuee", flags);
                Assert.That(phaseField, Is.Not.Null);
                Assert.That(walkerField, Is.Not.Null);
                Assert.That(evacueeField, Is.Not.Null);
                evacueeField.SetValue(prelude, civilian);
                phaseField.SetValue(prelude, 9);

                walkerField.SetValue(prelude, new Vector2(-72f + 44f, 0f));
                Assert.That(prelude.CanHelpEvacuee, Is.False,
                    "The wider cable-repair radius cannot pick up a distant civilian.");
                walkerField.SetValue(prelude, new Vector2(-72f + 32f, 0f));
                Assert.That(prelude.CanHelpEvacuee, Is.False,
                    "Two 32px bodies that only touch at their edges are not overlapping.");
                walkerField.SetValue(prelude, new Vector2(-72f + 31f, 0f));
                Assert.That(prelude.CanHelpEvacuee, Is.True,
                    "One pixel of body overlap on the bridge lane allows the escort.");
                walkerField.SetValue(prelude, new Vector2(-72f + 31f, 29f));
                Assert.That(prelude.CanHelpEvacuee, Is.False,
                    "Horizontal contact from below the walkable lane is insufficient.");
                phaseField.SetValue(prelude, 8);
                walkerField.SetValue(prelude, new Vector2(-72f, 0f));
                Assert.That(prelude.CanHelpEvacuee, Is.False,
                    "Boarding Han Qiao cannot prematurely start the next rescue.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TruncatedS00PixelActorDoesNotLeaveUnmanagedTexture()
        {
            var generator = typeof(WhitewaterPrelude).GetMethod("MakeActorSprite",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);
            Assert.That(generator, Is.Not.Null);
            string[] art = Enumerable.Repeat("................", 16).ToArray();
            art[4] = "truncated";
            var existingTextures = Resources.FindObjectsOfTypeAll<Texture2D>();
            object[] parameters = { "invalid wounded silhouette", art,
                new Color32(128, 80, 80, 255), null };
            try
            {
                var error = Assert.Throws<System.Reflection.TargetInvocationException>(
                    () => generator.Invoke(null, parameters));
                Assert.That(error.InnerException,
                    Is.TypeOf<System.InvalidOperationException>());
                Assert.That(parameters[3], Is.Null,
                    "A rejected sprite must never hand out a native texture.");
                Assert.That(Resources.FindObjectsOfTypeAll<Texture2D>()
                    .Where(texture => !existingTextures.Contains(texture)), Is.Empty,
                    "Reject malformed S00 art before allocating an orphan texture.");
            }
            finally
            {
                // Clean up even if a regression creates the very leak that
                // this test must catch, so later Unity tests stay independent.
                foreach (var orphan in Resources.FindObjectsOfTypeAll<Texture2D>()
                    .Where(texture => !existingTextures.Contains(texture)))
                    UnityEngine.Object.DestroyImmediate(orphan);
            }
        }

        [UnityTest]
        public IEnumerator ThirdBellRequiresAnEarnedDefenseCompletionAndEndsAtTwelveYearsLater()
        {
            // Isolate the visual callback while the separate S00 full-path
            // test proves the real ORK battle starts and accepts actions.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);

            var input = new InputTestFixture();
            input.Setup();
            WhitewaterPrelude stage = null;
            string originalLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                InputSystem.AddDevice<Keyboard>();
                InputSystem.AddDevice<Gamepad>();
                stage = new GameObject("S00 third bell regression")
                    .AddComponent<WhitewaterPrelude>();
                stage.Begin(() => SoundwellInputAdapter.SetEntryContext());
                yield return null;

                var flags = System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic;
                var type = typeof(WhitewaterPrelude);
                var completion = type.GetMethod("OnWhitewaterDefenseCompleted", flags);
                var phaseField = type.GetField("phase", flags);
                var defenseStarted = type.GetField("defenseStarted", flags);
                Assert.That(completion, Is.Not.Null);
                Assert.That(phaseField, Is.Not.Null);
                Assert.That(defenseStarted, Is.Not.Null);
                var bell = stage.GetComponentInChildren<AudioSource>(true);
                Assert.That(bell, Is.Not.Null);

                completion.Invoke(stage, new object[] { true });
                Assert.That(stage.Phase, Is.Zero,
                    "A callback cannot skip roll call, rescue, and the ORK encounter.");
                Assert.That(stage.ThirdBellStarted, Is.False);
                phaseField.SetValue(stage, 16);
                completion.Invoke(stage, new object[] { true });
                Assert.That(stage.ThirdBellStarted, Is.False,
                    "Holding Shen Li's cable without the live ORK encounter cannot ring the third bell.");
                Assert.That(bell.clip, Is.Null);

                defenseStarted.SetValue(stage, true);
                completion.Invoke(stage, new object[] { true });
                Assert.That(stage.Phase, Is.EqualTo(17));
                Assert.That(stage.ThirdBellStarted, Is.True);
                Assert.That(bell.clip, Is.Not.Null);
                Assert.That(bell.clip.name, Does.Contain("third warning bell"));
                Assert.That(bell.outputAudioMixerGroup, Is.Not.Null,
                    "The scripted third bell must use the existing saved M6 SFX bus.");
                Assert.That(bell.outputAudioMixerGroup.name, Is.EqualTo("SFX"));
                Assert.That(bell.clip.length, Is.InRange(1.25f, 1.35f));

                var walkway = stage.transform.Find(
                    "S00 Whitewater playable canvas/PLACEHOLDER bridge walkway") as RectTransform;
                Assert.That(walkway, Is.Not.Null);
                float deadline = Time.realtimeSinceStartup + 8f;
                while (stage.Phase != 18 && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(stage.Phase, Is.EqualTo(18),
                    "Only the earned win should reach the final blackout and historical title.");
                Assert.That(walkway.sizeDelta.x, Is.LessThan(400f),
                    "The far span must visibly fall, not merely change a caption.");
                Assert.That(walkway.anchoredPosition.y, Is.LessThan(-20f));
                var blackout = stage.transform.Find(
                    "S00 Whitewater playable canvas/S00 blackout after bridge collapse");
                Assert.That(blackout, Is.Not.Null);
                Assert.That(blackout.GetComponent<Image>().color.a,
                    Is.GreaterThan(0.99f));
                var card = stage.transform.Find(
                    "S00 Whitewater playable canvas/S00 twelve years later");
                Assert.That(card, Is.Not.Null);
                Assert.That(card.GetComponent<Text>().text,
                    Is.EqualTo("TWELVE YEARS LATER"));
                var exit = FindButton(stage, "S00 return to Bootstrap");
                Assert.That(exit.gameObject.activeInHierarchy, Is.True,
                    "The final title cannot trap players in an unimplemented M00.");

                completion.Invoke(stage, new object[] { true });
                yield return null;
                Assert.That(stage.Phase, Is.EqualTo(18),
                    "An old duplicate victory callback must not restart the third bell.");
            }
            finally
            {
                if (stage != null)
                    stage.Close();
                SoundwellUiLocalization.SetLanguage(originalLanguage);
                SoundwellInputAdapter.SetUiContext();
                input.TearDown();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator OpenS00ChoicesFollowLocaleWithoutAdvancingOrChangingSelection()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);

            var input = new InputTestFixture();
            input.Setup();
            WhitewaterPrelude stage = null;
            string originalLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                InputSystem.AddDevice<Keyboard>();
                InputSystem.AddDevice<Gamepad>();
                stage = new GameObject("S00 choice translation regression")
                    .AddComponent<WhitewaterPrelude>();
                stage.Begin(() => SoundwellInputAdapter.SetEntryContext());
                yield return null;

                Button first = FindButton(stage, "S00 choice 0");
                Button second = FindButton(stage, "S00 choice 1");
                Assert.That(first.GetComponentInChildren<Text>(true).text, Is.EqualTo("Here."));
                Assert.That(second.GetComponentInChildren<Text>(true).text,
                    Is.EqualTo("Raise your hand."));
                Assert.That(stage.Phase, Is.Zero);

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                yield return null;
                Assert.That(first.GetComponentInChildren<Text>(true).text, Is.EqualTo("在。"));
                Assert.That(second.GetComponentInChildren<Text>(true).text,
                    Is.EqualTo("举手示意"));
                Assert.That(stage.Phase, Is.Zero,
                    "Changing language cannot answer the roll call for the player.");

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                yield return null;
                Assert.That(first.GetComponentInChildren<Text>(true).text, Is.EqualTo("Here."));
                Assert.That(stage.Phase, Is.Zero);
                second.onClick.Invoke();
                Assert.That(stage.Phase, Is.EqualTo(1),
                    "The original choice remains operable after both locale changes.");
            }
            finally
            {
                if (stage != null)
                    stage.Close();
                SoundwellUiLocalization.SetLanguage(originalLanguage);
                SoundwellInputAdapter.SetUiContext();
                input.TearDown();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PlayerAndSoldierRescuesBothRequireAnEarnedStationInteraction()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);

            var input = new InputTestFixture();
            input.Setup();
            WhitewaterPrelude activePrelude = null;
            string originalLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Keyboard keys = InputSystem.AddDevice<Keyboard>();
                Gamepad pad = InputSystem.AddDevice<Gamepad>();
                EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/Game/Scenes/Bootstrap.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));
                float entranceDeadline = Time.realtimeSinceStartup + 5f;
                var entrance = UnityEngine.Object.FindFirstObjectByType<SoundwellEntrance>();
                while (entrance == null && Time.realtimeSinceStartup < entranceDeadline)
                {
                    yield return null;
                    entrance = UnityEngine.Object.FindFirstObjectByType<SoundwellEntrance>();
                }
                Assert.That(entrance, Is.Not.Null,
                    "The Bootstrap scene must expose the S00 entry after the input fixture is ready.");
                var defense = entrance.GetComponent<WhitewaterDefenseEncounter>();
                Assert.That(defense, Is.Not.Null,
                    "The Bootstrap entrance must own the S00 ORK defence.");
                Assert.That(SoundwellInputAdapter.IsEntryContextActive, Is.True,
                    "The Bootstrap entry must initialise the shared input adapter after fixture setup.");
                foreach (int rescueChoice in new[] { 0, 1, 2 })
                {
                    entrance.OpenPrelude();
                    yield return null;
                    activePrelude = entrance.GetComponentInChildren<WhitewaterPrelude>(true);
                    Assert.That(activePrelude, Is.Not.Null,
                        "Opening S00 must create its playable stage.");
                    var root = activePrelude.gameObject;

                    ClickChoice(activePrelude, 0); // Roll call: the player answers.
                    Assert.That(activePrelude.Phase, Is.EqualTo(1));
                    Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True,
                        "The roll call must return movement control to the gameplay action map.");
                    input.Press(keys.dKey);
                    float repairDeadline = Time.realtimeSinceStartup + 6f;
                    while (!activePrelude.CanRepair && Time.realtimeSinceStartup < repairDeadline)
                        yield return null;
                    input.Release(keys.dKey);
                    yield return null;
                    Assert.That(activePrelude.CanRepair, Is.True,
                        $"The player must physically reach the clamp before repairing (walker={activePrelude.WalkerPosition}, gameplay={SoundwellInputAdapter.IsGameplayContextActive}).");
                    input.Press(keys.eKey);
                    yield return null;
                    input.Release(keys.eKey);
                    yield return null;
                    Assert.That(activePrelude.Phase, Is.EqualTo(2));
                    ClickChoice(activePrelude, 0); // Brace the plate together.
                    Assert.That(activePrelude.Phase, Is.EqualTo(3));

                    Button workshop = FindButton(activePrelude, "S00 continue to workshop");
                    float cartDeadline = Time.realtimeSinceStartup + 4f;
                    while (!workshop.gameObject.activeInHierarchy &&
                           Time.realtimeSinceStartup < cartDeadline)
                        yield return null;
                    Assert.That(workshop.gameObject.activeInHierarchy, Is.True);
                    workshop.onClick.Invoke();
                    Assert.That(activePrelude.Phase, Is.EqualTo(4));
                    ClickChoice(activePrelude, 1); // A genuine, noncommittal workshop answer.
                    Assert.That(activePrelude.Phase, Is.EqualTo(5));
                    FindButton(activePrelude, "S00 continue to last boat").onClick.Invoke();
                    Assert.That(activePrelude.Phase, Is.EqualTo(6));

                    var inWater = activePrelude.transform.Find(
                        "S00 Whitewater playable canvas/S00 Han Qiao in water") as RectTransform;
                    Assert.That(inWater, Is.Not.Null);
                    float riverStartX = inWater.anchoredPosition.x;
                    var station = activePrelude.transform.Find(
                        "S00 Whitewater playable canvas/S00 rescue station") as RectTransform;
                    Assert.That(station, Is.Not.Null);
                    Assert.That(station.gameObject.activeInHierarchy, Is.False);
                    var soldierHelper = activePrelude.transform.Find(
                        "S00 Whitewater playable canvas/S00 soldier rescue helper") as RectTransform;
                    Assert.That(soldierHelper, Is.Not.Null);
                    Assert.That(soldierHelper.gameObject.activeInHierarchy, Is.False,
                        "The soldier should not appear before a rescue plan is chosen.");
                    var soldierGraphic = soldierHelper.GetComponent<Image>();
                    Assert.That(soldierGraphic, Is.Not.Null);
                    Assert.That(soldierGraphic.raycastTarget, Is.False,
                        "The soldier cannot intercept the player's real rescue input.");
                    Assert.That(soldierGraphic.sprite.texture.width, Is.EqualTo(16));
                    Assert.That(soldierGraphic.sprite.texture.height, Is.EqualTo(16));
                    Assert.That(soldierGraphic.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                    Assert.That(soldierGraphic.sprite.name, Does.Contain("ready at the rail"));
                    var shenLiLine = activePrelude.transform.Find(
                        "S00 Whitewater playable canvas/S00 Shen Li thrown rescue line") as RectTransform;
                    Assert.That(shenLiLine, Is.Not.Null);
                    Assert.That(shenLiLine.gameObject.activeInHierarchy, Is.False,
                        "Shen Li cannot throw the rope before the choice is made.");
                    var ropeGraphic = shenLiLine.GetComponent<RawImage>();
                    Assert.That(ropeGraphic, Is.Not.Null,
                        "The thrown rope must be rendered by a tiled pixel texture.");
                    Assert.That(ropeGraphic.raycastTarget, Is.False,
                        "The rescue line must not steal clicks or gamepad focus.");
                    var ropeTexture = ropeGraphic.texture as Texture2D;
                    Assert.That(ropeTexture, Is.Not.Null);
                    Assert.That(ropeTexture.width, Is.EqualTo(16));
                    Assert.That(ropeTexture.height, Is.EqualTo(4));
                    Assert.That(ropeTexture.filterMode, Is.EqualTo(FilterMode.Point),
                        "The crossing line cannot use blurred stretch filtering.");
                    Assert.That(ropeTexture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
                    Assert.That(ropeGraphic.uvRect.width, Is.EqualTo(315f / 16f).Within(0.01f),
                        "Every rope pixel must repeat at the same size along the span.");
                    Assert.That(ropeTexture.GetPixel(0, 1),
                        Is.Not.EqualTo(ropeTexture.GetPixel(2, 1)),
                        "The rope must contain visible braided strands, not solid paint.");

                    // The first choice is merely a plan. Back must return
                    // to the rescuer menu, not exit S00 or silently save a rescue.
                    int abandonedChoice = rescueChoice == 0 ? 2 : rescueChoice == 1 ? 1 : 0;
                    ClickChoice(activePrelude, abandonedChoice);
                    Assert.That(activePrelude.Phase, Is.EqualTo(7));
                    Assert.That(station.gameObject.activeInHierarchy, Is.True);
                    Assert.That(station.anchoredPosition.x,
                        Is.EqualTo(abandonedChoice == 0 ? 60f :
                            abandonedChoice == 1 ? 178f : -70f));
                    Assert.That(shenLiLine.gameObject.activeInHierarchy,
                        Is.EqualTo(abandonedChoice == 1),
                        "Only Shen Li's rope choice should draw the line over the water.");
                    Assert.That(soldierHelper.gameObject.activeInHierarchy,
                        Is.EqualTo(abandonedChoice == 2),
                        "A bridge soldier should arrive only for the soldier-assisted plan.");
                    if (rescueChoice == 0)
                        input.Press(keys.escapeKey);
                    else
                        input.Press(pad.buttonEast);
                    yield return null;
                    if (rescueChoice == 0)
                        input.Release(keys.escapeKey);
                    else
                        input.Release(pad.buttonEast);
                    yield return null;
                    Assert.That(activePrelude, Is.Not.Null,
                        "Cancelling a rescue plan must not destroy the whole S00 stage.");
                    Assert.That(activePrelude.Phase, Is.EqualTo(6));
                    Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                    Assert.That(station.gameObject.activeInHierarchy, Is.False,
                        "The abandoned rescue station must disappear.");
                    Assert.That(shenLiLine.gameObject.activeInHierarchy, Is.False,
                        "Pad B must retract a cancelled Shen Li rope without rescuing anyone.");
                    Assert.That(soldierHelper.gameObject.activeInHierarchy, Is.False,
                        "Cancelling a plan must also dismiss an uncommitted soldier helper.");
                    Assert.That(activePrelude.CanRescue, Is.False);
                    Assert.That(inWater.anchoredPosition.x, Is.EqualTo(riverStartX),
                        "Cancelling a plan cannot begin moving Han Qiao to the boat.");
                    Assert.That(FindButton(activePrelude, "S00 choice 0").gameObject.activeInHierarchy,
                        Is.True, "Rescuer choices must be available again.");
                    Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject?.name,
                        Is.EqualTo("S00 choice 0"), "The returned UGUI menu needs keyboard/gamepad focus.");

                    // Choosing a different rescuer is still an uncommitted
                    // plan until the player reaches that station and presses E.
                    ClickChoice(activePrelude, rescueChoice);
                    Assert.That(activePrelude.Phase, Is.EqualTo(7));
                    Assert.That(inWater.anchoredPosition.x, Is.EqualTo(riverStartX),
                        "Choosing a rescuer cannot instantly board Han Qiao.");
                    Assert.That(activePrelude.CanRescue, Is.EqualTo(rescueChoice == 1),
                        "The Shen Li station is at the previously reached clamp; the other rescues require travel.");
                    Assert.That(station.anchoredPosition.x,
                        Is.EqualTo(rescueChoice == 0 ? 60f : rescueChoice == 1 ? 178f : -70f),
                        "Each authored rescue method must point to its own visible station.");
                    Assert.That(shenLiLine.gameObject.activeInHierarchy,
                        Is.EqualTo(rescueChoice == 1),
                        "The thrown rope must appear only for the selected Shen Li method.");
                    Assert.That(soldierHelper.gameObject.activeInHierarchy,
                        Is.EqualTo(rescueChoice == 2),
                        "Only the soldier-assisted rescue gets an arriving pixel helper.");
                    if (rescueChoice == 2)
                    {
                        Assert.That(soldierHelper.anchoredPosition.x, Is.EqualTo(-108f));
                        Assert.That(soldierGraphic.sprite.name, Does.Contain("ready at the rail"),
                            "The soldier cannot animate a successful haul before the player's E / Pad A.");
                    }
                    if (rescueChoice == 1)
                    {
                        // Shen Li's station starts horizontally within reach.
                        // Stepping off the narrow bridge lane cannot rescue
                        // Han Qiao from below the decking, even after the
                        // player explicitly presses the real interact key.
                        input.Press(keys.sKey);
                        float offLaneDeadline = Time.realtimeSinceStartup + 2f;
                        while (activePrelude.CanRescue && Time.realtimeSinceStartup < offLaneDeadline)
                            yield return null;
                        input.Release(keys.sKey);
                        yield return null;
                        Assert.That(activePrelude.CanRescue, Is.False,
                            "The player must be on the bridge lane to reach the visible rescue station.");
                        input.Press(keys.eKey);
                        yield return null;
                        input.Release(keys.eKey);
                        yield return null;
                        Assert.That(activePrelude.Phase, Is.EqualTo(7),
                            "An off-lane E press must not board Han Qiao.");
                        Assert.That(activePrelude.CurrentInstructions,
                            Does.Contain("Not at the gold rescue station"),
                            "The player needs a usable return-to-station hint.");
                        input.Press(keys.wKey);
                        offLaneDeadline = Time.realtimeSinceStartup + 2f;
                        while (!activePrelude.CanRescue && Time.realtimeSinceStartup < offLaneDeadline)
                            yield return null;
                        input.Release(keys.wKey);
                        yield return null;
                        Assert.That(activePrelude.CanRescue, Is.True,
                            "Returning to Shen Li's bridge lane must restore the rescue interaction.");
                        Assert.That(activePrelude.Phase, Is.EqualTo(7),
                            "Walking back never earns a rescue without a fresh E.");
                    }
                    if (rescueChoice == 1)
                    {
                        Assert.That(shenLiLine.sizeDelta.x, Is.GreaterThan(300f),
                            "The rope should actually span the water to Han Qiao.");
                        Assert.That(activePrelude.Phase, Is.EqualTo(7),
                            "Already standing at Shen Li's clamp cannot auto-complete the rescue.");
                    }

                    // The soldier-assisted branch must be playable with the
                    // physical gamepad alone after the Pad B plan cancellation.
                    // A premature Pad A press away from the station cannot
                    // convert a mere dialogue choice into an earned rescue.
                    if (rescueChoice == 2)
                    {
                        input.Press(pad.buttonSouth);
                        yield return null;
                        input.Release(pad.buttonSouth);
                        yield return null;
                        Assert.That(activePrelude.Phase, Is.EqualTo(7),
                            "Pad A outside the soldier's station must not board Han Qiao.");
                        Assert.That(inWater.anchoredPosition.x, Is.EqualTo(riverStartX));
                        Assert.That(activePrelude.CurrentInstructions,
                            Does.Contain("Not at the gold rescue station"),
                            "An out-of-range Pad A press must explain the actual station, not fail silently.");
                        SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                        yield return null;
                        Assert.That(activePrelude.CurrentInstructions, Does.Contain("尚未到金色救援点"),
                            "The saved Chinese S00 locale needs the same recovery cue.");
                        Assert.That(activePrelude.CurrentInstructions, Does.Contain("手柄 A"));
                        SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                        yield return null;
                        yield return AssertStationCueTracksLiveRebind(activePrelude);
                        input.Set(pad.leftStick, Vector2.left);
                    }
                    else if (rescueChoice == 0)
                        input.Press(keys.aKey);
                    float rescueDeadline = Time.realtimeSinceStartup + 5f;
                    while (!activePrelude.CanRescue &&
                           Time.realtimeSinceStartup < rescueDeadline)
                        yield return null;
                    if (rescueChoice == 2)
                        input.Set(pad.leftStick, Vector2.zero);
                    else if (rescueChoice == 0)
                        input.Release(keys.aKey);
                    yield return null;
                    Assert.That(activePrelude.CanRescue, Is.True,
                        "The player must physically arrive at the selected rescue station.");
                    Assert.That(activePrelude.CurrentInstructions,
                        Does.Contain("At the gold rescue station"),
                        "Crossing into true station reach must show the fresh, available haul control.");
                    Assert.That(activePrelude.CurrentInstructions, Does.Contain("press"),
                        "The action must still need an explicit fresh E / Pad A.");
                    Assert.That(activePrelude.Phase, Is.EqualTo(7),
                        "Arriving alone must not grant rescue before the action.");
                    if (rescueChoice == 2)
                        input.Press(pad.buttonSouth);
                    else
                        input.Press(keys.eKey);
                    yield return null;
                    if (rescueChoice == 2)
                        input.Release(pad.buttonSouth);
                    else
                        input.Release(keys.eKey);
                    yield return null;
                    Assert.That(activePrelude.Phase, Is.EqualTo(8),
                        "Fresh gamepad South or E at the station must perform the actual rescue.");
                    Assert.That(shenLiLine.gameObject.activeInHierarchy, Is.False,
                        "The rope retracts after the real action; it cannot stay over the boat.");
                    Assert.That(soldierHelper.gameObject.activeInHierarchy,
                        Is.EqualTo(rescueChoice == 2));
                    if (rescueChoice == 2)
                        Assert.That(soldierGraphic.sprite.name, Does.Contain("hauling the line"),
                            "The helper's raised-arm sprite is earned only when the real rescue succeeds.");
                    Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                    Assert.That(activePrelude.CurrentNarration, Does.Contain(
                        rescueChoice == 0 ? "You heave Han Qiao" :
                        rescueChoice == 1 ? "Shen Li's rope holds" : "The soldiers lift"));
                    Assert.That(inWater.anchoredPosition.x, Is.GreaterThan(riverStartX),
                        "The officer must begin visibly moving only after the actual rescue.");
                    Assert.That(inWater.anchoredPosition.x,
                        Is.EqualTo(Mathf.Round(inWater.anchoredPosition.x)).Within(0.001f),
                        "The first earned rescue frame must move on whole canvas pixels.");

                    if (rescueChoice == 0)
                    {
                        // Leaving the bridge's walkable lane, not simply
                        // following the ordinary east/west route, must stop
                        // the escort and make recovery visible to the player.
                        // The reusable Continue button is renamed only when
                        // the real boat-arrival coroutine completes.
                        Button retreat = activePrelude.GetComponentsInChildren<Button>(true)
                            .Single(button => button.gameObject.name == "S00 continue to last boat" ||
                                button.gameObject.name == "S00 continue to final retreat");
                        float boatDeadline = Time.realtimeSinceStartup + 5f;
                        while (!retreat.gameObject.activeInHierarchy && Time.realtimeSinceStartup < boatDeadline)
                            yield return null;
                        Assert.That(retreat.gameObject.activeInHierarchy, Is.True);
                        Assert.That(retreat.gameObject.name, Is.EqualTo("S00 continue to final retreat"),
                            "Only the completed boat animation may unlock the final retreat.");
                        var departingBoat = activePrelude.transform.Find(
                            "S00 Whitewater playable canvas/S00 last boat") as RectTransform;
                        Assert.That(departingBoat, Is.Not.Null);
                        var boatImage = departingBoat.GetComponent<RawImage>();
                        Assert.That(boatImage, Is.Not.Null,
                            "The last ferry must have authored pixels, not a stretched flat rectangle.");
                        Assert.That(boatImage.raycastTarget, Is.False,
                            "The boat must not intercept rescue or menu input.");
                        var boatTexture = boatImage.texture as Texture2D;
                        Assert.That(boatTexture, Is.Not.Null);
                        Assert.That(boatTexture.width, Is.EqualTo(48));
                        Assert.That(boatTexture.height, Is.EqualTo(12));
                        Assert.That(boatTexture.filterMode, Is.EqualTo(FilterMode.Point));
                        Assert.That(boatTexture.mipmapCount, Is.EqualTo(1));
                        Assert.That(departingBoat.sizeDelta, Is.EqualTo(new Vector2(96f, 24f)),
                            "The boat must render every source pixel at an exact 2x scale.");
                        Assert.That(boatTexture.GetPixel(0, 0).a, Is.EqualTo(0f),
                            "The rectangular texture must have transparent hull cutouts.");
                        Assert.That(boatTexture.GetPixel(24, 5).a, Is.GreaterThan(0.9f));
                        Assert.That(boatTexture.GetPixel(24, 5),
                            Is.Not.EqualTo(boatTexture.GetPixel(24, 9)),
                            "The deck and wooden hull need distinct visible pixel details.");
                        Assert.That(departingBoat.gameObject.activeInHierarchy, Is.True);
                        float boatDockX = departingBoat.anchoredPosition.x;
                        float passengerDockX = inWater.anchoredPosition.x;
                        retreat.onClick.Invoke();
                        Assert.That(activePrelude.Phase, Is.EqualTo(9));
                        Assert.That(soldierHelper.gameObject.activeInHierarchy, Is.False,
                            "The soldier must leave the rescue lane as the last boat departs.");
                        float departureDeadline = Time.realtimeSinceStartup + 3f;
                        while (departingBoat.gameObject.activeInHierarchy &&
                               Time.realtimeSinceStartup < departureDeadline)
                        {
                            yield return null;
                            Assert.That(departingBoat.anchoredPosition.x,
                                Is.EqualTo(Mathf.Round(departingBoat.anchoredPosition.x))
                                    .Within(0.001f),
                                "The 2x point-filtered ferry must not shimmer between canvas pixels.");
                            Assert.That(inWater.anchoredPosition.x - passengerDockX,
                                Is.EqualTo(departingBoat.anchoredPosition.x - boatDockX)
                                    .Within(0.01f),
                                "Pixel snapping cannot separate the passenger from the ferry.");
                        }
                        Assert.That(departingBoat.gameObject.activeInHierarchy, Is.False,
                            "The last boat must actually leave after Han Qiao is aboard.");
                        Assert.That(inWater.gameObject.activeInHierarchy, Is.False,
                            "Han Qiao must depart with the boat rather than remaining in the river.");
                        float boatTravel = departingBoat.anchoredPosition.x - boatDockX;
                        Assert.That(boatTravel, Is.GreaterThan(300f),
                            "The boat has to move off the visible ferry route.");
                        Assert.That(inWater.anchoredPosition.x - passengerDockX,
                            Is.EqualTo(boatTravel).Within(0.1f),
                            "The rescued passenger cannot drift away from the moving boat.");

                        // The first two rescue stations leave the player far
                        // from the last civilian. A real premature E must
                        // explain proximity instead of silently doing nothing
                        // or skipping the earned approach.
                        if (!activePrelude.CanHelpEvacuee)
                        {
                            input.Press(keys.eKey);
                            yield return null;
                            input.Release(keys.eKey);
                            yield return null;
                            Assert.That(activePrelude.Phase, Is.EqualTo(9),
                                "An out-of-range interaction cannot start an escort.");
                            Assert.That(activePrelude.CurrentInstructions,
                                Does.Contain("Too far from the amber evacuee"),
                                "The actual keyboard action must explain the failed reach check.");
                        }
                        input.Press(keys.aKey);
                        float reachDeadline = Time.realtimeSinceStartup + 4f;
                        while (!activePrelude.CanHelpEvacuee && Time.realtimeSinceStartup < reachDeadline)
                            yield return null;
                        input.Release(keys.aKey);
                        yield return null;
                        Assert.That(activePrelude.CanHelpEvacuee, Is.True);
                        // A player horizontally beside the civilian but off the
                        // bridge's narrow walkable lane cannot begin the escort.
                        // The same real E action must explain how to get back.
                        input.Press(keys.sKey);
                        float laneDeadline = Time.realtimeSinceStartup + 2f;
                        while (activePrelude.CanHelpEvacuee &&
                               Time.realtimeSinceStartup < laneDeadline)
                            yield return null;
                        input.Release(keys.sKey);
                        yield return null;
                        Assert.That(activePrelude.CanHelpEvacuee, Is.False,
                            "Vertical separation cannot count as touching the wounded civilian.");
                        input.Press(keys.eKey);
                        yield return null;
                        input.Release(keys.eKey);
                        yield return null;
                        Assert.That(activePrelude.Phase, Is.EqualTo(9),
                            "An off-lane E press cannot start an unearned rescue.");
                        Assert.That(activePrelude.CurrentInstructions, Does.Contain("bridge lane"),
                            "The physical contact failure should give a usable recovery hint.");
                        input.Press(keys.wKey);
                        laneDeadline = Time.realtimeSinceStartup + 2f;
                        while (!activePrelude.CanHelpEvacuee &&
                               Time.realtimeSinceStartup < laneDeadline)
                            yield return null;
                        input.Release(keys.wKey);
                        yield return null;
                        Assert.That(activePrelude.CanHelpEvacuee, Is.True,
                            "Returning to the bridge beside the same civilian restores contact.");
                        input.Press(keys.eKey);
                        yield return null;
                        input.Release(keys.eKey);
                        yield return null;
                        Assert.That(activePrelude.Phase, Is.EqualTo(10));
                        var follower = activePrelude.transform.Find(
                            "S00 Whitewater playable canvas/S00 last evacuee").GetComponent<Image>();
                        var escortStatus = follower.transform.Find("S00 evacuee escort status")
                            ?.GetComponent<Text>();
                        Assert.That(escortStatus, Is.Not.Null);
                        Assert.That(escortStatus.gameObject.activeInHierarchy, Is.True);
                        Assert.That(escortStatus.raycastTarget, Is.False,
                            "The on-actor text cannot block rescue input.");
                        Assert.That(escortStatus.text, Is.EqualTo("FOLLOW"),
                            "Escort readiness must not rely on color alone.");
                        Assert.That(follower.color.g, Is.GreaterThan(follower.color.r),
                            "Green indicates a follower is connected to the escort.");

                        // An early confirmation must never skip the walk or
                        // silently ignore the player's actual input.
                        input.Press(keys.eKey);
                        yield return null;
                        input.Release(keys.eKey);
                        yield return null;
                        Assert.That(activePrelude.Phase, Is.EqualTo(10),
                            "A premature handoff must not complete the evacuation.");
                        Assert.That(activePrelude.CurrentInstructions,
                            Does.Contain("Not at the green safe line"),
                            "The player needs a clear recovery action after early E.");

                        input.Press(keys.sKey);
                        float separationDeadline = Time.realtimeSinceStartup + 4f;
                        while (follower.color.r <= follower.color.g && Time.realtimeSinceStartup < separationDeadline)
                            yield return null;
                        input.Release(keys.sKey);
                        yield return null;
                        Assert.That(follower.color.r, Is.GreaterThan(follower.color.g),
                            "Walking away from the bridge lane must visibly stop the wounded follower.");
                        Assert.That(activePrelude.CanDeliverEvacuee, Is.False,
                            "A separated follower cannot be delivered without returning to the bridge lane.");
                        Assert.That(activePrelude.CurrentInstructions, Does.Contain("bridge lane"),
                            "A player who stepped off the bridge needs a lane-specific recovery hint.");
                        Assert.That(escortStatus.text, Is.EqualTo("WAIT"),
                            "Off-lane separation must show readable state beside the follower.");

                        // Switching locale without moving must refresh the
                        // recovery instruction and readable follower status.
                        SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                        yield return null;
                        Assert.That(activePrelude.CurrentInstructions, Does.Contain("桥面通道"),
                            "A stranded player must see the lane recovery in the selected language.");
                        Assert.That(escortStatus.text, Is.EqualTo("等待"));
                        Assert.That(activePrelude.Phase, Is.EqualTo(10));
                        SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                        yield return null;
                        Assert.That(activePrelude.CurrentInstructions, Does.Contain("bridge lane"));
                        Assert.That(escortStatus.text, Is.EqualTo("WAIT"));

                        input.Press(keys.wKey);
                        float regroupDeadline = Time.realtimeSinceStartup + 3f;
                        while (follower.color.g <= follower.color.r && Time.realtimeSinceStartup < regroupDeadline)
                            yield return null;
                        input.Release(keys.wKey);
                        yield return null;
                        Assert.That(follower.color.g, Is.GreaterThan(follower.color.r),
                            "Returning to the follower must restore a visible green escort state.");
                        Assert.That(activePrelude.CurrentInstructions, Does.Contain("Green: following"),
                            "Regrouping must replace the previous amber recovery hint.");
                        Assert.That(escortStatus.text, Is.EqualTo("FOLLOW"),
                            "Returning to the lane must restore readable follower state.");

                        SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                        yield return null;
                        Assert.That(activePrelude.CurrentInstructions, Does.Contain("绿色"),
                            "The connected-escort guidance must update in place with the locale.");
                        Assert.That(escortStatus.text, Is.EqualTo("跟随"));
                        SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                        yield return null;
                        Assert.That(activePrelude.CurrentInstructions, Does.Contain("Green: following"));
                        Assert.That(escortStatus.text, Is.EqualTo("FOLLOW"));
                        Assert.That(activePrelude.Phase, Is.EqualTo(10),
                            "Regrouping never skips the physical safe-line confirmation.");

                        // Carry one real S00 route through the already earned
                        // evacuation and two bells. The next sighting must
                        // require an actual player step to the lookout and a
                        // fresh E press; the scripted bell cannot spot troops.
                        input.Press(keys.aKey);
                        float safeDeadline = Time.realtimeSinceStartup + 6f;
                        while (!activePrelude.CanDeliverEvacuee &&
                               Time.realtimeSinceStartup < safeDeadline)
                            yield return null;
                        input.Release(keys.aKey);
                        yield return null;
                        Assert.That(activePrelude.CanDeliverEvacuee, Is.True);
                        Assert.That(activePrelude.CurrentInstructions,
                            Does.Contain("At the green safe line"),
                            "The handoff hint must become affirmative only when both bodies arrive.");
                        input.Press(keys.eKey);
                        yield return null;
                        input.Release(keys.eKey);
                        yield return null;
                        Assert.That(activePrelude.Phase, Is.EqualTo(11));
                        Assert.That(escortStatus.gameObject.activeSelf, Is.False,
                            "The old follower status must disappear after safe arrival.");
                        var coil = activePrelude.transform.Find(
                            "S00 Whitewater playable canvas/PLACEHOLDER S00 spare rope at clamp");
                        Assert.That(coil, Is.Not.Null);
                        Assert.That(coil.gameObject.activeInHierarchy, Is.False,
                            "The rope must not appear before the earned call after a safe escort.");
                        FindButton(activePrelude, "S00 call for spare rope").onClick.Invoke();
                        Assert.That(activePrelude.Phase, Is.EqualTo(12));
                        Assert.That(coil.gameObject.activeInHierarchy, Is.True);
                        var coilImage = coil.GetComponent<Image>();
                        Assert.That(coilImage.sprite, Is.Not.Null);
                        Assert.That(coilImage.sprite.name, Does.Contain("spare rope coil"));
                        Assert.That(coilImage.sprite.texture.width, Is.EqualTo(16));
                        Assert.That(coilImage.sprite.texture.height, Is.EqualTo(16));
                        Assert.That(coilImage.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                        Assert.That(coilImage.sprite.pixelsPerUnit, Is.EqualTo(16f));
                        Assert.That(((RectTransform)coil).sizeDelta, Is.EqualTo(new Vector2(32f, 32f)),
                            "The coil must retain exact 2x pixel scaling in the existing UGUI scene.");
                        Assert.That(coilImage.sprite.texture.GetPixel(7, 8).a,
                            Is.GreaterThan(0.9f), "The wound loop needs a real interior rope pixel.");
                        Assert.That(coilImage.raycastTarget, Is.False,
                            "A travelling rope cannot steal UI choice clicks or gamepad focus.");

                        input.Press(keys.dKey);
                        float postDeadline = Time.realtimeSinceStartup + 6f;
                        while (!activePrelude.CanTakeBridgePost &&
                               Time.realtimeSinceStartup < postDeadline)
                            yield return null;
                        input.Release(keys.dKey);
                        yield return null;
                        Assert.That(activePrelude.CanTakeBridgePost, Is.True);
                        Assert.That(activePrelude.CurrentInstructions, Does.Contain("left stick"),
                            "The bridge post must advertise movement when gameplay control returns.");
                        yield return AssertStationCueTracksLiveRebind(activePrelude);
                        yield return AssertBridgeStationNeedsVisibleContact(input, keys,
                            activePrelude, candidate => candidate.CanTakeBridgePost, 12);
                        input.Press(keys.eKey);
                        yield return null;
                        input.Release(keys.eKey);
                        yield return null;
                        Assert.That(activePrelude.Phase, Is.EqualTo(13));

                        var watch = FindButton(activePrelude, "S00 watch opposite bank");
                        watch.onClick.Invoke();
                        Assert.That(activePrelude.Phase, Is.EqualTo(14));
                        Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True);
                        Assert.That(activePrelude.CanWarnFarBank, Is.False,
                            "The second bell must not automatically reveal the enemy approach.");
                        var lookout = activePrelude.transform.Find(
                            "S00 Whitewater playable canvas/S00 forward lookout");
                        Assert.That(lookout, Is.Not.Null);
                        Assert.That(lookout.gameObject.activeInHierarchy, Is.True);
                        var approaching = activePrelude.transform.Find(
                            "S00 Whitewater playable canvas/S00 far-bank approaching figure 0");
                        Assert.That(approaching, Is.Not.Null);
                        Assert.That(approaching.gameObject.activeInHierarchy, Is.False);
                        // Capture the authored spawn position before the warning starts its
                        // coroutine. The first observed warning frame already moves the guard.
                        float threatSpawnX = ((RectTransform)approaching).anchoredPosition.x;
                        var approachingImage = approaching.GetComponent<Image>();
                        var playerImage = activePrelude.transform.Find(
                            "S00 Whitewater playable canvas/PLACEHOLDER player")
                            .GetComponent<Image>();
                        Assert.That(approachingImage.sprite, Is.Not.SameAs(playerImage.sprite),
                            "The far-bank spear guard must be visually distinct from the player.");
                        Assert.That(approachingImage.sprite.texture.width, Is.EqualTo(16));
                        Assert.That(approachingImage.sprite.texture.height, Is.EqualTo(16));
                        Assert.That(approachingImage.sprite.texture.filterMode,
                            Is.EqualTo(FilterMode.Point));
                        Assert.That(approachingImage.rectTransform.sizeDelta,
                            Is.EqualTo(new Vector2(32f, 32f)),
                            "The spear guard must use the same exact 2x pixel scaling.");
                        Assert.That(approachingImage.sprite.texture.GetPixel(13, 15).a,
                            Is.GreaterThan(0.9f),
                            "The upright spear must be present in the guard silhouette.");
                        Assert.That(playerImage.sprite.texture.GetPixel(13, 15).a,
                            Is.EqualTo(0f), "The player silhouette must not share that spear.");
                        Assert.That(approachingImage.raycastTarget, Is.False,
                            "Distant guards cannot steal gamepad or mouse interaction.");
                        Sprite guardIdle = approachingImage.sprite;
                        Assert.That(guardIdle.name, Does.Not.Contain("walking"));

                        input.Press(keys.aKey);
                        float sightlineDeadline = Time.realtimeSinceStartup + 4f;
                        while (!activePrelude.CanWarnFarBank &&
                               Time.realtimeSinceStartup < sightlineDeadline)
                            yield return null;
                        input.Release(keys.aKey);
                        yield return null;
                        Assert.That(activePrelude.CanWarnFarBank, Is.True,
                            "The player must move to the physical forward lookout.");
                        Assert.That(activePrelude.CurrentInstructions, Does.Contain("left stick"),
                            "The lookout needs movement instructions after the second bell.");
                        Assert.That(activePrelude.Phase, Is.EqualTo(14),
                            "Walking into view without E does not discover troops.");
                        yield return AssertBridgeStationNeedsVisibleContact(input, keys,
                            activePrelude, candidate => candidate.CanWarnFarBank, 14);
                        input.Press(keys.eKey);
                        yield return null;
                        input.Release(keys.eKey);
                        yield return null;
                        Assert.That(activePrelude.Phase, Is.EqualTo(15));
                        Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True,
                            "Warning the crew returns control for the physical cable hold.");
                        Assert.That(activePrelude.CanHoldCable, Is.False,
                            "Spotting troops does not magically return the player to Shen Li.");
                        Assert.That(lookout.GetComponent<Image>().color.g,
                            Is.GreaterThan(0.8f));
                        Assert.That(approaching.gameObject.activeInHierarchy, Is.True,
                            "Spotting must reveal the actual far-bank figures.");
                        float threatStartX = ((RectTransform)approaching).anchoredPosition.x;
                        Assert.That(activePrelude.CurrentNarration,
                            Does.Contain("Three armed silhouettes"));
                        var bell = activePrelude.GetComponentInChildren<AudioSource>(true);
                        Assert.That(bell.clip.name, Does.Contain("second warning bell"),
                            "The lookout warning must not invent the scripted third bell.");
                        Assert.That(bell.outputAudioMixerGroup.name, Is.EqualTo("SFX"),
                            "S00 warning bells must still use the saved player mute and volume bus.");
                        Assert.That(bell.clip.length, Is.InRange(1.25f, 1.35f),
                            "The distant bell should decay naturally instead of cutting off as a short beep.");
                        var bellSamples = new float[bell.clip.samples];
                        Assert.That(bell.clip.GetData(bellSamples, 0), Is.True);
                        int tailStart = Mathf.RoundToInt(0.96f * bell.clip.frequency);
                        Assert.That(bellSamples.Skip(tailStart).Any(sample =>
                            Mathf.Abs(sample) > 0.0003f), Is.True,
                            "The warning bell must still ring audibly after its initial strike.");
                        Assert.That(bellSamples.All(sample => Mathf.Abs(sample) < 0.5f), Is.True,
                            "The authored inharmonic bell must not clip or overpower gameplay audio.");

                        var post = activePrelude.transform.Find(
                            "S00 Whitewater playable canvas/S00 bridge defence post");
                        Assert.That(post, Is.Not.Null);
                        Assert.That(post.GetComponent<Image>().color.r,
                            Is.GreaterThan(post.GetComponent<Image>().color.g),
                            "The cable needs help again only after a genuine warning.");
                        input.Press(keys.dKey);
                        float returnDeadline = Time.realtimeSinceStartup + 4f;
                        bool observedMarchFrame = false;
                        while (!activePrelude.CanHoldCable &&
                               Time.realtimeSinceStartup < returnDeadline)
                        {
                            observedMarchFrame |= approachingImage.sprite != guardIdle;
                            yield return null;
                            Assert.That(((RectTransform)approaching).anchoredPosition.x,
                                Is.EqualTo(Mathf.Round(
                                    ((RectTransform)approaching).anchoredPosition.x)).Within(0.001f),
                                "The marching 16px guard must stay on whole canvas pixels.");
                        }
                        observedMarchFrame |= approachingImage.sprite != guardIdle;
                        input.Release(keys.dKey);
                        yield return null;
                        Assert.That(activePrelude.CanHoldCable, Is.True,
                            "The player must actually return to Shen Li's post.");
                        Assert.That(activePrelude.CurrentInstructions, Does.Contain("left stick"),
                            "The cable return must advertise the active movement controls.");
                        yield return AssertBridgeStationNeedsVisibleContact(input, keys,
                            activePrelude, candidate => candidate.CanHoldCable, 15);
                        Assert.That(observedMarchFrame, Is.True,
                            "The far-bank soldiers must show a distinct pixel footfall while advancing.");
                        Assert.That(approachingImage.sprite.texture.filterMode,
                            Is.EqualTo(FilterMode.Point));
                        Assert.That(approachingImage.rectTransform.sizeDelta,
                            Is.EqualTo(new Vector2(32f, 32f)));
                        float threatAfterReturnX = ((RectTransform)approaching).anchoredPosition.x;
                        Assert.That(threatAfterReturnX, Is.GreaterThan(threatStartX + 4f),
                            "The silhouettes must approach during the player-controlled return, rather than standing still.");
                        Assert.That(threatAfterReturnX, Is.LessThan(-100f),
                            "A visual threat must not teleport into a fake ORK battle.");
                        Assert.That(activePrelude.Phase, Is.EqualTo(15),
                            "Reaching the clamp is not itself an interaction.");
                        Vector2 coilBeforeHold = ((RectTransform)coil).anchoredPosition;
                        Assert.That(coilBeforeHold.y, Is.GreaterThan(40f),
                            "The spare rope must remain at the handoff point until the player braces the cable.");
                        var shenLiSprite = activePrelude.transform.Find(
                            "S00 Whitewater playable canvas/PLACEHOLDER Shen Li")
                            .GetComponent<Image>();
                        Sprite beforeHoldPose = shenLiSprite.sprite;
                        Assert.That(beforeHoldPose, Is.Not.Null);
                        Assert.That(beforeHoldPose.texture.GetPixel(0, 7).a,
                            Is.EqualTo(0f),
                            "Shen Li must not brace the cable before the earned interaction.");
                        input.Press(keys.eKey);
                        yield return null;
                        input.Release(keys.eKey);
                        yield return null;
                        Assert.That(activePrelude.Phase, Is.EqualTo(16));
                        Assert.That(shenLiSprite.sprite, Is.Not.SameAs(beforeHoldPose),
                            "Shen Li must visibly raise the wrench only on the actual cable hold.");
                        Assert.That(shenLiSprite.sprite.texture.width, Is.EqualTo(16));
                        Assert.That(shenLiSprite.sprite.texture.height, Is.EqualTo(16));
                        Assert.That(shenLiSprite.sprite.texture.filterMode,
                            Is.EqualTo(FilterMode.Point));
                        Assert.That(shenLiSprite.rectTransform.sizeDelta,
                            Is.EqualTo(new Vector2(32f, 32f)),
                            "The bracing pose must retain the exact existing 2x pixel scale.");
                        Assert.That(shenLiSprite.sprite.texture.GetPixel(0, 7).a,
                            Is.GreaterThan(0.9f),
                            "The new left-facing wrench must be drawn in the action frame.");
                        Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                        Assert.That(post.GetComponent<Image>().color.g, Is.GreaterThan(0.8f),
                            "The actual held post must change visibly after E / Pad A.");
                        var cable = activePrelude.transform.Find(
                            "S00 Whitewater playable canvas/Loose cable clamp");
                        Assert.That(cable, Is.Not.Null);
                        Assert.That(cable.GetComponent<Image>().color.r, Is.GreaterThan(0.9f),
                            "Shen Li's strained cable must show the physical hold.");
                        Assert.That(activePrelude.CurrentNarration, Does.Contain("For a moment it holds"));
                        Assert.That(approaching.gameObject.activeInHierarchy, Is.True);
                        Assert.That(bell.clip.name, Does.Contain("second warning bell"),
                            "Holding the cable must not invent a third bell or a fake ORK victory.");

                        // The held clamp buys time but cannot freeze the
                        // spotted enemy. Only the actual cable interaction
                        // permits this last visible approach; no combat or
                        // historical third-bell result is issued by the view.
                        // CI can spend part of this realtime window on
                        // Unity Editor player-loop/editor transitions while
                        // the authored figures still move at 24 units per
                        // rendered second. Await the same physical target,
                        // not a fixed number of frames or a shortened timer.
                        float pressureDeadline = Time.realtimeSinceStartup + 8f;
                        // Pixel-snapped motion can land exactly on this
                        // whole-pixel threshold. Wait until the strict
                        // assertion below is true, not until it first equals
                        // the threshold (the old float motor overshot it).
                        while (((RectTransform)approaching).anchoredPosition.x <=
                               threatStartX + 70f &&
                               Time.realtimeSinceStartup < pressureDeadline)
                            yield return null;
                        float afterHoldX = ((RectTransform)approaching).anchoredPosition.x;
                        Assert.That(afterHoldX, Is.GreaterThan(threatStartX + 70f),
                            "The same soldiers must keep closing in after the player actually holds the cable.");
                        Assert.That(afterHoldX, Is.LessThan(-180f),
                            "The staging silhouettes must remain distant and cannot fake an ORK battle.");
                        float marchEndDeadline = Time.realtimeSinceStartup + 6f;
                        while (((RectTransform)approaching).anchoredPosition.x <
                               threatSpawnX + 77.99f &&
                               Time.realtimeSinceStartup < marchEndDeadline)
                            yield return null;
                        Assert.That(((RectTransform)approaching).anchoredPosition.x,
                            Is.EqualTo(threatSpawnX + 78f).Within(0.02f),
                            "The scene-only patrol must stop at the already authored distance.");
                        Assert.That(approachingImage.sprite, Is.SameAs(guardIdle),
                            "The soldiers must return to their original standing pixels after stopping.");
                        Vector2 braceTarget = ((RectTransform)cable).anchoredPosition +
                            new Vector2(-3f, 18f);
                        Vector2 coilAfterHold = ((RectTransform)coil).anchoredPosition;
                        Assert.That(Vector2.Distance(coilAfterHold, braceTarget),
                            Is.LessThan(0.01f),
                            "The actual hand-delivered rope must move onto the cable clamp after E / Pad A.");
                        Assert.That(Vector2.Distance(coilBeforeHold, coilAfterHold),
                            Is.GreaterThan(20f),
                            "A changed caption alone is not a visible cable-brace action.");
                        Assert.That(coilAfterHold.x % 1f, Is.EqualTo(0f));
                        Assert.That(coilAfterHold.y % 1f, Is.EqualTo(0f),
                            "The 16px rope must land on whole canvas pixels.");
                        Assert.That(activePrelude.Phase, Is.EqualTo(16),
                            "Visual pressure must not skip into an unimplemented third-bell break.");
                        Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                        Assert.That(bell.clip.name, Does.Contain("second warning bell"));

                        float battleDeadline = Time.realtimeSinceStartup + 35f;
                        while (!defense.BattleInProgress &&
                               Time.realtimeSinceStartup < battleDeadline)
                            yield return null;
                        Assert.That(defense.IsReady, Is.True,
                            "The S00 ORK party must register before the bridgehead encounter.");
                        Assert.That(defense.BattleInProgress, Is.True,
                            "The three advancing silhouettes must resolve into a real ORK encounter, not a staged attack.");
                        float actionDeadline = Time.realtimeSinceStartup + 8f;
                        bool acceptedAction = false;
                        while (!acceptedAction && Time.realtimeSinceStartup < actionDeadline)
                        {
                            acceptedAction = defense.TryAttack() || defense.TryGuard();
                            yield return null;
                        }
                        Assert.That(acceptedAction, Is.True,
                            "At least one live ORK chooser must accept a real Attack or Guard action.");
                    }

                    activePrelude.Close();
                    yield return null;
                    Assert.That(root == null, Is.True,
                        "Replaying an alternate S00 path must release its earlier stage.");
                    activePrelude = null;
                }
            }
            finally
            {
                if (activePrelude != null)
                    activePrelude.Close();
                SoundwellUiLocalization.SetLanguage(originalLanguage);
                SoundwellInputAdapter.SetUiContext();
                input.TearDown();
            }
            yield return new ExitPlayMode();
        }


        // Rebinding does not move a character or change locale. The station
        // hint must still refresh on the NEXT real input frame, then return
        // to the original caption once the binding is restored. This runs
        // within the existing S00 gameplay test; no fake rescue or ORK turn.
        private static IEnumerator AssertStationCueTracksLiveRebind(WhitewaterPrelude stage)
        {
            var actionAsset = Resources.Load<InputActionAsset>("Input/SoundwellControls");
            Assert.That(actionAsset, Is.Not.Null);
            InputAction ring = actionAsset.FindActionMap("Gameplay", true)
                .FindAction("Ring", true);
            int keyIndex = -1;
            for (int i = 0; i < ring.bindings.Count; i++)
                if (ring.bindings[i].path == "<Keyboard>/e")
                {
                    keyIndex = i;
                    break;
                }
            Assert.That(keyIndex, Is.GreaterThanOrEqualTo(0));
            string originalOverride = ring.bindings[keyIndex].overridePath;
            string originalText = stage.CurrentInstructions;
            int phase = stage.Phase;
            try
            {
                ring.ApplyBindingOverride(keyIndex, "<Keyboard>/h");
                string activeKey = SoundwellInputAdapter.GameplayKeyboardInteractionLabel();
                Assert.That(activeKey, Is.Not.Empty);
                yield return null;
                Assert.That(stage.CurrentInstructions,
                    Does.Contain(activeKey + " / Pad"),
                    "Rebinding Gameplay/Ring must update the already-visible station cue.");
                Assert.That(stage.CurrentInstructions, Is.Not.EqualTo(originalText),
                    "A reachable station cannot leave its old input label cached.");
                Assert.That(stage.Phase, Is.EqualTo(phase),
                    "A binding change is not an earned interaction.");
            }
            finally
            {
                if (originalOverride == null)
                    ring.RemoveBindingOverride(keyIndex);
                else
                    ring.ApplyBindingOverride(keyIndex, originalOverride);
            }
            yield return null;
            Assert.That(stage.CurrentInstructions, Is.EqualTo(originalText),
                "Restoring the original binding must also restore the station cue.");
            Assert.That(stage.Phase, Is.EqualTo(phase));
        }

        // Real keyboard movement and Ring presses through the shipping Input
        // System: standing above the bridge's visible marker must not count
        // as taking a post, spotting troops, or holding Shen Li's cable.
        private static IEnumerator AssertBridgeStationNeedsVisibleContact(
            InputTestFixture input, Keyboard keys, WhitewaterPrelude stage,
            System.Func<WhitewaterPrelude, bool> canUse, int phase)
        {
            Assert.That(canUse(stage), Is.True,
                "The player must first be horizontally in reach of the real station.");
            input.Press(keys.wKey);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (stage.WalkerPosition.y < 38f && Time.realtimeSinceStartup < deadline)
                yield return null;
            input.Release(keys.wKey);
            yield return null;
            Assert.That(stage.WalkerPosition.y, Is.GreaterThan(35f),
                "The player must actually move outside the station's visible vertical reach.");
            Assert.That(canUse(stage), Is.False,
                "The old loose station radius gave interaction credit off the bridge.");
            Assert.That(stage.CurrentInstructions, Does.Contain(
                phase == 15 ? "Return" : "Reach"),
                "Walking off the amber marker must restore the phase-correct approach cue.");
            input.Press(keys.eKey);
            yield return null;
            input.Release(keys.eKey);
            yield return null;
            Assert.That(stage.Phase, Is.EqualTo(phase),
                "A real off-bridge interaction press cannot advance S00 or award a false warning.");
            Assert.That(stage.CurrentInstructions, Does.Contain(
                phase == 14 ? "amber lookout" : phase == 15 ? "amber cable post" : "amber bridge post"),
                "A rejected real E press must identify the correct reachable marker.");
            Assert.That(stage.CurrentInstructions, Does.Contain("left stick"),
                "The rejected action must still explain the playable movement control.");
            // Both languages must offer a reachable action instead of a
            // silent miss; localization cannot advance the phase on its own.
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            input.Press(keys.eKey);
            yield return null;
            input.Release(keys.eKey);
            yield return null;
            Assert.That(stage.Phase, Is.EqualTo(phase));
            Assert.That(stage.CurrentInstructions, Does.Contain(
                phase == 14 ? "瞭望点" : phase == 15 ? "缆桩" : "桥头守位"));
            Assert.That(stage.CurrentInstructions, Does.Contain("左摇杆"));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            input.Press(keys.sKey);
            deadline = Time.realtimeSinceStartup + 3f;
            while (stage.WalkerPosition.y > 9f && Time.realtimeSinceStartup < deadline)
                yield return null;
            input.Release(keys.sKey);
            yield return null;
            Assert.That(stage.WalkerPosition.y, Is.LessThanOrEqualTo(12f));
            Assert.That(canUse(stage), Is.True,
                "Returning to the same visible marker must restore the earned interaction.");
            Assert.That(stage.Phase, Is.EqualTo(phase),
                "Proximity feedback cannot grant the post, warning or cable hold.");
            Assert.That(stage.CurrentInstructions, Does.Contain("At "),
                "Returning into real station reach must switch to a ready-to-interact cue.");
            Assert.That(stage.CurrentInstructions, Does.Contain("press "),
                "A ready station must tell the player which input to use before pressing it.");
        }

        private static Button FindButton(WhitewaterPrelude stage, string name)
        {
            return stage.GetComponentsInChildren<Button>(true)
                .Single(button => button.gameObject.name == name);
        }

        private static void ClickChoice(WhitewaterPrelude stage, int index)
        {
            FindButton(stage, "S00 choice " + index).onClick.Invoke();
        }
    }
}
