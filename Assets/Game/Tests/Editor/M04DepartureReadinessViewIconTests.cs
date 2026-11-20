using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04DepartureReadinessViewIconTests
    {
        private static M04DepartureManifest Plan(bool ready, bool stamped = true)
        {
            return new M04DepartureManifest(M03VoyageChoice.HanQiaoOfficial,
                ready ? "Inspected hull" : null, ready ? "Confirmed pilot" : null,
                ready ? 3 : 0, ready ? "Inspected tender" : null,
                ready, ready ? 18 : 0, 13,
                ready ? "Named landing receiver" : null, stamped, false);
        }

        [Test]
        public void StatusIconTracksBlockedReadyAndConfirmedStates()
        {
            var root = new GameObject("M04 readiness icon states");
            M04DepartureReadinessView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var blocked = Plan(false, stamped: false);
                view = new M04DepartureReadinessView(root.transform, blocked,
                    _ => { }, () => { });

                Assert.That(view.StatusIcon, Is.Not.Null);
                Assert.That(view.StatusIcon.gameObject.activeSelf, Is.True);
                Assert.That(view.StatusIcon.raycastTarget, Is.False);
                Assert.That(view.StatusIcon.preserveAspect, Is.True);
                Assert.That(view.StatusIcon.sprite, Is.Not.Null);
                Assert.That(view.StatusIcon.sprite.name,
                    Is.EqualTo(RpgStoryArtResolver.ResolveM07PassSprite(stamped: false).name));
                Assert.That(view.StatusIcon.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(view.StatusIcon.sprite.pixelsPerUnit, Is.EqualTo(16f));

                var ready = Plan(true);
                view.SetConfirmedManifest(ready);
                Assert.That(view.StatusIcon.gameObject.activeSelf, Is.True);
                Assert.That(view.StatusIcon.sprite, Is.Not.Null);
                Assert.That(view.StatusIcon.sprite.name,
                    Is.EqualTo(RpgStoryArtResolver.ResolveM03TidalChartSprite(open: true).name));
                Assert.That(view.StatusIcon.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(view.StatusIcon.sprite.pixelsPerUnit, Is.EqualTo(16f));

                view.ConfirmButton.onClick.Invoke();
                Assert.That(ready.DepartureConfirmed, Is.True);
                Assert.That(view.StatusIcon.gameObject.activeSelf, Is.True);
                Assert.That(view.StatusIcon.sprite, Is.Not.Null);
                Assert.That(view.StatusIcon.sprite.name,
                    Is.EqualTo(RpgStoryArtResolver.ResolveM07PassSprite(stamped: true).name));
                Assert.That(view.StatusIcon.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(view.StatusIcon.sprite.pixelsPerUnit, Is.EqualTo(16f));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.ConfirmButton.interactable, Is.False);
                Assert.That(view.StatusIcon.sprite.name,
                    Is.EqualTo(RpgStoryArtResolver.ResolveM07PassSprite(stamped: true).name));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StatusIconDoesNotOverlapDetailsAndSurvivesViewportScaling()
        {
            var root = new GameObject("M04 readiness icon layout");
            M04DepartureReadinessView view = null;
            try
            {
                view = new M04DepartureReadinessView(root.transform, Plan(true),
                    _ => { }, () => { });

                RectTransform iconRect = view.StatusIcon.rectTransform;
                RectTransform detailsRect = view.Details.rectTransform;
                float iconRight = iconRect.anchoredPosition.x + iconRect.sizeDelta.x * 0.5f;
                float detailsLeft = detailsRect.anchoredPosition.x - detailsRect.sizeDelta.x * 0.5f;
                Assert.That(iconRight, Is.LessThan(detailsLeft));

                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                {
                    view.LayoutForViewportAspect(aspect);
                    Assert.That(view.StatusIcon.gameObject.activeSelf, Is.True);
                    Assert.That(view.StatusIcon.sprite, Is.Not.Null);
                }
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
