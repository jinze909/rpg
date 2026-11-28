using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionSaveSlotCanvasGuardE2ETests
    {
        [Test]
        public void AllFourRegions_SaveSlotHudPanel_DisabledCanvasRejectsSaveLoadDeleteUntilRestored()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            const RpgWorld500xSaveSlotId slot = RpgWorld500xSaveSlotId.Slot1;
            string key = RpgWorld500xSaveSlotManager.GetStorageKey(slot);

            for (int i = 0; i < specs.Count; i++)
            {
                RpgWorldMapId startMap = specs[i].MapId;
                RpgWorldMapId otherMap = specs[(i + 1) % specs.Count].MapId;
                var root = new GameObject("FourRegionSaveSlotDisabledCanvas_" + startMap);
                try
                {
                    var storage = new RpgWorld500xInMemorySaveSlotStorage();
                    var manager = new RpgWorld500xSaveSlotManager(storage);

                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: startMap,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xSaveSlotHudPanel(root.transform, boot, manager))
                    {
                        boot.Driver.Facade.BountyAndSupply.AddSaltSilver(40);
                        Assert.IsTrue(panel.Show());
                        Assert.IsTrue(panel.TryClickSave(slot));
                        Assert.IsTrue(storage.TryRead(key, out string original));

                        Assert.IsTrue(boot.SwitchMap(otherMap));
                        boot.Driver.Facade.BountyAndSupply.AddSaltSilver(60);

                        var canvas = root.transform.Find("World500xSaveSlotHudPanel").GetComponent<Canvas>();
                        var row = canvas.transform.Find("SaveSlotModalBox/SaveSlotRows/SaveSlotRow_Slot1");
                        var save = row.Find("SaveButton").GetComponent<Button>();
                        var load = row.Find("LoadButton").GetComponent<Button>();
                        var delete = row.Find("DeleteButton").GetComponent<Button>();
                        string statusBefore = panel.StatusText;

                        canvas.enabled = false;
                        Assert.IsFalse(panel.TryClickSave(slot));
                        Assert.IsFalse(panel.TryClickLoad(slot));
                        Assert.IsFalse(panel.TryClickDelete(slot));
                        save.onClick.Invoke();
                        load.onClick.Invoke();
                        delete.onClick.Invoke();

                        Assert.IsTrue(storage.TryRead(key, out string unchanged));
                        Assert.AreEqual(original, unchanged);
                        Assert.AreEqual(otherMap, boot.Driver.Facade.CurrentMapId);
                        Assert.AreEqual(100, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                        Assert.AreEqual(statusBefore, panel.StatusText);

                        canvas.enabled = true;
                        Assert.IsTrue(panel.TryClickLoad(slot));
                        Assert.AreEqual(startMap, boot.Driver.Facade.CurrentMapId);
                        Assert.AreEqual(40, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                        Assert.IsTrue(panel.TryClickDelete(slot));
                        Assert.IsFalse(storage.TryRead(key, out _));
                        Assert.IsTrue(panel.TryClickSave(slot));
                        Assert.IsTrue(storage.TryRead(key, out _));
                    }

                    Assert.AreEqual(0, root.transform.childCount);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void AllFourRegions_SaveSlotHudPanel_HiddenAndCanvasGroupLockRejectMutationsWithZeroLeak()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            const RpgWorld500xSaveSlotId slot = RpgWorld500xSaveSlotId.Slot1;
            string key = RpgWorld500xSaveSlotManager.GetStorageKey(slot);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var root = new GameObject("FourRegionSaveSlotCanvasGroupLock_" + spec.MapId);
                try
                {
                    var storage = new RpgWorld500xInMemorySaveSlotStorage();
                    var manager = new RpgWorld500xSaveSlotManager(storage);

                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xSaveSlotHudPanel(root.transform, boot, manager))
                    {
                        Assert.IsFalse(panel.TryClickSave(slot));
                        Assert.IsFalse(panel.TryClickLoad(slot));
                        Assert.IsFalse(panel.TryClickDelete(slot));
                        Assert.IsFalse(storage.TryRead(key, out _));

                        Assert.IsTrue(panel.Show());
                        Assert.IsTrue(panel.TryClickSave(slot));
                        Assert.IsTrue(storage.TryRead(key, out string savedPayload));

                        CanvasGroup group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;
                        Assert.IsFalse(panel.TryClickSave(slot));
                        Assert.IsFalse(panel.TryClickLoad(slot));
                        Assert.IsFalse(panel.TryClickDelete(slot));
                        Assert.IsTrue(storage.TryRead(key, out string afterLock));
                        Assert.AreEqual(savedPayload, afterLock);
                    }

                    Assert.AreEqual(0, root.transform.childCount);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }
    }
}
