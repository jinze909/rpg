using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class SoundwellLargeMapExplorationHostTests
    {
        private static void WalkUntil(SoundwellLargeMapExplorationHost host,
            Vector2 direction, Func<Vector2, bool> reached, int maxSteps = 200)
        {
            for (int i = 0; i < maxSteps && !reached(host.Explorer.Position); i++)
                host.Step(direction, 0.1f);
        }

        [Test]
        public void SoundwellHostBuildsSixChunksFourHalosVisualOnlyHudAndSwitchesLocale()
        {
            var root = new GameObject("Soundwell host init test");
            var camObj = new GameObject("Soundwell test camera");
            camObj.transform.position = new Vector3(-40f, -40f, -10f);
            string prevLang = SoundwellUiLocalization.Language;
            SoundwellLargeMapExplorationHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                host = new SoundwellLargeMapExplorationHost(root.transform);

                Assert.That(host.MapRenderer.ChunkCount, Is.EqualTo(6));
                Assert.That(host.MapRenderer.LightHaloCount, Is.EqualTo(4));
                Assert.That(host.MapRenderer.UsedRealArtSlices, Is.True);
                Assert.That(host.HeroVisual.SpriteRenderer.sprite, Is.Not.Null);
                Assert.That(host.BeastVisual.SpriteRenderer.sprite, Is.Not.Null);
                Assert.That(host.MinimapHud.BeaconCount, Is.EqualTo(4));
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(4));
                Assert.That(host.MinimapHud.IsThreatMarkerVisible, Is.True);
                Assert.That(root.GetComponentInChildren<GraphicRaycaster>(), Is.Null,
                    "Exploration, minimap, and lock-break HUD overlays must remain visual-only.");
                Assert.That(host.CurrentZoneId, Is.EqualTo("soundwell.entry"));
                Assert.That(host.ZoneLabel.text, Is.EqualTo("下井入口"));

                Vector2 initialMarker = host.MinimapHud.PlayerMarkerRect.anchoredPosition;
                host.Step(Vector2.right, 0.1f, camObj.transform);
                Assert.That(host.MinimapHud.PlayerMarkerRect.anchoredPosition.x,
                    Is.GreaterThan(initialMarker.x));
                Assert.That(host.HeroVisual.ActorObject.transform.localPosition.x,
                    Is.EqualTo(host.Explorer.Position.x).Within(0.01f));
                float halfW = RpgWorldMapScale.LogicalScreenTilesX * 0.5f;
                float halfH = RpgWorldMapScale.LogicalScreenTilesY * 0.5f;
                Assert.That(camObj.transform.position.x, Is.GreaterThanOrEqualTo(halfW));
                Assert.That(camObj.transform.position.y, Is.GreaterThanOrEqualTo(halfH));
                Assert.That(camObj.transform.position.z, Is.EqualTo(-10f));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(host.ZoneLabel.text, Is.EqualTo("Entry Shaft"));
            }
            finally
            {
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(prevLang);
                UnityEngine.Object.DestroyImmediate(camObj);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SoundwellHostEnforcesAcousticBreakBeforeIronGateOpensAndAllowsExtraction()
        {
            var root = new GameObject("Soundwell acoustic escape test");
            string prevLang = SoundwellUiLocalization.Language;
            SoundwellLargeMapExplorationHost host = null;
            var visitedZones = new List<string>();
            bool gateOpened = false;
            bool extracted = false;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                host = new SoundwellLargeMapExplorationHost(root.transform);
                host.ZoneChanged += z => visitedZones.Add(z);
                host.GateOpened += () => gateOpened = true;
                host.ExtractionReached += () => extracted = true;

                // 1. Walk to West Gate Lever (46, 11) before breaking acoustic locks.
                WalkUntil(host, Vector2.right, p => p.x >= 36f);
                WalkUntil(host, Vector2.down, p => p.y <= 11.5f);
                WalkUntil(host, Vector2.right, p => p.x >= 46f);

                Assert.That(host.TryInteract(), Is.False,
                    "Gate lever must refuse opening before both acoustic locks are broken.");
                Assert.That(host.IsGateOpen, Is.False);
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(4));

                // Closed iron gate at x=52 must physically block walking east.
                WalkUntil(host, Vector2.right, p => p.x >= 55f, maxSteps: 40);
                Assert.That(host.Explorer.Position.x, Is.LessThan(52f));

                // 2. Return to Central Hall and enter East Vault for Wet Cloth (80, 26) & Bell (80, 34).
                WalkUntil(host, Vector2.left, p => p.x <= 36.2f);
                WalkUntil(host, Vector2.up, p => p.y >= 29.8f);
                WalkUntil(host, Vector2.right, p => p.x >= 80f);
                WalkUntil(host, Vector2.down, p => p.y <= 26.5f);
                Assert.That(host.TryInteract(), Is.True);
                Assert.That(host.AcousticTracker.AcousticLocks.RemainingLocks, Is.EqualTo(1));
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(3));

                WalkUntil(host, Vector2.up, p => p.y >= 34f);
                Assert.That(host.TryInteract(), Is.True);
                Assert.That(host.AcousticTracker.IsBeastBroken, Is.True);
                Assert.That(host.AcousticTracker.CanTriggerNonViolentEscape, Is.True);
                Assert.That(host.LockBreakHud.ObjectiveLabel.text, Does.Contain("BREAK"));
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(2));

                // 3. Return to West Gate Lever (46, 11), open gate, and walk through to Extraction (78, 11).
                WalkUntil(host, Vector2.down, p => p.y <= 30.2f);
                WalkUntil(host, Vector2.left, p => p.x <= 36.2f);
                WalkUntil(host, Vector2.down, p => p.y <= 11.5f);
                WalkUntil(host, Vector2.right, p => p.x >= 46f);

                Assert.That(host.TryInteract(), Is.True);
                Assert.That(host.IsGateOpen, Is.True);
                Assert.That(gateOpened, Is.True);
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(1));

                WalkUntil(host, Vector2.right, p => p.x >= 78f);
                Assert.That(host.Explorer.Position.x, Is.GreaterThan(75f));
                Assert.That(host.TryInteract(), Is.True);
                Assert.That(host.ExtractionCompleted, Is.True);
                Assert.That(extracted, Is.True);
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.Zero);
                Assert.That(visitedZones, Does.Contain("soundwell.east-vault"));
                Assert.That(visitedZones, Does.Contain("soundwell.extraction"));
            }
            finally
            {
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(prevLang);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SoundwellHostWiresBeastPatrolBellLureAndBrokenNonViolentWindow()
        {
            var root = new GameObject("Soundwell beast patrol wiring test");
            SoundwellLargeMapExplorationHost host = null;
            try
            {
                host = new SoundwellLargeMapExplorationHost(root.transform);
                Assert.That(host.BeastPatrol, Is.Not.Null);
                Assert.That(host.MinimapHud.IsThreatMarkerVisible, Is.True);
                Vector2 initialBeastPos = host.BeastPatrol.Position;
                Vector2 initialThreatMarker = host.MinimapHud.ThreatMarkerRect.anchoredPosition;

                // Stepping the host advances the beast, its live world sprite, and its minimap marker.
                for (int i = 0; i < 10; i++)
                    host.Step(Vector2.zero, 0.1f);
                Assert.That(host.BeastPatrol.Position, Is.Not.EqualTo(initialBeastPos));
                Assert.That(host.BeastVisual.ActorObject.transform.localPosition.x,
                    Is.EqualTo(host.BeastPatrol.Position.x).Within(0.01f));
                Assert.That(host.MinimapHud.ThreatMarkerRect.anchoredPosition,
                    Is.Not.EqualTo(initialThreatMarker));

                // Walk to East Vault Bronze Bell (80, 34) first (without wet cloth) -> lures beast.
                WalkUntil(host, Vector2.right, p => p.x >= 80f);
                WalkUntil(host, Vector2.up, p => p.y >= 34f);
                Assert.That(host.TryInteract(), Is.True);
                Assert.That(host.BeastPatrol.State, Is.EqualTo(SoundwellBeastAlertState.InvestigatingBell));

                // Apply Wet Cloth (80, 26) -> breaks both acoustic locks -> beast enters StaggeredBroken.
                WalkUntil(host, Vector2.down, p => p.y <= 26.5f);
                Assert.That(host.TryInteract(), Is.True);
                host.Step(Vector2.zero, 0.1f);
                Assert.That(host.BeastPatrol.State, Is.EqualTo(SoundwellBeastAlertState.StaggeredBroken));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NullParentRejectedAndTeardownDisposesCleanly()
        {
            var root = new GameObject("Soundwell host teardown test");
            SoundwellLargeMapExplorationHost host = null;
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new SoundwellLargeMapExplorationHost(null));

                host = new SoundwellLargeMapExplorationHost(root.transform);
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => host.LayoutForViewportAspect(aspect));

                host.Dispose();
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.HeroVisual.IsDisposed, Is.True);
                Assert.That(host.BeastVisual.IsDisposed, Is.True);
                Assert.That(host.MapRenderer.IsDisposed, Is.True);
                Assert.That(host.LockBreakHud.IsDisposed, Is.True);
                Assert.That(host.MinimapHud.IsDisposed, Is.True);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
