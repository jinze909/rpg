using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M02QiQueNightViewTests
    {
        private GameObject root;
        private M02QiQueNightView view;
        private string previousLanguage;

        [SetUp]
        public void SetUp()
        {
            previousLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M02 Qi Que night test");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
            SoundwellUiLocalization.SetLanguage(previousLanguage);
        }

        [Test]
        public void StageIconTracksWarehouseLedgerChartAndHidesOnFinishWithoutLeakingDebt()
        {
            RpgPixelArtManifest manifest = RpgPixelArtManifest.LoadDefault();
            Assert.That(manifest, Is.Not.Null);

            view = new M02QiQueNightView(root.transform, true, () => { });
            AssertStageIcon(view.StageIcon, manifest.M07CargoCoverCrateClosed);

            view.ProgressButton.onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.M02BridgeKeeperOpenLedger);

            view.ProgressButton.onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.M03TidalChartFolded);

            view.ProgressButton.onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.M03TidalChartOpen);

            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.Finished));
            Assert.That(view.StageIcon.sprite, Is.Null);
            Assert.That(view.StageIcon.enabled, Is.False);
            Assert.That(view.StageIcon.gameObject.activeSelf, Is.False);
            view.Dispose();

            // Unknown debt skips the ledger confrontation and goes straight to the folded chart.
            view = new M02QiQueNightView(root.transform, false, () => { });
            AssertStageIcon(view.StageIcon, manifest.M07CargoCoverCrateClosed);
            view.ProgressButton.onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.M03TidalChartFolded);
        }

        private static void AssertStageIcon(Image icon, Sprite expected)
        {
            Assert.That(icon, Is.Not.Null);
            Assert.That(expected, Is.Not.Null);
            Assert.That(icon.sprite, Is.EqualTo(expected));
            Assert.That(icon.preserveAspect, Is.True);
            Assert.That(icon.raycastTarget, Is.False);
            Assert.That(icon.enabled, Is.True);
            Assert.That(icon.gameObject.activeSelf, Is.True);
            Assert.That(icon.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(icon.sprite.pixelsPerUnit, Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
        }

        [Test]
        public void DebtConfrontationAppearsOnlyWhenFactWasKnownEarlier()
        {
            int completed = 0;
            view = new M02QiQueNightView(root.transform, true, () => completed++);
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.ConfrontingDebt));
            Assert.That(view.Dialogue.text, Does.Contain("mortgaged"));
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Night.DebtConfronted, Is.True);
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Night.ChartStudied, Is.True);
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.Finished));
        }

        [Test]
        public void UnknownDebtAndLanguageChangesNeverLeakTheSecret()
        {
            int completed = 0;
            view = new M02QiQueNightView(root.transform, false, () => completed++);
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.OfferingCake));
            Assert.That(view.Dialogue.text, Does.Not.Contain("韩峤"));
            view.LayoutForViewportAspect(9f / 16f);
            view.LayoutForViewportAspect(32f / 9f);
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Night.DebtConfronted, Is.False);
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void FailedHostCallbackKeepsWarehouseDepartureRetryableAndBlocksReentrancy()
        {
            int attempts = 0;
            view = new M02QiQueNightView(root.transform, true, () =>
            {
                attempts++;
                if (attempts == 1)
                {
                    view.ProgressButton.onClick.Invoke();
                    throw new InvalidOperationException("Simulated host handoff failure");
                }
            });
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.ReviewingChart));

            Assert.Throws<InvalidOperationException>(() => view.ProgressButton.onClick.Invoke());
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.ReviewingChart));
            Assert.That(view.Night.DebtConfronted, Is.True);
            Assert.That(view.Night.ChartStudied, Is.True);
            Assert.That(view.ProgressButton.gameObject.activeSelf, Is.True);

            view.ProgressButton.onClick.Invoke();
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.Finished));
            Assert.That(view.ProgressButton.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void DestroyedWarehouseCanvasRejectsLayoutAndLanguageCallbacksWithoutLeaving()
        {
            foreach (bool debtKnown in new[] { false, true })
            {
                foreach (bool layoutFirst in new[] { false, true })
                {
                    var warehouse = new GameObject("Destroyed Qi Que warehouse");
                    try
                    {
                        SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                        int persisted = 0;
                        view = new M02QiQueNightView(warehouse.transform, debtKnown,
                            () => persisted++);
                        Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.AtWarehouse));

                        UnityEngine.Object.DestroyImmediate(warehouse);
                        warehouse = null;

                        if (layoutFirst)
                            Assert.DoesNotThrow(() => view.LayoutForViewportAspect(9f / 16f));
                        Assert.DoesNotThrow(() => SoundwellUiLocalization.SetLanguage(
                            SoundwellUiLocalization.Chinese));
                        Assert.DoesNotThrow(() => view.LayoutForViewportAspect(32f / 9f));
                        Assert.DoesNotThrow(() => view.Refresh());
                        Assert.DoesNotThrow(() => view.Dispose());
                        Assert.DoesNotThrow(() => view.Dispose());
                        Assert.DoesNotThrow(() => SoundwellUiLocalization.SetLanguage(
                            SoundwellUiLocalization.English));
                        Assert.That(view.Night.Stage,
                            Is.EqualTo(M02QiQueNightStage.AtWarehouse));
                        Assert.That(view.Night.DebtAlreadyKnown, Is.EqualTo(debtKnown));
                        Assert.That(view.Night.DebtConfronted, Is.False);
                        Assert.That(persisted, Is.Zero);
                    }
                    finally
                    {
                        view?.Dispose();
                        if (warehouse != null)
                            UnityEngine.Object.DestroyImmediate(warehouse);
                    }
                }
            }
        }

        [Test]
        public void DisposalStopsCallbackAndDetachesLocalization()
        {
            int calls = 0;
            view = new M02QiQueNightView(root.transform, true, () => calls++);
            view.ProgressButton.onClick.Invoke();
            view.Dispose();
            view.ProgressButton.onClick.Invoke();
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(calls, Is.Zero);
        }
    }
}
