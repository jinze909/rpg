using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionGrandFacadeSwitchAndCompletionE2ETests
    {
        [Test]
        public void FourRegion_GrandRuntimeFacade_SameMapAndInvalidSwitchPreserveSubsystemsAcrossAllFourRegionsWithZeroSpriteLeak()
        {
            var root = new GameObject("World500xFourRegionGrandFacadeSwitchRoot");
            RpgWorld500xGrandRuntimeFacade facade = null;
            try
            {
                IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                Assert.AreEqual(4, specs.Count);

                // Warm up one-time UGUI/font and per-region caches before steady-state leak baseline.
                using (var warmupFacade = new RpgWorld500xGrandRuntimeFacade(
                    root.transform,
                    specs[0].MapId,
                    streamingRadiusChunks: 1,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    for (int w = 1; w < specs.Count; w++)
                    {
                        Assert.IsTrue(warmupFacade.SwitchMap(specs[w].MapId));
                    }
                }

                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var store = new Dictionary<string, int>(System.StringComparer.Ordinal);
                facade = new RpgWorld500xGrandRuntimeFacade(
                    root.transform,
                    specs[0].MapId,
                    getInt: k => store.TryGetValue(k, out int v) ? v : 0,
                    setInt: (k, v) => store[k] = v,
                    streamingRadiusChunks: 1,
                    overrideTileSlices: new Sprite[] { null });

                facade.BountyAndSupply.AddSaltSilver(85);
                int expectedSilver = facade.BountyAndSupply.SaltSilverBalance;

                for (int i = 0; i < specs.Count; i++)
                {
                    RpgWorldMapId region = specs[i].MapId;
                    if (facade.CurrentMapId != region)
                    {
                        Assert.IsTrue(facade.SwitchMap(region), $"Failed to switch facade to {region}.");
                    }

                    Assert.AreEqual(region, facade.CurrentMapId);
                    Assert.AreEqual(region, facade.Session.Host.Layout.Spec.MapId);
                    Assert.AreEqual(expectedSilver, facade.BountyAndSupply.SaltSilverBalance);

                    var hostBefore = facade.Session.Host;
                    var activityBefore = facade.ActivityDirector;
                    var propsBefore = facade.PropSpawner;
                    var districtBefore = facade.DistrictTracker;
                    Vector2 posBefore = facade.Session.Host.PlayerPosition;

                    // Invalid map ID must fail closed without tearing down per-map subsystems.
                    Assert.IsFalse(facade.SwitchMap((RpgWorldMapId)9999));
                    // Same-map switch is a successful no-op preserving all mounted subsystems.
                    Assert.IsTrue(facade.SwitchMap(region));

                    Assert.AreSame(hostBefore, facade.Session.Host);
                    Assert.AreSame(activityBefore, facade.ActivityDirector);
                    Assert.AreSame(propsBefore, facade.PropSpawner);
                    Assert.AreSame(districtBefore, facade.DistrictTracker);
                    Assert.AreEqual(posBefore, facade.Session.Host.PlayerPosition);

                    Assert.DoesNotThrow(() => facade.StepFrame(Vector2.zero, 0.2f));
                    RpgWorld500xMapCompletionSummary summary = facade.GetCompletionSummary();
                    Assert.Greater(summary.TotalChunks, 0);
                    Assert.Greater(summary.TotalMarkers, 0);
                    Assert.GreaterOrEqual(summary.OverallCompletionPercent, 0);
                    Assert.LessOrEqual(summary.OverallCompletionPercent, 100);
                }

                facade.Dispose();
                Assert.IsTrue(facade.IsDisposed);
                Assert.IsFalse(facade.SwitchMap(RpgWorldMapId.Soundwell));
                Assert.IsFalse(facade.TryInteractUnified(100000f, out _, out _));
                facade = null;

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                facade?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
