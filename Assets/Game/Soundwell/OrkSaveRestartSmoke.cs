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
                saves.Save(Slot);
                if (!saves.FileExists(Slot))
                {
                    Fail("Makinom did not create a save file.");
                    yield break;
                }
                Debug.Log("RPG_ORK_SAVE_RESTART_WRITE");
            }
            else
            {
                if (OrkPartyBridge.RescueSettled ||
                    (M02QiQueDebtFacts.TryRead(out bool known) && known))
                {
                    Fail("The read process must start with fresh gameplay state.");
                    yield break;
                }
                saves.GameLoaded += OnGameLoaded;
                saves.Load(Slot);
                yield return WaitFor(() => loaded);
                saves.GameLoaded -= OnGameLoaded;
                if (!loaded || SceneManager.GetActiveScene().name != SoundwellEntrance.BootstrapSceneName ||
                    !OrkPartyBridge.RescueSettled || !HasRescueExperience(ORK.Game.ActiveGroup.GetGroup()) ||
                    !M02QiQueDebtFacts.TryRead(out bool debtKnown) || !debtKnown)
                {
                    Fail("The saved scene, quest, experience or chapter fact did not restore.");
                    yield break;
                }

                yield return EnterTrial();
                var party = FindFirstObjectByType<OrkPartyBridge>();
                if (party == null || !party.IsReady || !party.SettleRescue() ||
                    !HasRescueExperience(party.Members))
                {
                    Fail("A settled rescue must keep its experience without granting it twice after reentry.");
                    yield break;
                }
                Debug.Log("RPG_ORK_SAVE_RESTART_READ");
            }
            Application.Quit(0);
        }

        private void OnGameLoaded() => loaded = true;

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

        private static void Fail(string reason)
        {
            Debug.LogError("ORK save restart regression failed. " + reason);
            Application.Quit(2);
        }
    }
}
#endif
