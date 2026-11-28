using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionCacheShortcutCanvasGuardE2ETests
    {
        [Test]
        public void FourRegion_CacheShortcutHud_RejectsInvisibleInputAndRestoresTracking()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);
            foreach (var spec in specs)
            {
                var root = new GameObject("CacheShortcutCanvasGuard_" + spec.MapId);
                try
                {
                    var group = root.AddComponent<CanvasGroup>();
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId, overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xCacheAndShortcutHudPanel(root.transform, boot))
                    {
                        Assert.IsTrue(panel.Show());
                        var canvas = root.transform.Find("World500xCacheAndShortcutHudPanel").GetComponent<Canvas>();
                        var modal = canvas.transform.Find("CacheShortcutModalBox");
                        var shortcuts = modal.Find("Tab_Shortcuts").GetComponent<Button>();
                        var caches = modal.Find("Tab_HiddenCaches").GetComponent<Button>();
                        Assert.IsTrue(panel.TrySelectTab(RpgWorld500xCacheShortcutTab.Shortcuts));
                        Assert.IsTrue(panel.TrySetIncompleteOnly(true));
                        Assert.Greater(panel.DisplayedRowCount, 0);
                        Assert.IsNotEmpty(panel.NearestIncompleteId);
                        int rows = panel.DisplayedRowCount;
                        string header = panel.HeaderText;
                        string summary = panel.SummaryText;
                        string target = panel.NearestIncompleteId;
                        string label = panel.NearestIncompleteLabelZh;
                        float distance = panel.NearestIncompleteDistanceTiles;
                        int remaining = panel.RemainingCacheSaltSilver;

                        void AssertInputRejected()
                        {
                            caches.onClick.Invoke();
                            shortcuts.onClick.Invoke();
                            Assert.IsFalse(panel.TrySelectTab(RpgWorld500xCacheShortcutTab.HiddenCaches));
                            Assert.IsFalse(panel.TrySetIncompleteOnly(false));
                            Assert.AreEqual(RpgWorld500xCacheShortcutTab.Shortcuts, panel.ActiveTab);
                            Assert.IsTrue(panel.IncompleteOnly);
                            Assert.AreEqual(rows, panel.DisplayedRowCount);
                            Assert.AreEqual(header, panel.HeaderText);
                            Assert.AreEqual(summary, panel.SummaryText);
                            Assert.AreEqual(target, panel.NearestIncompleteId);
                            Assert.AreEqual(label, panel.NearestIncompleteLabelZh);
                            Assert.AreEqual(distance, panel.NearestIncompleteDistanceTiles);
                            Assert.AreEqual(remaining, panel.RemainingCacheSaltSilver);
                        }

                        canvas.enabled = false;
                        Assert.IsTrue(caches.IsInteractable());
                        AssertInputRejected();
                        // Explicit controller refresh is not user input.
                        panel.Refresh();
                        AssertInputRejected();
                        canvas.enabled = true;

                        group.interactable = false;
                        AssertInputRejected();
                        group.interactable = true;

                        Assert.IsTrue(panel.Hide());
                        AssertInputRejected();
                        Assert.IsTrue(panel.Show());
                        caches.onClick.Invoke();
                        Assert.AreEqual(RpgWorld500xCacheShortcutTab.HiddenCaches, panel.ActiveTab);
                        Assert.IsTrue(panel.TrySetIncompleteOnly(false));
                        Assert.AreEqual(RpgWorld500xContentCatalog.GetCaches(spec.MapId).Count,
                            panel.DisplayedRowCount);
                        Assert.IsTrue(panel.TrySelectTab(RpgWorld500xCacheShortcutTab.Shortcuts));
                        Assert.AreEqual(RpgWorld500xContentCatalog.GetShortcuts(spec.MapId).Count,
                            panel.DisplayedRowCount);
                    }
                    Assert.AreEqual(0, root.transform.childCount, spec.MapId.ToString());
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }
    }
}
