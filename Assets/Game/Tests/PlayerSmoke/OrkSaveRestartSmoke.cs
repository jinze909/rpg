#if RPG_TEST_PROFILE && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rpg.Soundwell
{
    // The Linux Test Player runs this only when launched by the save regression.
    internal sealed class OrkSaveRestartSmoke : MonoBehaviour
    {
        private const int Slot = 0;
        private const string LiveFact = "rpg.test.checkpoint-read-keeps-live-fact";
        private bool writing;
        private bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartSmoke()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-rpg-save-restart");
            if (index < 0)
                return;
            if (index + 1 >= args.Length || (args[index + 1] != "write" && args[index + 1] != "read"))
            {
                Fail("Expected a write or read process.");
                return;
            }
            var root = new GameObject("ORK save restart regression");
            DontDestroyOnLoad(root);
            root.AddComponent<OrkSaveRestartSmoke>().writing = args[index + 1] == "write";
        }

        private IEnumerator Start()
        {
            string sandbox = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(sandbox) || !Application.persistentDataPath.StartsWith(
                Path.GetFullPath(sandbox) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                Fail("Unity persistent data path is outside the isolated test directory.");
                yield break;
            }

            yield return WaitFor(() => ORK.Initialized);
            if (!ORK.Initialized)
            {
                Fail("The ORK bootstrap did not initialize.");
                yield break;
            }

            var saves = Maki.SaveGame;
            saves.SetSaveFileHandler(new PersistentDataPathSaveGameFileHandler());
            if (saves.FileExists(Slot) == writing)
            {
                Fail(writing ? "The write slot already exists." : "The write process left no save file.");
                yield break;
            }

            if (writing)
            {
                yield return EnterTrial();
                var party = FindFirstObjectByType<OrkPartyBridge>();
                if (party == null || !party.IsReady)
                {
                    Fail("The three-member ORK party never became ready after scene load.");
                    yield break;
                }
                if (!party.SettleRescue())
                {
                    Fail("The ORK rescue quest could not be marked finished.");
                    yield break;
                }
                if (!HasRescueExperience(party.Members))
                {
                    Fail("The ORK rescue quest finished without granting all three experience rewards.");
                    yield break;
                }
                if (!M02QiQueDebtFacts.TryRecordDiscovered())
                {
                    Fail("The live Makinom chapter variables did not accept the debt discovery fact.");
                    yield break;
                }
                var trial = FindFirstObjectByType<SoundwellTrial>();
                yield return trial.ReturnToBootstrap();
                if (SceneManager.GetActiveScene().name != SoundwellEntrance.BootstrapSceneName)
                {
                    Fail("Could not save from the Bootstrap checkpoint.");
                    yield break;
                }
                // Persist the actual M03 route, M04 departure/roster, completed
                // fog passage, M05 landing observation, and the player's
                // arrival-specific letter choice into the same ORK save slot.
                if (!RecordVoyageFacts())
                    yield break;
                yield return WaitFor(() => CheckpointButton("Save")?.IsInteractable() == true);
                var saveButton = CheckpointButton("Save");
                if (saveButton == null || !saveButton.IsInteractable())
                {
                    Fail("The Bootstrap save button never enabled.");
                    yield break;
                }
                saveButton.onClick.Invoke();
                if (!saves.FileExists(Slot))
                {
                    Fail("Makinom did not create a save file.");
                    yield break;
                }
                string path = CheckpointPath(saves);
                byte[] recorded = File.ReadAllBytes(path);
                // Start a confirmation, then leave and return to the same Bootstrap owner.
                var saveLabel = saveButton.GetComponentInChildren<UnityEngine.UI.Text>();
                saveButton.onClick.Invoke();
                if (saveLabel.text != "Confirm overwrite")
                {
                    Fail("The existing checkpoint did not ask for overwrite confirmation.");
                    yield break;
                }
                FindFirstObjectByType<SoundwellEntrance>().OpenPrelude();
                var prelude = FindFirstObjectByType<WhitewaterPrelude>();
                if (prelude == null)
                {
                    Fail("The checkpoint confirmation visit did not reach Whitewater.");
                    yield break;
                }
                prelude.Close();
                yield return WaitFor(() => saveButton.IsInteractable() && saveLabel.text == "Save checkpoint");
                if (saveLabel.text != "Save checkpoint")
                {
                    Fail("The overwrite confirmation did not expire on return to entry.");
                    yield break;
                }
                saveButton.onClick.Invoke();
                if (saveLabel.text != "Confirm overwrite" ||
                    !System.Linq.Enumerable.SequenceEqual(recorded, File.ReadAllBytes(path)))
                {
                    Fail("An overwrite confirmation survived leaving the entry screen.");
                    yield break;
                }
                Debug.Log("RPG_ORK_SAVE_RESTART_WRITE");
            }
            else
            {
                if (OrkPartyBridge.RescueSettled ||
                    (M02QiQueDebtFacts.TryRead(out bool known) && known) ||
                    !HasFreshVoyageFacts())
                {
                    Fail("The read process must start with fresh gameplay and voyage state.");
                    yield break;
                }
                yield return WaitFor(() => CheckpointButton("Load")?.IsInteractable() == true);
                var loadButton = CheckpointButton("Load");
                if (loadButton == null || !loadButton.IsInteractable() ||
                    !RejectsIncompleteCheckpoint(saves, loadButton))
                {
                    Fail("Loading an incomplete checkpoint changed the live session or left no error message.");
                    yield break;
                }
                var entrance = FindFirstObjectByType<SoundwellEntrance>();
                saves.GameLoaded += OnGameLoaded;
                loadButton.onClick.Invoke();
                loadButton.onClick.Invoke();
                if (saves.IsLoading && entrance.OpenTrial() != null)
                {
                    Fail("Entry accepted a second scene transition during load.");
                    yield break;
                }
                yield return WaitFor(() => loaded);
                saves.GameLoaded -= OnGameLoaded;
                if (!loaded || SceneManager.GetActiveScene().name != SoundwellEntrance.BootstrapSceneName ||
                    !OrkPartyBridge.RescueSettled || !HasRescueExperience(ORK.Game.ActiveGroup.GetGroup()) ||
                    !M02QiQueDebtFacts.TryRead(out bool debtKnown) || !debtKnown ||
                    !VerifyVoyageFacts("after checkpoint load"))
                {
                    Fail("The saved scene, quest, experience, chapter, or voyage facts did not restore.");
                    yield break;
                }

                yield return EnterTrial();
                var party = FindFirstObjectByType<OrkPartyBridge>();
                if (party == null || !party.IsReady)
                {
                    Fail("Re-entering the trial did not register the ORK party.");
                    yield break;
                }
                if (!party.SettleRescue())
                {
                    Fail("A restored ORK rescue quest lost its finished state after reentry.");
                    yield break;
                }
                if (!HasRescueExperience(party.Members))
                {
                    Fail("The re-entered party lost or duplicated its ORK experience: " +
                        DescribeExperience(party.Members));
                    yield break;
                }
                if (!VerifyVoyageFacts("after trial re-entry"))
                    yield break;
                Debug.Log("RPG_ORK_SAVE_RESTART_READ");
            }
            Application.Quit(0);
        }

        private static bool RecordVoyageFacts()
        {
            var encounter = new M03LuJunEncounter(M02BridgeEvidence.None, true);
            encounter.Begin();
            encounter.ObserveRollCall();
            encounter.Ask(M03LuJunOpening.NeedAShip);
            encounter.ChooseVoyage(M03VoyageChoice.QiQueMerchant);
            if (!M03LuJunFacts.TryRecord(encounter))
            {
                Fail("The real M03 voyage choice could not be saved.");
                return false;
            }

            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant,
                "Verified civilian hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named Red Salt receiver", false, false);
            if (!manifest.ConfirmDeparture() ||
                !M04DepartureFacts.TryRecord(manifest) ||
                !M04DepartureFacts.TryMarkSailed())
            {
                Fail("The M04 departure manifest or actual cast-off could not be saved.");
                return false;
            }

            var roster = new M04RosterLedger();
            roster.AddEntry("roll-01", "Listed person", M04RosterStatus.ListedDead);
            roster.MarkConfirmedAtDock("roll-01");
            roster.ResolveRollCorrection("roll-01");
            roster.AddEntry("dock-01", "Confirmed 1", M04RosterStatus.ConfirmedAtDock);
            roster.AddEntry("dock-02", "Confirmed 2", M04RosterStatus.ConfirmedAtDock);
            roster.AddEntry("unconfirmed-01", "Unconfirmed 1", M04RosterStatus.RansomedUnconfirmed);
            if (!roster.TryBookForVoyage("roll-01", 16))
            {
                Fail("The reconciled M04 roster could not book a verified berth.");
                return false;
            }
            roster.MarkDeclinedVoyage("dock-02");
            if (!M04RosterFacts.TryRecord(roster))
            {
                Fail("The reconciled M04 returnee roster could not be saved.");
                return false;
            }

            var fog = new FogListeningSession(new[]
            {
                new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80)
            }, maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            if (!fog.IsComplete || !fog.Succeeded || !FogListeningFacts.TryRecord(fog))
            {
                Fail("The completed M04 fog passage could not be saved.");
                return false;
            }

            if (!M05LandingFacts.TryRecord(new M05LandingReport(3, 10, true, true)))
            {
                Fail("The actual M05 crew, tender, receiver, and usable-berth observation could not be saved.");
                return false;
            }
            if (!M05LettersFacts.TryRecord(M05LetterDecision.TakePacket))
            {
                Fail("The M05 eight-letter packet choice could not be saved.");
                return false;
            }

            return VerifyVoyageFacts("before checkpoint save");
        }

        private static bool HasFreshVoyageFacts()
        {
            return M03LuJunFacts.TryRead(out _, out _, out M03VoyageChoice? route) &&
                !route.HasValue &&
                M04DepartureFacts.TryRead(out M04DepartureRecord? departure) &&
                !departure.HasValue &&
                M04RosterFacts.TryRead(out M04RosterSummaryRecord? roster) &&
                !roster.HasValue &&
                FogListeningFacts.TryRead(out FogListeningRecord? fog) &&
                !fog.HasValue &&
                M05LandingFacts.TryRead(out M05LandingReport? landing) &&
                !landing.HasValue &&
                M05LettersFacts.TryRead(out M05LetterDecision? letters) &&
                !letters.HasValue;
        }

        private static bool VerifyVoyageFacts(string phase)
        {
            if (!M03LuJunFacts.TryRead(out _, out _, out M03VoyageChoice? route) ||
                route != M03VoyageChoice.QiQueMerchant)
            {
                Fail("M03 route fact did not survive " + phase + ".");
                return false;
            }
            if (!M04DepartureFacts.TryRead(out M04DepartureRecord? departure) ||
                !departure.HasValue ||
                departure.Value.Route != M03VoyageChoice.QiQueMerchant ||
                departure.Value.AssignedCrew != 4 ||
                departure.Value.AvailableBerths != 16 ||
                departure.Value.ReturneesToBook != 8 ||
                !M04DepartureFacts.TryReadSailed(out bool sailed) || !sailed)
            {
                Fail("M04 route, crew, reserved berths, or cast-off fact did not survive " + phase + ".");
                return false;
            }
            if (!M04RosterFacts.TryRead(out M04RosterSummaryRecord? roster) ||
                !roster.HasValue ||
                roster.Value.TotalEntries != 4 ||
                roster.Value.ConfirmedAtDockCount != 1 ||
                roster.Value.BookedCount != 1 ||
                roster.Value.DeclinedCount != 1)
            {
                Fail("The reconciled M04 roster did not survive " + phase + ".");
                return false;
            }
            if (!FogListeningFacts.TryRead(out FogListeningRecord? fog) ||
                !fog.HasValue || !fog.Value.Succeeded || fog.Value.HullIntegrity != 2)
            {
                Fail("The completed M04 fog outcome did not survive " + phase + ".");
                return false;
            }
            if (!M05LandingFacts.TryRead(out M05LandingReport? landing) ||
                !landing.HasValue ||
                landing.Value.CrewArrived != 3 ||
                landing.Value.ReadyRescueBerths != 10 ||
                !landing.Value.TenderArrived ||
                !landing.Value.ReceiverMet)
            {
                Fail("The actual M05 crew/tender/receiver/berth observation did not survive " + phase + ".");
                return false;
            }
            if (!M05LettersFacts.TryRead(out M05LetterDecision? letters) ||
                letters != M05LetterDecision.TakePacket)
            {
                Fail("The M05 arrival-specific letter choice did not survive " + phase + ".");
                return false;
            }
            return true;
        }

        private void OnGameLoaded() => loaded = true;

        private static UnityEngine.UI.Button CheckpointButton(string action) =>
            GameObject.Find(action + " ORK checkpoint button")
                ?.GetComponent<UnityEngine.UI.Button>();

        private static string CheckpointPath(SaveGameHandler saves) =>
            Path.Combine(Application.persistentDataPath, saves.GetFileName(Slot) + ".save");

        private static bool RejectsIncompleteCheckpoint(SaveGameHandler saves, UnityEngine.UI.Button loadButton)
        {
            string path = CheckpointPath(saves);
            byte[] valid = File.ReadAllBytes(path);
            try
            {
                Maki.Game.Variables.Set(LiveFact, 1);
                File.WriteAllText(path, new DataObject().ToXML("savegame"));
                loadButton.onClick.Invoke();
                loadButton.onClick.Invoke();
                var label = loadButton.GetComponentInChildren<UnityEngine.UI.Text>();
                bool rejected = !saves.IsLoading && Maki.Game.Variables.GetInt(LiveFact) == 1 &&
                    label.text == "Could not read checkpoint";
                if (rejected)
                    Debug.Log("RPG_ORK_INVALID_CHECKPOINT_REJECTED");
                return rejected;
            }
            finally
            {
                File.WriteAllBytes(path, valid);
            }
        }

        private static IEnumerator EnterTrial()
        {
            var entrance = FindFirstObjectByType<SoundwellEntrance>();
            if (entrance != null)
            {
                yield return entrance.OpenTrial();
                // Scene activation and trial UI readiness are not sufficient:
                // ORK creates and registers the three real combatants in a
                // separate coroutine. Do not settle the quest on that race.
                yield return WaitFor(() =>
                {
                    var trial = FindFirstObjectByType<SoundwellTrial>();
                    var party = FindFirstObjectByType<OrkPartyBridge>();
                    return trial != null && trial.IsReady &&
                        party != null && party.IsReady &&
                        Maki.Initialized && Maki.Game?.Variables != null;
                });
            }
        }

        private static IEnumerator WaitFor(Func<bool> ready)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!ready() && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private static bool HasRescueExperience(IReadOnlyList<Combatant> members)
        {
            if (members.Count != 3)
                return false;
            foreach (var member in members)
                if (member.Status[OrkPartyBridge.ExperienceStatus].GetValue() != OrkPartyBridge.RescueExperience)
                    return false;
            return true;
        }

        private static string DescribeExperience(IReadOnlyList<Combatant> members)
        {
            if (members == null)
                return "party=null";
            var values = new string[members.Count];
            for (int i = 0; i < members.Count; i++)
                values[i] = members[i] == null ? "missing" :
                    members[i].Status[OrkPartyBridge.ExperienceStatus].GetValue().ToString();
            return "members=" + members.Count + " XP=[" + string.Join(",", values) + "]";
        }

        private static void Fail(string reason)
        {
            Debug.LogError("ORK save restart regression failed. " + reason);
            Application.Quit(2);
        }
    }
}
#endif
