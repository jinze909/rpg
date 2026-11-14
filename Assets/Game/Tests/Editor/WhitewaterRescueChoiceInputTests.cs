using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Rpg.Tests.Editor
{
    // The main S00 input regression follows Shen Li's rope. These two routes
    // must also require physical movement and a fresh gameplay interaction:
    // neither the authored choice nor a timer may automatically rescue Han Qiao.
    public sealed class WhitewaterRescueChoiceInputTests
    {
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
                foreach (int rescueChoice in new[] { 0, 1, 2 })
                {
                    var root = new GameObject("S00 alternate rescue input regression");
                    activePrelude = root.AddComponent<WhitewaterPrelude>();
                    activePrelude.Begin(() => SoundwellInputAdapter.SetEntryContext());
                    yield return null;

                    ClickChoice(activePrelude, 0); // Roll call: the player answers.
                    Assert.That(activePrelude.Phase, Is.EqualTo(1));
                    input.Press(keys.dKey);
                    float repairDeadline = Time.realtimeSinceStartup + 6f;
                    while (!activePrelude.CanRepair && Time.realtimeSinceStartup < repairDeadline)
                        yield return null;
                    input.Release(keys.dKey);
                    yield return null;
                    Assert.That(activePrelude.CanRepair, Is.True,
                        "The player must physically reach the clamp before repairing.");
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
                            yield return null;
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
                        input.Press(keys.aKey);
                        float reachDeadline = Time.realtimeSinceStartup + 4f;
                        while (!activePrelude.CanHelpEvacuee && Time.realtimeSinceStartup < reachDeadline)
                            yield return null;
                        input.Release(keys.aKey);
                        yield return null;
                        Assert.That(activePrelude.CanHelpEvacuee, Is.True);
                        input.Press(keys.eKey);
                        yield return null;
                        input.Release(keys.eKey);
                        yield return null;
                        Assert.That(activePrelude.Phase, Is.EqualTo(10));
                        var follower = activePrelude.transform.Find(
                            "S00 Whitewater playable canvas/S00 last evacuee").GetComponent<Image>();
                        Assert.That(follower.color.g, Is.GreaterThan(follower.color.r),
                            "Green indicates a follower is connected to the escort.");

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

                        input.Press(keys.wKey);
                        float regroupDeadline = Time.realtimeSinceStartup + 3f;
                        while (follower.color.g <= follower.color.r && Time.realtimeSinceStartup < regroupDeadline)
                            yield return null;
                        input.Release(keys.wKey);
                        yield return null;
                        Assert.That(follower.color.g, Is.GreaterThan(follower.color.r),
                            "Returning to the follower must restore a visible green escort state.");
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
                        input.Press(keys.eKey);
                        yield return null;
                        input.Release(keys.eKey);
                        yield return null;
                        Assert.That(activePrelude.Phase, Is.EqualTo(11));
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
                        Assert.That(activePrelude.Phase, Is.EqualTo(14),
                            "Walking into view without E does not discover troops.");
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
                        }
                        observedMarchFrame |= approachingImage.sprite != guardIdle;
                        input.Release(keys.dKey);
                        yield return null;
                        Assert.That(activePrelude.CanHoldCable, Is.True,
                            "The player must actually return to Shen Li's post.");
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
                        float pressureDeadline = Time.realtimeSinceStartup + 4f;
                        while (((RectTransform)approaching).anchoredPosition.x <
                               threatStartX + 70f &&
                               Time.realtimeSinceStartup < pressureDeadline)
                            yield return null;
                        float afterHoldX = ((RectTransform)approaching).anchoredPosition.x;
                        Assert.That(afterHoldX, Is.GreaterThan(threatStartX + 70f),
                            "The same soldiers must keep closing in after the player actually holds the cable.");
                        Assert.That(afterHoldX, Is.LessThan(-180f),
                            "The staging silhouettes must remain distant and cannot fake an ORK battle.");
                        float marchEndDeadline = Time.realtimeSinceStartup + 2f;
                        while (((RectTransform)approaching).anchoredPosition.x <
                               threatSpawnX + 77.99f &&
                               Time.realtimeSinceStartup < marchEndDeadline)
                            yield return null;
                        Assert.That(((RectTransform)approaching).anchoredPosition.x,
                            Is.EqualTo(threatSpawnX + 78f).Within(0.02f),
                            "The scene-only patrol must stop at the already authored distance.");
                        Assert.That(approachingImage.sprite, Is.SameAs(guardIdle),
                            "The soldiers must return to their original standing pixels after stopping.");
                        Assert.That(activePrelude.Phase, Is.EqualTo(16),
                            "Visual pressure must not skip into an unimplemented third-bell break.");
                        Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                        Assert.That(bell.clip.name, Does.Contain("second warning bell"));
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
