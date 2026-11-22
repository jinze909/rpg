using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class CombatLockBreakDirectorLifecycleTests
    {
        private GameObject root;
        private CombatLockBreakRuntimeDirector director;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("CombatLockBreakDirectorLifecycleRoot");
            director = root.AddComponent<CombatLockBreakRuntimeDirector>();
        }

        [TearDown]
        public void TearDown()
        {
            if (director != null)
                director.DetachBattle();
            director = null;
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void HudView_IsDisposedReflectsExternalTeardownImmediately_AndIgnoresInvalidAspect()
        {
            var tracker = new S00BridgeheadCableTracker(startingMomentum: 1);
            var view = new CombatLockBreakHudView(root.transform, tracker);
            try
            {
                Vector3 initialScale = view.Panel.localScale;
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(float.NaN));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(float.PositiveInfinity));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(float.NegativeInfinity));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(0f));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(-1.5f));
                Assert.That(view.Panel.localScale, Is.EqualTo(initialScale));

                Assert.That(view.IsDisposed, Is.False);
                UnityEngine.Object.DestroyImmediate(view.Panel.gameObject);
                Assert.That(view.IsDisposed, Is.True);
            }
            finally
            {
                view.Dispose();
            }
        }

        [Test]
        public void Director_ExternalHudTeardown_ClearsAttachedStateAndAllowsReattach()
        {
            director.AttachForBattle(BattleContextMode.SoundwellParty, startingMomentum: 1);
            Assert.That(director.IsBattleAttached, Is.True);
            Assert.That(director.HudView, Is.Not.Null);

            UnityEngine.Object.DestroyImmediate(director.HudView.Panel.gameObject);

            Assert.That(director.IsBattleAttached, Is.False);
            Assert.That(director.HudView, Is.Null);
            Assert.That(director.Adapter, Is.Null);
            Assert.That(director.ObserveSoundwellAcousticAction(CombatLockKind.WetClothMuffle), Is.False);
            Assert.That(director.ObserveSnapshots(Array.Empty<OrkPartyBridge.BattleUnitSnapshot>()), Is.False);

            director.AttachForBattle(BattleContextMode.WhitewaterSolo, startingMomentum: 1);
            Assert.That(director.IsBattleAttached, Is.True);
            Assert.That(director.HudView, Is.Not.Null);
            Assert.That(director.ActiveMode, Is.EqualTo(BattleContextMode.WhitewaterSolo));
        }

        [Test]
        public void Director_ManualHudDispose_ClearsAttachedStateCleanly()
        {
            director.AttachForBattle(BattleContextMode.WhitewaterSolo, startingMomentum: 1);
            Assert.That(director.IsBattleAttached, Is.True);

            director.HudView.Dispose();

            Assert.That(director.IsBattleAttached, Is.False);
            Assert.That(director.HudView, Is.Null);
            Assert.That(director.Adapter, Is.Null);
            Assert.That(director.ObserveSnapshots(Array.Empty<OrkPartyBridge.BattleUnitSnapshot>()), Is.False);
        }

        [Test]
        public void Director_InvalidBattleContextMode_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                director.AttachForBattle((BattleContextMode)999));
            Assert.That(director.IsBattleAttached, Is.False);
            Assert.That(director.HudView, Is.Null);
        }
    }
}
