using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M04PreflightCastOffHandoffTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> data)
        {
            return key => data.TryGetValue(key, out int value) ? value : 0;
        }

        private static Action<string, int> Writer(Dictionary<string, int> data)
        {
            return (key, value) => data[key] = value;
        }

        private static void SeedChosenRoute(Action<string, int> setInt,
            M03VoyageChoice route)
        {
            var encounter = new M03LuJunEncounter(M02BridgeEvidence.None, true);
            encounter.Begin();
            encounter.ObserveRollCall();
            encounter.Ask(M03LuJunOpening.NeedAShip);
            encounter.ChooseVoyage(route);
            Assert.That(M03LuJunFacts.TryRecord(encounter, setInt), Is.True);
        }

        private static void SeedVerifiedDeparture(Action<string, int> setInt,
            M03VoyageChoice route)
        {
            var manifest = new M04DepartureManifest(route,
                "Confirmed hull at the dock", "Confirmed pilot",
                4, "Actual shallow-draft tender", true,
                18, 8, "Actual landing receiver", true, true);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, setInt), Is.True);
        }

        [Test]
        public void CastOffRequiresThisVisitsExactlyMatchingVerifiedAndUnsailedRoute()
        {
            var data = new Dictionary<string, int>();
            Func<string, int> read = Reader(data);
            Action<string, int> write = Writer(data);
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(null, out _),
                Is.False);
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read, out _),
                Is.False, "No route or ship exists merely because the scene begins.");

            SeedChosenRoute(write, M03VoyageChoice.QiQueMerchant);
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read, out _),
                Is.False, "An M03 route choice is not a confirmed departure.");

            SeedVerifiedDeparture(write, M03VoyageChoice.HanQiaoOfficial);
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read, out _),
                Is.False, "A different route's readiness cannot hijack this voyage.");

            SeedVerifiedDeparture(write, M03VoyageChoice.QiQueMerchant);
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read,
                out M04DepartureRecord? verified), Is.True);
            Assert.That(verified.HasValue, Is.True);
            Assert.That(verified.Value.AssignedCrew, Is.EqualTo(4));
            Assert.That(verified.Value.ReturneesToBook, Is.EqualTo(8));
            Assert.That(data[M04DepartureFacts.SailedKey], Is.Zero,
                "A verified plan does not mark cast-off.");

            data[M04DepartureFacts.SailedKey] = 2;
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read, out _),
                Is.False, "Corrupt sailing bits cannot grant a new cast-off.");
            data[M04DepartureFacts.SailedKey] = 0;
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read, out _),
                Is.False, "An already sailed vessel cannot be launched a second time.");

            Assert.That(M04DepartureFacts.TryResetForNewVisit(write), Is.True);
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read, out _),
                Is.False, "The next visit may not inherit a previous ship.");
        }

        [Test]
        public void CorruptOrIncompletePhysicalManifestCannotUnlockCastOff()
        {
            var data = new Dictionary<string, int>();
            Func<string, int> read = Reader(data);
            Action<string, int> write = Writer(data);
            SeedChosenRoute(write, M03VoyageChoice.LuJunEscort);
            SeedVerifiedDeparture(write, M03VoyageChoice.LuJunEscort);
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read, out _),
                Is.True);

            data[M04DepartureFacts.EscortFlagKey] = 0;
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read, out _),
                Is.False, "Lu Jun's escort flag must actually be accepted.");
            data[M04DepartureFacts.EscortFlagKey] = 1;
            data[M04DepartureFacts.AvailableBerthsKey] = 1;
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read, out _),
                Is.False, "Rescue berths may not exceed verified capacity.");
            data[M04DepartureFacts.AvailableBerthsKey] = 18;
            data[M04DepartureFacts.ConfirmedKey] = 0;
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read, out _),
                Is.False, "Interrupted saves cannot publish a prepared ship.");
        }

        [UnityTest]
        public IEnumerator LivePreflightOffersCastOffOnlyWithSavedPhysicalReadiness()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return null;

            GameObject owner = null;
            GameObject noShipOwner = null;
            try
            {
                EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/Game/Scenes/Bootstrap.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));
                float deadline = Time.realtimeSinceStartup + 8f;
                while ((!Maki.Initialized || Maki.Game?.Variables == null) &&
                       Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(Maki.Initialized && Maki.Game?.Variables != null, Is.True);

                var vars = Maki.Game.Variables;
                Action<string, int> write = (key, value) => vars.Set(key, value);
                Assert.That(M04DepartureFacts.TryResetForNewVisit(write), Is.True);
                SeedChosenRoute(write, M03VoyageChoice.QiQueMerchant);
                SeedVerifiedDeparture(write, M03VoyageChoice.QiQueMerchant);

                owner = new GameObject("live M04 cast-off handoff owner");
                var continuation = owner.AddComponent<ReturnOpeningContinuation>();
                var completion = typeof(ReturnOpeningContinuation).GetField(
                    "onFinished", BindingFlags.Instance | BindingFlags.NonPublic);
                var finishDock = typeof(ReturnOpeningContinuation).GetMethod(
                    "FinishDockPreflight", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(completion, Is.Not.Null);
                Assert.That(finishDock, Is.Not.Null);
                int deliveries = 0;
                completion.SetValue(continuation, new Action(() => deliveries++));
                finishDock.Invoke(continuation, null);

                Button castOff = owner.GetComponentsInChildren<Button>(true)
                    .Single(b => b.gameObject.name == "M04 cast off confirm");
                Assert.That(castOff.interactable, Is.True);
                Assert.That(deliveries, Is.Zero);
                Assert.That(vars.GetInt(M04DepartureFacts.SailedKey), Is.Zero,
                    "Opening a cast-off panel cannot automatically launch a ship.");

                castOff.onClick.Invoke();
                Assert.That(vars.GetInt(M04DepartureFacts.SailedKey), Is.EqualTo(1));
                Assert.That(deliveries, Is.EqualTo(1));
                castOff.onClick.Invoke();
                Assert.That(deliveries, Is.EqualTo(1));

                // Repeat the same handoff with no physical manifest. The
                // opening returns to dock control; it must not offer sailing.
                Assert.That(M04DepartureFacts.TryResetForNewVisit(write), Is.True);
                noShipOwner = new GameObject("live M04 no-hull handoff owner");
                var noShip = noShipOwner.AddComponent<ReturnOpeningContinuation>();
                int returnedToDock = 0;
                completion.SetValue(noShip, new Action(() => returnedToDock++));
                finishDock.Invoke(noShip, null);
                Assert.That(returnedToDock, Is.EqualTo(1));
                Assert.That(noShipOwner.GetComponentsInChildren<Button>(true)
                    .Any(b => b.gameObject.name == "M04 cast off confirm"), Is.False);
                Assert.That(vars.GetInt(M04DepartureFacts.SailedKey), Is.Zero);
            }
            finally
            {
                if (owner != null) UnityEngine.Object.Destroy(owner);
                if (noShipOwner != null) UnityEngine.Object.Destroy(noShipOwner);
            }
            yield return new ExitPlayMode();
        }
    }
}
