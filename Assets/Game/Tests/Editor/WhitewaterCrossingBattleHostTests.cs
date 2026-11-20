using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class WhitewaterCrossingBattleHostTests
    {
        [Test]
        public void WhitewaterHostBuildsSixChunksFourBraziersVisualOnlyHudAndClampedCamera()
        {
            var root = new GameObject("Whitewater S00 host init test");
            string prevLang = SoundwellUiLocalization.Language;
            WhitewaterCrossingBattleHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                host = new WhitewaterCrossingBattleHost(root.transform);

                Assert.That(host.MapRenderer.ChunkCount, Is.EqualTo(6));
                Assert.That(host.MapRenderer.LightHaloCount, Is.EqualTo(4));
                Assert.That(host.MinimapHud.BeaconCount, Is.EqualTo(3));
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(3));
                Assert.That(root.GetComponentInChildren<GraphicRaycaster>(), Is.Null,
                    "S00 stage, minimap, and lock-break overlays must not block ORK battle clicks.");
                Assert.That(host.StageTitleLabel.text, Does.Contain("白水渡"));
                Assert.That(host.LockBreakHud.ObjectiveLabel.text, Does.Contain("0/3"));

                // Camera stays clamped inside 80x44 map bounds even from an out-of-bounds start.
                Vector3 cam = host.ComputeFramedCameraPosition(new Vector3(-80f, -80f, -10f), 10f);
                float halfW = RpgWorldMapScale.LogicalScreenTilesX * 0.5f;
                float halfH = RpgWorldMapScale.LogicalScreenTilesY * 0.5f;
                Assert.That(cam.x, Is.GreaterThanOrEqualTo(halfW));
                Assert.That(cam.x, Is.LessThanOrEqualTo(host.Layout.Width - halfW));
                Assert.That(cam.y, Is.GreaterThanOrEqualTo(halfH));
                Assert.That(cam.y, Is.LessThanOrEqualTo(host.Layout.Height - halfH));
                Assert.That(cam.z, Is.EqualTo(-10f));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(host.StageTitleLabel.text, Does.Contain("Whitewater Crossing"));
            }
            finally
            {
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(prevLang);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WhitewaterHostDrivesThreeBellSoloBridgeheadHoldAndStallsOnMissedSapper()
        {
            var root = new GameObject("Whitewater S00 3-bell loop test");
            string prevLang = SoundwellUiLocalization.Language;
            WhitewaterCrossingBattleHost host = null;
            var bells = new List<int>();
            int collapseCount = 0;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                host = new WhitewaterCrossingBattleHost(root.transform, startingMomentum: 1);
                host.BellAdvanced += b => bells.Add(b);
                host.ThirdBellCollapseReady += () => collapseCount++;

                // Bell 1: Shield Parry counters SpearThrustAtHero -> Bell 1/3, shake triggered.
                Assert.That(host.ResolveAuthoritativeCounter(CombatLockKind.ShieldParry), Is.True);
                Assert.That(host.CableTracker.BellsCompleted, Is.EqualTo(1));
                Assert.That(host.CableTracker.CurrentThreat, Is.EqualTo(S00BridgeThreatKind.SapperRushCable));
                Assert.That(host.ShakeRemaining, Is.GreaterThan(0f));
                host.ComputeFramedCameraPosition(new Vector3(24f, 22f, -10f), 0.3f);
                Assert.That(host.ShakeRemaining, Is.Zero);

                // Wrong counter on SapperRushCable stalls cut (stays 1/3) and keeps SapperRushCable active.
                Assert.That(host.ResolveAuthoritativeCounter(CombatLockKind.ShieldParry), Is.False);
                Assert.That(host.CableTracker.BellsCompleted, Is.EqualTo(1));
                Assert.That(host.CableTracker.CurrentThreat, Is.EqualTo(S00BridgeThreatKind.SapperRushCable));
                Assert.That(host.StageStatusLabel.text, Does.Contain("受阻"));
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(3));

                // Bell 2 (Repel the unresolved Sapper) + Bell 3 (Parry next Spear) -> ThirdBellCollapseReady fires once.
                Assert.That(host.ResolveAuthoritativeCounter(CombatLockKind.BladeRepel), Is.True);
                Assert.That(host.CableTracker.BellsCompleted, Is.EqualTo(2));
                Assert.That(host.CableTracker.CurrentThreat, Is.EqualTo(S00BridgeThreatKind.SpearThrustAtHero));
                Assert.That(host.ResolveAuthoritativeCounter(CombatLockKind.ShieldParry), Is.True);
                Assert.That(host.CableTracker.ReadyForThirdBellCollapse, Is.True);
                Assert.That(bells, Is.EqualTo(new[] { 1, 2, 3 }));
                Assert.That(collapseCount, Is.EqualTo(1));
                Assert.That(host.LockBreakHud.ObjectiveLabel.text, Does.Contain("3/3"));
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(1));
            }
            finally
            {
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(prevLang);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NullParentRejectedAndTeardownDisposesCleanly()
        {
            var root = new GameObject("Whitewater S00 teardown test");
            WhitewaterCrossingBattleHost host = null;
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new WhitewaterCrossingBattleHost(null));

                host = new WhitewaterCrossingBattleHost(root.transform);
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => host.LayoutForViewportAspect(aspect));

                host.Dispose();
                Assert.That(host.IsDisposed, Is.True);
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
