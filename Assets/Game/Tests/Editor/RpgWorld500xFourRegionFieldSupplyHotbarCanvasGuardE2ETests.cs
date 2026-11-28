using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionFieldSupplyHotbarCanvasGuardE2ETests
    {
        [Test]
        public void AllFourRegions_FieldSupplyHotbarRejectsDisabledCanvasLockedAndInactiveInputUntilRestored()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var root = new GameObject("FieldSupplyHotbarCanvasGuard_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var hud = new RpgWorld500xFieldSupplyHotbarHud(
                        root.transform,
                        boot,
                        clockSecondsProvider: () => 20f))
                    {
                        var shop = boot.Driver.Facade.BountyAndSupply;
                        shop.AddSaltSilver(200);
                        Assert.AreEqual(
                            RpgCampSupplyPurchaseStatus.Succeeded,
                            shop.TryPurchaseSupply("lamp_oil", 2),
                            spec.MapId.ToString());
                        hud.Refresh(10f);

                        var canvas = root.transform
                            .Find("World500xFieldSupplyHotbarHud")
                            .GetComponent<Canvas>();
                        Assert.IsNotNull(canvas, spec.MapId.ToString());
                        var button = canvas.transform
                            .Find("SupplyHotbarPanel/SupplySlot_lamp_oil")
                            .GetComponent<Button>();
                        Assert.IsNotNull(button, spec.MapId.ToString());
                        Assert.IsTrue(button.IsInteractable(), spec.MapId.ToString());

                        string feedback = hud.FeedbackText;
                        string buffs = hud.ActiveBuffsText;

                        // 1. Disabled owning Canvas rejects retained UnityEvent and public consumption.
                        canvas.enabled = false;
                        Assert.IsTrue(button.IsInteractable(), spec.MapId.ToString());
                        AssertBlocked(hud, shop, button, feedback, buffs);
                        canvas.enabled = true;

                        // 2. Ancestor CanvasGroup lock rejects retained UnityEvent and public consumption.
                        var group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;
                        AssertBlocked(hud, shop, button, feedback, buffs);
                        group.interactable = true;

                        // 3. Inactive hotbar root rejects retained UnityEvent and public consumption.
                        canvas.gameObject.SetActive(false);
                        AssertBlocked(hud, shop, button, feedback, buffs);
                        canvas.gameObject.SetActive(true);

                        // 4. Restored hotbar accepts both public API and raw button consumption.
                        Assert.IsTrue(hud.TryClickUseSupply("lamp_oil", 10f), spec.MapId.ToString());
                        Assert.AreEqual(1, shop.GetOwnedSupplyCount("lamp_oil"), spec.MapId.ToString());
                        Assert.AreEqual(
                            90f,
                            hud.SupplyUseService.GetRemainingBuffSeconds("lamp_oil", 10f),
                            0.01f,
                            spec.MapId.ToString());

                        button.onClick.Invoke();
                        Assert.AreEqual(0, shop.GetOwnedSupplyCount("lamp_oil"), spec.MapId.ToString());
                        Assert.AreEqual(
                            170f,
                            hud.SupplyUseService.GetRemainingBuffSeconds("lamp_oil", 20f),
                            0.01f,
                            spec.MapId.ToString());

                        Assert.IsFalse(hud.TryClickUseSupply("lamp_oil", 20f), spec.MapId.ToString());
                    }

                    Assert.AreEqual(0, root.transform.childCount, spec.MapId.ToString());
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void AssertBlocked(
            RpgWorld500xFieldSupplyHotbarHud hud,
            RpgWorld500xFieldBountyAndSupplyService shop,
            Button button,
            string expectedFeedback,
            string expectedBuffs)
        {
            button.onClick.Invoke();
            Assert.IsFalse(hud.TryClickUseSupply("lamp_oil", 10f));

            Assert.AreEqual(2, shop.GetOwnedSupplyCount("lamp_oil"));
            Assert.AreEqual(2, hud.TotalOwnedSupplyCount);
            Assert.AreEqual(0, hud.ActiveBuffCount);
            Assert.AreEqual(0f, hud.SupplyUseService.GetRemainingBuffSeconds("lamp_oil", 20f));
            Assert.AreEqual(expectedFeedback, hud.FeedbackText);
            Assert.AreEqual(expectedBuffs, hud.ActiveBuffsText);
        }
    }
}
