using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04CastOffViewTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> saved)
        {
            return key => saved.TryGetValue(key, out int value) ? value : 0;
        }

        private static void ConfirmReady(Dictionary<string, int> saved)
        {
            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant, "Verified hull", "A Yuan", 4,
                "Actual shallow tender", true, 15, 9, "Named landing receiver",
                false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest,
                (key, value) => saved[key] = value), Is.True);
        }

        [Test]
        public void CastOffSummaryShowsActualVerifiedRouteBeforeAndAfterDeparture()
        {
            var routes = new[]
            {
                (M03VoyageChoice.LuJunEscort, "陆钧护航", "Lu Jun escort"),
                (M03VoyageChoice.QiQueMerchant, "祁雀商路", "Qi Que merchant route"),
                (M03VoyageChoice.HanQiaoOfficial, "韩峤官船", "Han Qiao official ship"),
                (M03VoyageChoice.MixedWithOfficial, "官船混合航路", "Mixed convoy (official)"),
                (M03VoyageChoice.MixedWithCivilian, "民船混合航路", "Mixed convoy (civilian)")
            };
            foreach (var (route, chineseName, englishName) in routes)
            {
                var confirmed = new M04DepartureRecord(route, 4, 15, 9, false, false);
                string awaitingCastOff = M04CastOffCopy.Describe(confirmed, false, false);
                string sailed = M04CastOffCopy.Describe(confirmed, true, true);
                Assert.That(awaitingCastOff, Does.Contain(englishName));
                Assert.That(awaitingCastOff, Does.Contain("still at the dock"));
                Assert.That(sailed, Does.Contain(chineseName));
                Assert.That(sailed, Does.Contain("船已离岸"));
            }
            Assert.That(M04CastOffCopy.Describe(null, false, false),
                Does.Not.Contain("This voyage:"),
                "No route may be presented as verified without a confirmed manifest.");
        }

        [Test]
        public void ChosenRouteAndUnconfirmedPlanCannotLaunchAnyVessel()
        {
            var root = new GameObject("M04 unconfirmed cast off");
            var saved = new Dictionary<string, int>();
            M04CastOffView view = null;
            try
            {
                int nextScene = 0;
                view = new M04CastOffView(root.transform,
                    Reader(saved), (key, value) => saved[key] = value,
                    () => nextScene++);
                Assert.That(view.CastOffButton.interactable, Is.False);
                Assert.That(view.Details.text, Does.Contain("no verified"));
                view.CastOffButton.onClick.Invoke();
                Assert.That(nextScene, Is.Zero);
                Assert.That(saved.ContainsKey(M04DepartureFacts.SailedKey), Is.False);

                ConfirmReady(saved);
                view.Refresh();
                Assert.That(view.CastOffButton.interactable, Is.True);
                Assert.That(view.Details.text, Does.Contain("still at the dock"));
                Assert.That(saved[M04DepartureFacts.SailedKey], Is.Zero,
                    "Merely opening a cast-off view does not set sail.");
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CastOffMarksSailedOnlyAfterButtonAndOnlyOnce()
        {
            var root = new GameObject("M04 real cast off");
            var saved = new Dictionary<string, int>();
            M04CastOffView view = null;
            try
            {
                ConfirmReady(saved);
                int castOffWrites = 0, nextScene = 0;
                view = new M04CastOffView(root.transform, Reader(saved),
                    (key, value) =>
                    {
                        if (key == M04DepartureFacts.SailedKey && value == 1)
                            castOffWrites++;
                        saved[key] = value;
                    }, () =>
                    {
                        nextScene++;
                        view.CastOffButton.onClick.Invoke();
                    });
                Assert.That(saved[M04DepartureFacts.SailedKey], Is.Zero);
                view.CastOffButton.onClick.Invoke();
                view.CastOffButton.onClick.Invoke();
                Assert.That(saved[M04DepartureFacts.SailedKey], Is.EqualTo(1));
                Assert.That(castOffWrites, Is.EqualTo(1));
                Assert.That(nextScene, Is.EqualTo(1));
                Assert.That(view.CastOffButton.gameObject.activeSelf, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HostTransitionFailureAllowsRetryWithoutDoubleCastOff()
        {
            var root = new GameObject("M04 transition retry");
            var saved = new Dictionary<string, int>();
            M04CastOffView view = null;
            try
            {
                ConfirmReady(saved);
                int castOffWrites = 0, attempts = 0;
                view = new M04CastOffView(root.transform, Reader(saved),
                    (key, value) =>
                    {
                        saved[key] = value;
                        if (key == M04DepartureFacts.SailedKey && value == 1)
                            castOffWrites++;
                    }, () =>
                    {
                        attempts++;
                        view.CastOffButton.onClick.Invoke();
                        if (attempts == 1)
                            throw new InvalidOperationException("Fog scene unavailable");
                    });
                Assert.Throws<InvalidOperationException>(() =>
                    view.CastOffButton.onClick.Invoke());
                Assert.That(saved[M04DepartureFacts.SailedKey], Is.EqualTo(1));
                Assert.That(view.Details.text, Does.Contain("left the dock"));
                Assert.That(view.CastOffButton.interactable, Is.True);
                Assert.That(attempts, Is.EqualTo(1));
                view.CastOffButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(castOffWrites, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StaleManifestCorruptSailedBitAndInterruptedWriteCannotSail()
        {
            var root = new GameObject("M04 invalid sailing fact");
            var saved = new Dictionary<string, int>();
            M04CastOffView view = null;
            try
            {
                ConfirmReady(saved);
                int nextScene = 0;
                Func<string, int> read = Reader(saved);
                view = new M04CastOffView(root.transform, read,
                    (key, value) =>
                    {
                        if (key == M04DepartureFacts.SailedKey)
                            throw new InvalidOperationException("Save unavailable");
                        saved[key] = value;
                    }, () => nextScene++);
                Assert.Throws<InvalidOperationException>(() =>
                    view.CastOffButton.onClick.Invoke());
                Assert.That(nextScene, Is.Zero);
                Assert.That(saved[M04DepartureFacts.SailedKey], Is.Zero);
                Assert.That(view.CastOffButton.interactable, Is.True);

                saved[M04DepartureFacts.SailedKey] = 2;
                view.Refresh();
                Assert.That(view.CastOffButton.interactable, Is.False);
                view.CastOffButton.onClick.Invoke();
                Assert.That(nextScene, Is.Zero);

                saved[M04DepartureFacts.SailedKey] = 0;
                saved[M04DepartureFacts.ConfirmedKey] = 0;
                view.Refresh();
                Assert.That(view.CastOffButton.interactable, Is.False);
                view.CastOffButton.onClick.Invoke();
                Assert.That(nextScene, Is.Zero);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LanguageAndViewportChangesDoNotLaunchOrInventResources()
        {
            var root = new GameObject("M04 cast off locale");
            var saved = new Dictionary<string, int>();
            M04CastOffView view = null;
            string language = SoundwellUiLocalization.Language;
            try
            {
                ConfirmReady(saved);
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                view = new M04CastOffView(root.transform, Reader(saved),
                    (key, value) => saved[key] = value, () => { });
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Details.text, Does.Contain("可用铺位 15"));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                Assert.That(view.Panel.sizeDelta.y, Is.LessThan(210f));
                Assert.That(saved[M04DepartureFacts.SailedKey], Is.Zero);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(language);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposeRejectsInputAndConstructorRequiresActualSaveDelegates()
        {
            var root = new GameObject("M04 cast off dispose");
            M04CastOffView view = null;
            try
            {
                var saved = new Dictionary<string, int>();
                Func<string, int> read = Reader(saved);
                Action<string, int> write = (key, value) => saved[key] = value;
                Assert.Throws<ArgumentNullException>(() =>
                    new M04CastOffView(null, read, write, () => { }));
                Assert.Throws<ArgumentNullException>(() =>
                    new M04CastOffView(root.transform, null, write, () => { }));
                Assert.Throws<ArgumentNullException>(() =>
                    new M04CastOffView(root.transform, read, null, () => { }));
                Assert.Throws<ArgumentNullException>(() =>
                    new M04CastOffView(root.transform, read, write, null));
                ConfirmReady(saved);
                int callbacks = 0;
                view = new M04CastOffView(root.transform, read, write,
                    () => callbacks++);
                var button = view.CastOffButton;
                view.Dispose();
                button.onClick.Invoke();
                Assert.That(callbacks, Is.Zero);
                Assert.That(saved[M04DepartureFacts.SailedKey], Is.Zero);
                Assert.DoesNotThrow(() => view.Refresh());
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
