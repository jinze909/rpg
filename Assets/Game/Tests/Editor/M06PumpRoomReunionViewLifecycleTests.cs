using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M06PumpRoomReunionViewLifecycleTests
    {
        private static M06PumpRoomReunionSession CreateSession()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = key => values.TryGetValue(key, out int v) ? v : 0;
            Action<string, int> write = (key, value) => values[key] = value;

            var manifest = new M04DepartureManifest(M03VoyageChoice.QiQueMerchant,
                "Verified civilian hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 10, true, true), read, write), Is.True);
            Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            return session;
        }

        [Test]
        public void LayoutForViewportAspectIgnoresNonFiniteAndNonPositiveAspects()
        {
            var root = new GameObject("M06 reunion view invalid aspect");
            M06PumpRoomReunionView view = null;
            try
            {
                M06PumpRoomReunionSession session = CreateSession();
                view = new M06PumpRoomReunionView(root.transform, session, _ => { });
                Vector3 initialScale = view.Panel.localScale;

                foreach (float aspect in new[]
                    { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f, -1.5f })
                {
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));
                    Assert.That(view.Panel.localScale, Is.EqualTo(initialScale));
                    Assert.That(view.IsDisposed, Is.False);
                }
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PartialCanvasDestructionDisposesOnChoiceClickWithoutAdvancingSession()
        {
            var root = new GameObject("M06 reunion view partial choice click");
            M06PumpRoomReunionView view = null;
            try
            {
                M06PumpRoomReunionSession session = CreateSession();
                view = new M06PumpRoomReunionView(root.transform, session, _ => { });
                Button greetingBtn = view.GreetingBringBackButton;

                UnityEngine.Object.DestroyImmediate(view.Heading);
                Assert.DoesNotThrow(() => greetingBtn.onClick.Invoke());
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.VesselBriefing));
                Assert.That(view.IsDisposed, Is.True);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PartialCanvasDestructionDisposesOnAdvanceClickWithoutAdvancingSession()
        {
            var root = new GameObject("M06 reunion view partial advance click");
            M06PumpRoomReunionView view = null;
            try
            {
                M06PumpRoomReunionSession session = CreateSession();
                view = new M06PumpRoomReunionView(root.transform, session, _ => { });
                view.GreetingBringBackButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.PumpRoomWitnesses));
                Button advanceBtn = view.AdvanceButton;

                UnityEngine.Object.DestroyImmediate(view.Body);
                Assert.DoesNotThrow(() => advanceBtn.onClick.Invoke());
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.PumpRoomWitnesses));
                Assert.That(view.IsDisposed, Is.True);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
