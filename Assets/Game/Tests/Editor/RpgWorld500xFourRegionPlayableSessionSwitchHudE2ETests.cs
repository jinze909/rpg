using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionPlayableSessionSwitchHudE2ETests
    {
        [Test]
        public void AllFourRegions_PlayableSession_SwitchesCleanlyPreservesSameMapAndGuardsJournalModal()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var root = new GameObject("FourRegionPlayableSessionSwitchHudRoot");
            RpgWorld500xPlayableSession session = null;
            try
            {
                session = new RpgWorld500xPlayableSession(
                    root.transform,
                    specs[0].MapId,
                    getInt: k => store.TryGetValue(k, out int v) ? v : 0,
                    setInt: (k, v) => store[k] = v,
                    streamingRadiusChunks: 1,
                    overrideTileSlices: new Sprite[] { null });

                foreach (RpgExpandedMapSpec spec in specs)
                {
                    Assert.IsTrue(session.SwitchMap(spec.MapId), $"SwitchMap to {spec.MapId} should succeed.");
                    Assert.AreEqual(spec.MapId, session.CurrentMapId);
                    Assert.LessOrEqual(session.Host.ChunkRenderer.ChunkCount, 9);

                    var liveHost = session.Host;
                    var liveJournal = session.JournalPanel;
                    int retainedChildren = root.transform.childCount;

                    Assert.IsFalse(session.SwitchMap((RpgWorldMapId)999), $"Invalid map id must be rejected on {spec.MapId}.");
                    Assert.AreSame(liveHost, session.Host);
                    Assert.AreSame(liveJournal, session.JournalPanel);
                    Assert.AreEqual(retainedChildren, root.transform.childCount);

                    Assert.IsTrue(session.SwitchMap(spec.MapId), $"Same-map switch should be a no-op returning true on {spec.MapId}.");
                    Assert.AreSame(liveHost, session.Host);
                    Assert.AreSame(liveJournal, session.JournalPanel);
                    Assert.AreEqual(retainedChildren, root.transform.childCount);

                    Assert.IsTrue(session.ToggleOverviewMap());
                    Assert.IsTrue(session.MinimapHud.IsOverviewMode);
                    Assert.IsTrue(session.ToggleOverviewMap());
                    Assert.IsFalse(session.MinimapHud.IsOverviewMode);

                    Assert.IsTrue(session.ToggleJournal(RpgQuestJournalTab.ActiveQuests));
                    Assert.IsTrue(session.JournalPanel.IsVisible);
                    Assert.IsFalse(session.StepMovement(Vector2.right, 0.2f), $"Movement must be blocked while journal is visible in {spec.MapId}.");
                    Assert.AreEqual(RpgWorldInteractOutcome.None, session.InteractNearby(), $"Interact must be blocked while journal is visible in {spec.MapId}.");
                    Assert.IsTrue(session.ToggleJournal());
                    Assert.IsFalse(session.JournalPanel.IsVisible);
                }

                session.Dispose();
                Assert.IsTrue(session.IsDisposed);
                Assert.AreEqual(0, root.transform.childCount, "Disposing PlayableSession must leave zero child GameObjects.");
            }
            finally
            {
                session?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AllFourRegions_PlayableSession_PostDisposeGuardsRejectAllOperations()
        {
            var root = new GameObject("FourRegionPlayableSessionDisposedGuardRoot");
            RpgWorld500xPlayableSession session = null;
            try
            {
                session = new RpgWorld500xPlayableSession(
                    root.transform,
                    RpgWorldMapId.RedSaltHarbor,
                    streamingRadiusChunks: 1,
                    overrideTileSlices: new Sprite[] { null });

                session.Dispose();
                Assert.IsTrue(session.IsDisposed);
                Assert.AreEqual(0, root.transform.childCount);

                Assert.IsFalse(session.SwitchMap(RpgWorldMapId.Soundwell));
                Assert.IsFalse(session.StepMovement(Vector2.right, 0.2f));
                Assert.IsFalse(session.ToggleOverviewMap());
                Assert.IsFalse(session.ToggleJournal());
                Assert.AreEqual(RpgWorldInteractOutcome.None, session.InteractNearby());
            }
            finally
            {
                session?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
