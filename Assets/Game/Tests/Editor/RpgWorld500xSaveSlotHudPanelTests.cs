using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xSaveSlotHudPanelTests
    {
        [Test]
        public void SaveSlotHudPanel_SavesLoadsDeletesIsolatesCorruptAndDisposesWithZeroLeak()
        {
            var root = new GameObject("World500xSaveSlotHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xSaveSlotHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var storage = new RpgWorld500xInMemorySaveSlotStorage();
                DateTime clock = new DateTime(2026, 11, 23, 15, 30, 0, DateTimeKind.Utc);
                var manager = new RpgWorld500xSaveSlotManager(storage, () => clock);

                // Seed a corrupted payload into Slot3 to verify UI disables Load but allows Delete/Overwrite.
                storage.Write(RpgWorld500xSaveSlotManager.GetStorageKey(RpgWorld500xSaveSlotId.Slot3), "{corrupt");

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xSaveSlotHudPanel(root.transform, boot, manager))
                {
                    Assert.IsTrue(panel.Show());
                    Assert.AreEqual(4, panel.DisplayedSlotRowCount);
                    Assert.IsFalse(panel.TryClickLoad(RpgWorld500xSaveSlotId.Slot1));
                    Assert.IsFalse(panel.TryClickLoad(RpgWorld500xSaveSlotId.Slot3));

                    // Save Soundwell state with 110 Salt-Silver to Slot1.
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(110);
                    Assert.IsTrue(panel.TryClickSave(RpgWorld500xSaveSlotId.Slot1));
                    Assert.IsTrue(panel.StatusText.Contains("已保存至"));

                    // Mutate state: switch to RedSaltHarbor and add more Salt-Silver.
                    Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(50);
                    Assert.AreEqual(160, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                    // Load Slot1 via UI -> returns to Soundwell with 110 Salt-Silver.
                    Assert.IsTrue(panel.TryClickLoad(RpgWorld500xSaveSlotId.Slot1));
                    Assert.AreEqual(RpgWorldMapId.Soundwell, boot.Driver.Facade.CurrentMapId);
                    Assert.AreEqual(110, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                    Assert.IsTrue(panel.StatusText.Contains("已读取"));

                    // Delete corrupted Slot3 cleanly via UI.
                    Assert.IsTrue(panel.TryClickDelete(RpgWorld500xSaveSlotId.Slot3));
                    Assert.IsFalse(panel.TryClickDelete(RpgWorld500xSaveSlotId.Slot3));

                    Assert.IsTrue(panel.Hide());
                    Assert.IsFalse(panel.IsVisible);
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
