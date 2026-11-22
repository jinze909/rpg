using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class CombatLockBreakHudConstructorLeakTests
    {
        [Test]
        public void NullTrackerConstructorsThrowBeforeCreatingCanvasOrSubscribingLocalization()
        {
            var root = new GameObject("LockBreakHudCtorLeakRoot");
            string prevLang = SoundwellUiLocalization.Language;
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new CombatLockBreakHudView(root.transform, (S00BridgeheadCableTracker)null));
                Assert.That(root.transform.childCount, Is.Zero,
                    "Failed S00 constructor must not leak an orphaned HUD canvas.");

                Assert.Throws<ArgumentNullException>(() =>
                    new CombatLockBreakHudView(root.transform, (SoundwellAcousticBreakTracker)null));
                Assert.That(root.transform.childCount, Is.Zero,
                    "Failed Soundwell constructor must not leak an orphaned HUD canvas.");

                // Switching localization after failed constructions must stay clean.
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                SoundwellUiLocalization.SetLanguage(prevLang);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
