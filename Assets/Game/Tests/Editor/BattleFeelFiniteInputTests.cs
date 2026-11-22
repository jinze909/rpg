using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class BattleFeelFiniteInputTests
    {
        [Test]
        public void PopupsRejectNonFiniteSpawnAndDeltaTimeWithoutLeaking()
        {
            var root = new GameObject("BattleFeelFinitePopupRoot");
            try
            {
                var director = root.AddComponent<BattleFeelAndTelegraphDirector>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                MethodInfo buildOverlay = typeof(BattleFeelAndTelegraphDirector).GetMethod("BuildOverlay", flags);
                FieldInfo overlayRootField = typeof(BattleFeelAndTelegraphDirector).GetField("overlayRoot", flags);
                FieldInfo latinFontField = typeof(BattleFeelAndTelegraphDirector).GetField("latinFont", flags);
                MethodInfo spawnPopup = typeof(BattleFeelAndTelegraphDirector).GetMethod("SpawnPopup", flags);
                MethodInfo updatePopups = typeof(BattleFeelAndTelegraphDirector).GetMethod("UpdatePopups", flags);
                FieldInfo popupsField = typeof(BattleFeelAndTelegraphDirector).GetField("popups", flags);

                Assert.That(buildOverlay, Is.Not.Null);
                Assert.That(overlayRootField, Is.Not.Null);
                Assert.That(latinFontField, Is.Not.Null);
                Assert.That(spawnPopup, Is.Not.Null);
                Assert.That(updatePopups, Is.Not.Null);
                Assert.That(popupsField, Is.Not.Null);

                // Awake is not invoked for a newly-added MonoBehaviour in EditMode tests.
                // Initialize the same overlay root and English font used by the runtime path.
                if (latinFontField.GetValue(director) == null)
                    latinFontField.SetValue(director,
                        Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
                if (overlayRootField.GetValue(director) == null)
                    buildOverlay.Invoke(director, null);

                // Non-finite spawn coordinates are rejected before creating a popup.
                spawnPopup.Invoke(director, new object[] { "-5", new Vector2(float.NaN, 10f), Color.red });
                spawnPopup.Invoke(director, new object[] { "-5", new Vector2(10f, float.PositiveInfinity), Color.red });
                Assert.That(((ICollection)popupsField.GetValue(director)).Count, Is.Zero);

                // Valid popup ignores NaN/Infinity deltaTime and finishes cleanly on finite deltaTime.
                spawnPopup.Invoke(director, new object[] { "-7", new Vector2(120f, 80f), Color.yellow });
                Assert.That(((ICollection)popupsField.GetValue(director)).Count, Is.EqualTo(1));

                updatePopups.Invoke(director, new object[] { float.NaN });
                updatePopups.Invoke(director, new object[] { float.PositiveInfinity });
                updatePopups.Invoke(director, new object[] { float.NegativeInfinity });
                Assert.That(((ICollection)popupsField.GetValue(director)).Count, Is.EqualTo(1));

                updatePopups.Invoke(director, new object[] { 1.0f });
                Assert.That(((ICollection)popupsField.GetValue(director)).Count, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CameraShakeRejectsNonFiniteInputsAndRestoresCameraPositionCleanly()
        {
            var root = new GameObject("BattleFeelFiniteShakeRoot");
            var camObj = new GameObject("BattleFeelFiniteTestCamera", typeof(Camera));
            camObj.tag = "MainCamera";
            Vector3 restPos = new Vector3(12f, -4f, -10f);
            camObj.transform.position = restPos;

            try
            {
                var director = root.AddComponent<BattleFeelAndTelegraphDirector>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                MethodInfo triggerShake = typeof(BattleFeelAndTelegraphDirector).GetMethod("TriggerCameraShake", flags);
                MethodInfo updateShake = typeof(BattleFeelAndTelegraphDirector).GetMethod("UpdateCameraShake", flags);

                Assert.That(triggerShake, Is.Not.Null);
                Assert.That(updateShake, Is.Not.Null);

                // Non-finite intensity/duration must be ignored.
                triggerShake.Invoke(director, new object[] { float.NaN, 0.2f });
                triggerShake.Invoke(director, new object[] { 0.3f, float.PositiveInfinity });
                updateShake.Invoke(director, new object[] { 0.05f });
                Assert.That(camObj.transform.position, Is.EqualTo(restPos));

                // Valid shake ignores non-finite dt and restores exact camera position when finished.
                triggerShake.Invoke(director, new object[] { 0.25f, 0.16f });
                updateShake.Invoke(director, new object[] { float.NaN });
                updateShake.Invoke(director, new object[] { float.PositiveInfinity });
                Assert.That(float.IsNaN(camObj.transform.position.x), Is.False);
                Assert.That(float.IsInfinity(camObj.transform.position.x), Is.False);
                Assert.That(camObj.transform.position, Is.EqualTo(restPos));

                updateShake.Invoke(director, new object[] { 0.04f });
                Assert.That(float.IsNaN(camObj.transform.position.x), Is.False);

                updateShake.Invoke(director, new object[] { 0.5f });
                Assert.That(camObj.transform.position, Is.EqualTo(restPos));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(camObj);
            }
        }
    }
}
