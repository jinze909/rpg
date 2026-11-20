using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class CombatLockBreakHudViewTests
    {
        [Test]
        public void S00HudReflectsLiveCableProgressLocksMomentumAndLocaleSwitches()
        {
            var root = new GameObject("S00 lock-break HUD test");
            var tracker = new S00BridgeheadCableTracker(startingMomentum: 1);
            string prevLang = SoundwellUiLocalization.Language;
            CombatLockBreakHudView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                view = new CombatLockBreakHudView(root.transform, tracker);

                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(1f));
                Assert.That(view.Panel.sizeDelta.y, Is.LessThan(70f));
                Assert.That(root.GetComponentInChildren<GraphicRaycaster>(), Is.Null,
                    "Visual-only HUD must not intercept ORK battle clicks.");
                Assert.That(view.ObjectiveLabel.text, Does.Contain("0/3"));
                Assert.That(view.LocksLabel.text, Does.Contain("举盾格挡"));
                Assert.That(view.MomentumLabel.text, Does.Contain("1/3"));

                Assert.That(tracker.ResolveHeroCounter(CombatLockKind.ShieldParry), Is.True);
                view.Refresh();
                Assert.That(view.ObjectiveLabel.text, Does.Contain("1/3"));
                Assert.That(view.LocksLabel.text, Does.Contain("挥刀逼退"));
                Assert.That(view.MomentumLabel.text, Does.Contain("2/3"));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(view.ObjectiveLabel.text, Does.Contain("Cable Cut"));
                Assert.That(view.LocksLabel.text, Does.Contain("Blade Repel"));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(prevLang);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SoundwellHudShowsPartialMitigationAndBrokenEscapeWindow()
        {
            var root = new GameObject("Soundwell lock-break HUD test");
            var tracker = new SoundwellAcousticBreakTracker(startingMomentum: 1);
            string prevLang = SoundwellUiLocalization.Language;
            CombatLockBreakHudView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                view = new CombatLockBreakHudView(root.transform, tracker);
                Assert.That(view.ObjectiveLabel.text, Does.Contain("2/2"));
                Assert.That(view.LocksLabel.text, Does.Contain("湿布消音"));
                Assert.That(view.LocksLabel.text, Does.Contain("铜铃诱敌"));

                Assert.That(tracker.ApplyAcousticCounter(CombatLockKind.WetClothMuffle), Is.True);
                view.Refresh();
                Assert.That(view.ObjectiveLabel.text, Does.Contain("1/2"));
                Assert.That(view.LocksLabel.text, Does.Contain("25%"));

                Assert.That(tracker.ApplyAcousticCounter(CombatLockKind.DecoyBellRing, momentumCost: 1), Is.True);
                view.Refresh();
                Assert.That(view.ObjectiveLabel.text, Does.Contain("BREAK"));
                Assert.That(view.LocksLabel.text, Does.Contain("已破锁"));
                Assert.That(view.MomentumLabel.text, Does.Contain("0/3"));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(prevLang);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NullArgumentsRejectedAndParentTeardownDisposesCleanly()
        {
            var root = new GameObject("Lock-break HUD teardown");
            var tracker = new S00BridgeheadCableTracker();
            CombatLockBreakHudView view = null;
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new CombatLockBreakHudView(null, tracker));
                Assert.Throws<ArgumentNullException>(() =>
                    new CombatLockBreakHudView(root.transform, (S00BridgeheadCableTracker)null));
                Assert.Throws<ArgumentNullException>(() =>
                    new CombatLockBreakHudView(root.transform, (SoundwellAcousticBreakTracker)null));

                view = new CombatLockBreakHudView(root.transform, tracker);
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.That(view.IsDisposed, Is.True);
            }
            finally
            {
                view?.Dispose();
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
