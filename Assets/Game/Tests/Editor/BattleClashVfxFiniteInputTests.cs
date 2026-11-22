using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class BattleClashVfxFiniteInputTests
    {
        [Test]
        public void EvaluateCardRecoilReturnsZeroForNonFiniteTimeOrImpulse()
        {
            var (nanTimeOffset, nanTimeFlash) = BattleClashVfxDirector.EvaluateCardRecoil(
                float.NaN, new Vector2(18f, 0f));
            Assert.That(nanTimeOffset, Is.EqualTo(Vector2.zero));
            Assert.That(nanTimeFlash, Is.Zero);

            var (infTimeOffset, infTimeFlash) = BattleClashVfxDirector.EvaluateCardRecoil(
                float.PositiveInfinity, new Vector2(18f, 0f));
            Assert.That(infTimeOffset, Is.EqualTo(Vector2.zero));
            Assert.That(infTimeFlash, Is.Zero);

            var (nanImpulseOffset, nanImpulseFlash) = BattleClashVfxDirector.EvaluateCardRecoil(
                0.1f, new Vector2(float.NaN, 4f));
            Assert.That(nanImpulseOffset, Is.EqualTo(Vector2.zero));
            Assert.That(nanImpulseFlash, Is.Zero);

            var (infImpulseOffset, infImpulseFlash) = BattleClashVfxDirector.EvaluateCardRecoil(
                0.1f, new Vector2(0f, float.NegativeInfinity));
            Assert.That(infImpulseOffset, Is.EqualTo(Vector2.zero));
            Assert.That(infImpulseFlash, Is.Zero);
        }

        [Test]
        public void UpdateEffectsAndRecoilsIgnoreNonFiniteDeltaTimeAndRecoverCleanly()
        {
            var directorRoot = new GameObject("BattleClashFiniteTestDirector");
            var cardObj = new GameObject("S00 enemy card",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var cardRect = cardObj.GetComponent<RectTransform>();
            var cardImg = cardObj.GetComponent<Image>();
            Vector2 restPos = new Vector2(320f, 144f);
            Color restColor = new Color(0.2f, 0.25f, 0.3f, 1f);
            cardRect.anchoredPosition = restPos;
            cardImg.color = restColor;

            try
            {
                var director = directorRoot.AddComponent<BattleClashVfxDirector>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

                // Awake does not execute on AddComponent in EditMode tests; initialize the overlay explicitly.
                MethodInfo buildOverlay = typeof(BattleClashVfxDirector).GetMethod("BuildOverlay", flags);
                MethodInfo spawnSlash = typeof(BattleClashVfxDirector).GetMethod("SpawnSlashArc", flags);
                MethodInfo triggerRecoil = typeof(BattleClashVfxDirector).GetMethod("TriggerNamedCardRecoil", flags);
                MethodInfo updateEffects = typeof(BattleClashVfxDirector).GetMethod("UpdateEffects", flags);
                MethodInfo updateRecoils = typeof(BattleClashVfxDirector).GetMethod("UpdateRecoils", flags);
                FieldInfo effectsField = typeof(BattleClashVfxDirector).GetField("effects", flags);
                FieldInfo recoilsField = typeof(BattleClashVfxDirector).GetField("recoils", flags);

                Assert.That(buildOverlay, Is.Not.Null);
                Assert.That(spawnSlash, Is.Not.Null);
                Assert.That(triggerRecoil, Is.Not.Null);
                Assert.That(updateEffects, Is.Not.Null);
                Assert.That(updateRecoils, Is.Not.Null);

                buildOverlay.Invoke(director, null);

                // Non-finite spawn inputs are rejected before creating VFX or recoil entries.
                spawnSlash.Invoke(director, new object[] { new Vector2(float.NaN, 0f), 0f, Color.white });
                spawnSlash.Invoke(director, new object[] { Vector2.zero, float.PositiveInfinity, Color.white });
                triggerRecoil.Invoke(director, new object[] { "S00 enemy card", new Vector2(float.NaN, 0f), Color.red });
                Assert.That(((ICollection)effectsField.GetValue(director)).Count, Is.Zero);
                Assert.That(((ICollection)recoilsField.GetValue(director)).Count, Is.Zero);

                // Valid spawn followed by NaN/Infinity deltaTime must not poison or leak active items.
                spawnSlash.Invoke(director, new object[] { new Vector2(100f, 50f), -25f, Color.yellow });
                triggerRecoil.Invoke(director, new object[] { "S00 enemy card", new Vector2(18f, 0f), Color.white });
                Assert.That(((ICollection)effectsField.GetValue(director)).Count, Is.EqualTo(1));
                Assert.That(((ICollection)recoilsField.GetValue(director)).Count, Is.EqualTo(1));

                updateEffects.Invoke(director, new object[] { float.NaN });
                updateEffects.Invoke(director, new object[] { float.PositiveInfinity });
                updateRecoils.Invoke(director, new object[] { float.NaN });
                updateRecoils.Invoke(director, new object[] { float.NegativeInfinity });

                Assert.That(float.IsNaN(cardRect.anchoredPosition.x), Is.False);
                Assert.That(float.IsInfinity(cardRect.anchoredPosition.x), Is.False);
                Assert.That(cardRect.anchoredPosition, Is.EqualTo(restPos));
                Assert.That(((ICollection)effectsField.GetValue(director)).Count, Is.EqualTo(1));
                Assert.That(((ICollection)recoilsField.GetValue(director)).Count, Is.EqualTo(1));

                // Subsequent finite deltaTime still completes and cleans up both VFX and recoil.
                updateEffects.Invoke(director, new object[] { 0.5f });
                updateRecoils.Invoke(director, new object[] { 0.5f });
                Assert.That(((ICollection)effectsField.GetValue(director)).Count, Is.Zero);
                Assert.That(((ICollection)recoilsField.GetValue(director)).Count, Is.Zero);
                Assert.That(cardRect.anchoredPosition, Is.EqualTo(restPos));
                Assert.That(cardImg.color, Is.EqualTo(restColor));
            }
            finally
            {
                Object.DestroyImmediate(directorRoot);
                Object.DestroyImmediate(cardObj);
            }
        }
    }
}
