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
                foreach (int rescueChoice in new[] { 0, 2 })
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
                    ClickChoice(activePrelude, rescueChoice);
                    Assert.That(activePrelude.Phase, Is.EqualTo(7));
                    Assert.That(inWater.anchoredPosition.x, Is.EqualTo(riverStartX),
                        "Choosing a rescuer cannot instantly board Han Qiao.");
                    Assert.That(activePrelude.CanRescue, Is.False,
                        "Neither alternative station is already in reach after the clamp repair.");
                    var station = activePrelude.transform.Find(
                        "S00 Whitewater playable canvas/S00 rescue station") as RectTransform;
                    Assert.That(station, Is.Not.Null);
                    Assert.That(station.anchoredPosition.x,
                        Is.EqualTo(rescueChoice == 0 ? 60f : -70f),
                        "Each authored rescue method must point to its own visible station.");

                    // Real Input System movement and the same E / Pad A
                    // gameplay interaction used by the main S00 player path.
                    input.Press(keys.aKey);
                    float rescueDeadline = Time.realtimeSinceStartup + 5f;
                    while (!activePrelude.CanRescue &&
                           Time.realtimeSinceStartup < rescueDeadline)
                        yield return null;
                    input.Release(keys.aKey);
                    yield return null;
                    Assert.That(activePrelude.CanRescue, Is.True,
                        "The player must physically arrive at the selected rescue station.");
                    Assert.That(activePrelude.Phase, Is.EqualTo(7),
                        "Arriving alone must not grant rescue before the action.");
                    input.Press(keys.eKey);
                    yield return null;
                    input.Release(keys.eKey);
                    yield return null;
                    Assert.That(activePrelude.Phase, Is.EqualTo(8));
                    Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                    Assert.That(activePrelude.CurrentNarration, Does.Contain(
                        rescueChoice == 0 ? "You heave Han Qiao" : "The soldiers lift"));
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
                        retreat.onClick.Invoke();
                        Assert.That(activePrelude.Phase, Is.EqualTo(9));
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
                        FindButton(activePrelude, "S00 call for spare rope").onClick.Invoke();
                        Assert.That(activePrelude.Phase, Is.EqualTo(12));

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
                        Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                        Assert.That(lookout.GetComponent<Image>().color.g,
                            Is.GreaterThan(0.8f));
                        Assert.That(approaching.gameObject.activeInHierarchy, Is.True,
                            "Spotting must reveal the actual far-bank figures.");
                        Assert.That(activePrelude.CurrentNarration,
                            Does.Contain("Three armed silhouettes"));
                        var bell = activePrelude.GetComponentInChildren<AudioSource>(true);
                        Assert.That(bell.clip.name, Does.Contain("second warning bell"),
                            "The lookout warning must not invent the scripted third bell.");
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
