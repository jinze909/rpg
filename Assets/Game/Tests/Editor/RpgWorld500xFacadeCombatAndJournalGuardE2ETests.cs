using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFacadeCombatAndJournalGuardE2ETests
    {
        [Test]
        public void GrandRuntimeFacade_GuardsUnifiedInteractionsDuringCombatAndOpenJournalThenResumesWithZeroLeak()
        {
            var root = new GameObject("World500xFacadeCombatJournalGuardE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmup = new RpgWorld500xGrandRuntimeFacade(
                    root.transform, RpgWorldMapId.Soundwell,
                    streamingRadiusChunks: 1,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    warmup.Session.JournalPanel.Show();
                    warmup.Session.JournalPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                var toasts = new List<string>();
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                Action<string, int> write = (k, v) => store[k] = v;

                using (var facade = new RpgWorld500xGrandRuntimeFacade(
                    root.transform, RpgWorldMapId.Soundwell,
                    getInt: read,
                    setInt: write,
                    onToast: (title, body) => toasts.Add(title + ":" + body),
                    streamingRadiusChunks: 1,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    Assert.IsTrue(facade.Session.Host.TryPlaceAtWalkableTile(new Vector2Int(680, 640)));
                    Assert.IsTrue(facade.Session.Host.TryTriggerNearbyDangerEncounter(out string markerId));
                    Assert.AreEqual("sw.sunken-drain", markerId);
                    Assert.IsTrue(facade.Session.Host.Bridge.IsInDangerEncounter);

                    int interactionsBefore = facade.ActivityDirector.InteractionCount;
                    int toastsBefore = toasts.Count;

                    // Active combat blocks unified world interactions.
                    Assert.IsFalse(facade.TryInteractUnified(100000f, out var blockedCombat, out var blockedOutcome));
                    Assert.AreEqual(RpgWorld500xInteractionKind.None, blockedCombat.Kind);
                    Assert.AreEqual(RpgWorldInteractOutcome.None, blockedOutcome);
                    Assert.AreEqual(interactionsBefore, facade.ActivityDirector.InteractionCount);
                    Assert.AreEqual(toastsBefore, toasts.Count);

                    // Resolve combat victory -> danger cleared in global ledger.
                    Assert.IsTrue(facade.Session.ResolveActiveDangerVictory(true, out _));
                    Assert.IsFalse(facade.Session.Host.Bridge.IsInDangerEncounter);
                    RpgWorld500xGlobalProgress progress = RpgWorld500xLedgerSummary.Capture(read);
                    Assert.GreaterOrEqual(progress.ClearedDangerZones, 1);

                    // Open quest journal -> still blocks unified world interactions while visible.
                    Assert.IsTrue(facade.Session.JournalPanel.Show());
                    Assert.IsTrue(facade.Session.JournalPanel.IsVisible);
                    Assert.IsFalse(facade.TryInteractUnified(100000f, out var blockedJournal, out _));
                    Assert.AreEqual(RpgWorld500xInteractionKind.None, blockedJournal.Kind);
                    Assert.AreEqual(interactionsBefore, facade.ActivityDirector.InteractionCount);

                    // Close journal -> unified world interaction resumes cleanly.
                    Assert.IsTrue(facade.Session.JournalPanel.Hide());
                    Assert.IsFalse(facade.Session.JournalPanel.IsVisible);
                    Assert.IsTrue(facade.TryInteractUnified(100000f, out var resumed, out _));
                    Assert.IsTrue(resumed.Succeeded);
                    Assert.AreEqual(interactionsBefore + 1, facade.ActivityDirector.InteractionCount);
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
