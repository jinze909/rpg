using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        public void OpeningContinuationRejectsTornFogBeforeOfferingRealCastOff()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            SeedChosenRoute(write, M03VoyageChoice.QiQueMerchant);
            SeedVerifiedDeparture(write, M03VoyageChoice.QiQueMerchant);
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read, out _), Is.True);
            string[] payloadKeys =
            {
                FogListeningFacts.SucceededKey,
                FogListeningFacts.HullIntegrityKey,
                FogListeningFacts.AvoidedHazardsKey,
                FogListeningFacts.MatchedSignalsKey,
                FogListeningFacts.RemainingFocusKey
            };
            foreach (string key in payloadKeys)
            {
                Assert.That(FogListeningFacts.TryResetForNewLeg(write), Is.True);
                values[key] = 1;
                Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read,
                    out M04DepartureRecord? rejected), Is.False, key);
                Assert.That(rejected.HasValue, Is.False);
                Assert.That(read(M04DepartureFacts.SailedKey), Is.Zero);
            }
            Assert.That(FogListeningFacts.TryResetForNewLeg(write), Is.True);
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read, out _), Is.True);
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
            GameObject failedOwner = null;
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
                Func<string, int> read = key => vars.GetInt(key);
                Action<string, int> write = (key, value) => vars.Set(key, value);
                Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write), Is.True);
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

                Assert.That(owner.GetComponentsInChildren<Button>(true)
                    .Any(b => b.gameObject.name == "Fog continue"), Is.False);

                castOff.onClick.Invoke();
                Assert.That(vars.GetInt(M04DepartureFacts.SailedKey), Is.EqualTo(1));
                Assert.That(vars.GetInt(FogListeningFacts.CompletedKey), Is.Zero);
                Assert.That(deliveries, Is.Zero, "Cast-off must open the actual fog leg.");
                castOff.onClick.Invoke();
                Assert.That(FindButton(owner, "Fog continue").interactable, Is.False);

                FindButton(owner, "Fog focus listen").onClick.Invoke();
                FindButton(owner, "Fog helm slow").onClick.Invoke();
                FindButton(owner, "Fog helm hold").onClick.Invoke();
                FindButton(owner, "Fog helm port").onClick.Invoke();
                FindButton(owner, "Fog helm signal").onClick.Invoke();
                Button finishFog = FindButton(owner, "Fog continue");
                Assert.That(finishFog.interactable, Is.True);
                finishFog.onClick.Invoke();
                M04ToM05SeaCrossingHost activeCrossing = continuation.ActiveSeaCrossing;
                Assert.That(activeCrossing, Is.Not.Null);
                Assert.That(activeCrossing.Stage,
                    Is.EqualTo(M04ToM05SeaCrossingStage.LandingObservation));
                Assert.That(deliveries, Is.Zero,
                    "A successful fog passage must continue to observed arrival and M05.");
                Assert.That(FogListeningFacts.TryRead(read, out FogListeningRecord? safe), Is.True);
                Assert.That(safe.HasValue && safe.Value.Succeeded, Is.True);
                Assert.That(safe.Value.AvoidedHazards, Is.EqualTo(2));
                Assert.That(safe.Value.MatchedSignals, Is.EqualTo(1));
                Assert.That(vars.GetInt(M05LandingFacts.ConfirmedKey), Is.Zero,
                    "Passing the reef alone does not record a harbor arrival.");

                M05LandingObservationView landing = activeCrossing.LandingObservationView;
                Assert.That(landing, Is.Not.Null);
                Assert.That(landing.CrewArrived, Is.Zero,
                    "The departure manifest is not copied into actual arrival crew.");
                Assert.That(landing.ReadyRescueBerths, Is.Zero);
                landing.CrewPlusButton.onClick.Invoke();
                landing.CrewPlusButton.onClick.Invoke();
                landing.TenderButton.onClick.Invoke();
                landing.ReceiverButton.onClick.Invoke();
                landing.BerthPlusButton.onClick.Invoke();
                landing.BerthPlusButton.onClick.Invoke();
                landing.BerthPlusButton.onClick.Invoke();
                Assert.That(landing.CanConfirm, Is.True);
                landing.ConfirmButton.onClick.Invoke();

                Assert.That(activeCrossing.Stage,
                    Is.EqualTo(M04ToM05SeaCrossingStage.OuterHarbor));
                Assert.That(activeCrossing.LandingObservationView, Is.Null);
                Assert.That(activeCrossing.OuterHarborHost.Stage,
                    Is.EqualTo(M05OuterHarborFlowStage.ArrivalSession));
                Assert.That(M05LandingFacts.TryRead(read,
                    out M05LandingReport? savedLanding), Is.True);
                Assert.That(savedLanding.HasValue, Is.True);
                Assert.That(savedLanding.Value.CrewArrived, Is.EqualTo(2));
                Assert.That(savedLanding.Value.ReadyRescueBerths, Is.EqualTo(3));
                Assert.That(savedLanding.Value.TenderArrived, Is.True);
                Assert.That(savedLanding.Value.ReceiverMet, Is.True);

                var harbor = activeCrossing.OuterHarborHost;
                harbor.ArrivalSession.SeranView.TakeButton.onClick.Invoke();
                harbor.ArrivalSession.SeranView.ContinueButton.onClick.Invoke();
                var harborWalk = harbor.ArrivalSession.HarborWalkView;
                harborWalk.AdvanceButton.onClick.Invoke();
                harborWalk.AdvanceButton.onClick.Invoke();
                Assert.That(harborWalk.Beat, Is.EqualTo(M05HarborBeat.MarketAccusation));
                harborWalk.AdvanceButton.onClick.Invoke();
                Assert.That(deliveries, Is.Zero,
                    "M05 must hand its verified outcome into the playable M06 mine camp, not end the opening.");
                Assert.That(continuation.ActiveSeaCrossing, Is.Null);
                Assert.That(vars.GetInt(M05LandingFacts.ConfirmedKey), Is.EqualTo(1));

                M06MineCampSceneHost activeMineCamp = continuation.ActiveMineCamp;
                Assert.That(activeMineCamp, Is.Not.Null);
                Assert.That(activeMineCamp.CameraRig, Is.Not.Null);
                Assert.That(activeMineCamp.CameraRig.VirtualCamera.Lens.OrthographicSize,
                    Is.EqualTo(5.625f).Within(0.001f));

                M06MineCampExplorationHost mineCamp = activeMineCamp.Exploration;
                Vector2 spawn = mineCamp.PlayerPosition;
                Assert.That(mineCamp.StepMovement(Vector2.right, 0.1f), Is.True);
                Assert.That(mineCamp.PlayerPosition.x, Is.GreaterThan(spawn.x));

                // Travel through the authored south-gate, checkpost, and pump-hall
                // corridors instead of teleporting the hero. This lets the live
                // Cinemachine follow camera track the same movement the player uses.
                int movementFrames = 0;
                while (mineCamp.PlayerPosition.x < 50f && movementFrames++ < 80)
                {
                    Assert.That(mineCamp.StepMovement(Vector2.right, 0.1f), Is.True);
                    yield return new WaitForSecondsRealtime(0.1f);
                }
                Assert.That(mineCamp.PlayerPosition.x, Is.GreaterThanOrEqualTo(50f));

                movementFrames = 0;
                while (mineCamp.PlayerPosition.y < 36f && movementFrames++ < 80)
                {
                    Assert.That(mineCamp.StepMovement(Vector2.up, 0.1f), Is.True);
                    yield return new WaitForSecondsRealtime(0.1f);
                }
                Assert.That(mineCamp.PlayerPosition.y, Is.GreaterThanOrEqualTo(36f));

                movementFrames = 0;
                while (mineCamp.PlayerPosition.x < 54f && movementFrames++ < 20)
                {
                    Assert.That(mineCamp.StepMovement(Vector2.right, 0.1f), Is.True);
                    yield return new WaitForSecondsRealtime(0.1f);
                }
                Assert.That(mineCamp.PlayerPosition.x, Is.GreaterThanOrEqualTo(54f));
                yield return null;
                yield return new WaitForEndOfFrame();
                CaptureM06GameView(owner.transform, "m06-mine-camp-map.png",
                    mineCamp.HeroVisual.ActorObject.transform.position);
                Assert.That(mineCamp.FindNearbyInteractableTriggerId(), Is.EqualTo("m06.pump-reunion"));
                Assert.That(mineCamp.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(mineCamp.ActiveReunionView, Is.Not.Null);
                yield return null;
                yield return new WaitForEndOfFrame();
                CaptureM06GameView(owner.transform, "m06-pump-room-reunion.png",
                    mineCamp.HeroVisual.ActorObject.transform.position);

                mineCamp.ActiveReunionView.GreetingBringBackButton.onClick.Invoke();
                mineCamp.ActiveReunionView.AdvanceButton.onClick.Invoke();
                mineCamp.ActiveReunionView.AdvanceButton.onClick.Invoke();
                mineCamp.ActiveReunionView.AdvanceButton.onClick.Invoke();
                Assert.That(mineCamp.ActiveReunionView.Session.Stage, Is.EqualTo(M06ReunionStage.ShenResponse));
                mineCamp.ActiveReunionView.ShenWaitButton.onClick.Invoke();
                mineCamp.ActiveReunionView.OfficialHandoverButton.onClick.Invoke();

                Assert.That(mineCamp.RescuePlanSettled, Is.True);
                Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? savedPlan), Is.True);
                Assert.That(savedPlan.HasValue, Is.True);
                Assert.That(savedPlan.Value.Plan, Is.EqualTo(M06RescuePlan.OfficialHandover));
                Assert.That(savedPlan.Value.LetterDelivery,
                    Is.EqualTo(M06LetterDeliveryOutcome.DeliveredWithPending));
                Assert.That(deliveries, Is.Zero);

                activeMineCamp.Exit();
                Assert.That(deliveries, Is.EqualTo(1));
                Assert.That(continuation.ActiveMineCamp, Is.Null);

                Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write), Is.True);
                SeedVerifiedDeparture(write, M03VoyageChoice.QiQueMerchant);
                failedOwner = new GameObject("live M04 failed fog passage");
                var failed = failedOwner.AddComponent<ReturnOpeningContinuation>();
                completion.SetValue(failed, new Action(() => deliveries++));
                finishDock.Invoke(failed, null);
                FindButton(failedOwner, "M04 cast off confirm").onClick.Invoke();
                FindButton(failedOwner, "Fog helm hold").onClick.Invoke();
                FindButton(failedOwner, "Fog helm hold").onClick.Invoke();
                FindButton(failedOwner, "Fog helm starboard").onClick.Invoke();
                FindButton(failedOwner, "Fog continue").onClick.Invoke();
                Assert.That(deliveries, Is.EqualTo(2));
                Assert.That(FogListeningFacts.TryRead(read, out FogListeningRecord? failedLeg), Is.True);
                Assert.That(failedLeg.HasValue && !failedLeg.Value.Succeeded, Is.True);
                Assert.That(failedLeg.Value.HullIntegrity, Is.Zero);
                Assert.That(vars.GetInt(M05LandingFacts.ConfirmedKey), Is.Zero);

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
                if (failedOwner != null) UnityEngine.Object.Destroy(failedOwner);
                if (noShipOwner != null) UnityEngine.Object.Destroy(noShipOwner);
            }
            yield return new ExitPlayMode();
        }

        private static void CaptureM06GameView(Transform owner, string filename, Vector3 heroPosition)
        {
            Assert.That(SystemInfo.graphicsDeviceType,
                Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null),
                "M06 GameView evidence requires a graphics-capable Unity runner.");

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.orthographic, Is.True);

            Canvas[] canvases = owner.GetComponentsInChildren<Canvas>(true)
                .Where(canvas => canvas != null && canvas.isActiveAndEnabled &&
                    canvas.gameObject.name.StartsWith("M06 ", StringComparison.Ordinal))
                .ToArray();
            Assert.That(canvases.Any(canvas =>
                canvas.gameObject.name == "M06 mine camp HUD canvas"), Is.True);
            if (filename.Contains("reunion"))
            {
                Assert.That(canvases.Any(canvas =>
                    canvas.gameObject.name == "M06 pump room reunion canvas"), Is.True);
            }

            var rig = camera.GetComponent<SoundwellCameraRig>();
            Assert.That(rig, Is.Not.Null);
            Assert.That(rig.VirtualCamera, Is.Not.Null);

            RenderMode[] previousModes = canvases.Select(canvas => canvas.renderMode).ToArray();
            Camera[] previousWorldCameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
            float[] previousPlaneDistances = canvases.Select(canvas => canvas.planeDistance).ToArray();
            int previousCullingMask = camera.cullingMask;
            float previousOrthographicSize = camera.orthographicSize;
            float previousAspect = camera.aspect;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
            Texture2D frame = null;

            try
            {
                camera.targetTexture = target;
                camera.aspect = 960f / 540f;
                camera.orthographicSize = rig.VirtualCamera.Lens.OrthographicSize;

                camera.Render();
                RenderTexture.active = target;
                frame = new Texture2D(960, 540, TextureFormat.RGB24, false);
                frame.ReadPixels(new Rect(0, 0, 960, 540), 0, 0, false);
                frame.Apply(false, false);
                Color32[] worldPixels = frame.GetPixels32();

                for (int index = 0; index < canvases.Length; index++)
                {
                    Canvas canvas = canvases[index];
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1f;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    camera.cullingMask |= 1 << canvas.gameObject.layer;
                }

                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, 960, 540), 0, 0, false);
                frame.Apply(false, false);
                Color32[] finalPixels = frame.GetPixels32();

                int changedSamples = 0;
                var sampledColors = new HashSet<int>();
                for (int index = 0; index < finalPixels.Length; index += 3)
                {
                    Color32 before = worldPixels[index];
                    Color32 after = finalPixels[index];
                    int difference = Math.Abs((int)before.r - after.r) +
                        Math.Abs((int)before.g - after.g) + Math.Abs((int)before.b - after.b);
                    if (difference > 24) changedSamples++;
                    if (index % 127 == 0)
                    {
                        sampledColors.Add((after.r << 24) | (after.g << 16) |
                            (after.b << 8) | after.a);
                    }
                }

                Assert.That(changedSamples, Is.GreaterThan(100),
                    "M06 GameView capture must composite the live UGUI canvas over the mine-camp world.");
                Assert.That(sampledColors.Count, Is.GreaterThan(6),
                    "M06 GameView capture must retain the scene's pixel-art color detail.");

                Vector3 viewport = camera.WorldToViewportPoint(heroPosition);
                Assert.That(viewport.z, Is.GreaterThan(0f),
                    $"M06 hero must be in front of the live camera. Hero={heroPosition}, camera={camera.transform.position}.");
                Assert.That(viewport.x, Is.InRange(0.02f, 0.98f),
                    $"M06 hero must remain horizontally visible. Viewport={viewport}, camera={camera.transform.position}, target={rig.FramingTarget.position}.");
                Assert.That(viewport.y, Is.InRange(0.02f, 0.98f),
                    $"M06 hero must remain vertically visible. Viewport={viewport}, camera={camera.transform.position}, target={rig.FramingTarget.position}, lens={camera.orthographicSize}.");

                string directory = Path.Combine("artifacts", "p0", "story");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, filename);
                File.WriteAllBytes(path, frame.EncodeToPNG());
                Assert.That(new FileInfo(path).Length, Is.GreaterThan(1024));
            }
            finally
            {
                for (int index = 0; index < canvases.Length; index++)
                {
                    Canvas canvas = canvases[index];
                    if (canvas == null) continue;
                    canvas.renderMode = previousModes[index];
                    canvas.worldCamera = previousWorldCameras[index];
                    canvas.planeDistance = previousPlaneDistances[index];
                }

                camera.cullingMask = previousCullingMask;
                camera.orthographicSize = previousOrthographicSize;
                camera.aspect = previousAspect;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Canvas.ForceUpdateCanvases();
                RenderTexture.ReleaseTemporary(target);
                if (frame != null) UnityEngine.Object.Destroy(frame);
            }
        }

        private static Button FindButton(GameObject owner, string name)
        {
            return owner.GetComponentsInChildren<Button>(true)
                .Single(b => b.gameObject.name == name);
        }
    }
}
