using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04RosterReconciliationViewIconTests
    {
        [Test]
        public void RosterIconTracksLedgerDiscrepancyConfirmationAndBookingStates()
        {
            var root = new GameObject("M04 roster icon states");
            M04RosterReconciliationView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var ledger = new M04RosterLedger();
                ledger.AddEntry("roll-01", "Memorial listed survivor", M04RosterStatus.ListedDead);

                view = new M04RosterReconciliationView(
                    root.transform, ledger, 1, _ => { }, () => { });

                Assert.That(view.RosterIcon, Is.Not.Null);
                Assert.That(view.RosterIcon.gameObject.activeSelf, Is.True);
                Assert.That(view.RosterIcon.raycastTarget, Is.False);
                Assert.That(view.RosterIcon.preserveAspect, Is.True);
                Assert.That(view.RosterIcon.sprite, Is.Not.Null);
                Assert.That(view.RosterIcon.sprite.name,
                    Is.EqualTo(RpgStoryArtResolver.ResolveM02LedgerSprite().name));
                Assert.That(view.RosterIcon.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(view.RosterIcon.sprite.pixelsPerUnit, Is.EqualTo(16f));

                // Confirming a dead-roll entry raises a discrepancy -> pending pass icon.
                view.ConfirmDockButton.onClick.Invoke();
                Assert.That(view.RosterIcon.sprite.name,
                    Is.EqualTo(RpgStoryArtResolver.ResolveM07PassSprite(stamped: false).name));

                // Resolving the dead-roll discrepancy -> sealed document icon for confirmed at dock.
                view.ResolveRollButton.onClick.Invoke();
                Assert.That(view.RosterIcon.sprite.name,
                    Is.EqualTo(RpgStoryArtResolver.ResolveM02DocumentSprite(sealedDocument: true).name));

                // Booking a berth -> stamped pass icon.
                view.BookButton.onClick.Invoke();
                Assert.That(view.RosterIcon.sprite.name,
                    Is.EqualTo(RpgStoryArtResolver.ResolveM07PassSprite(stamped: true).name));
                Assert.That(view.RosterIcon.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(view.RosterIcon.sprite.pixelsPerUnit, Is.EqualTo(16f));

                // Lowering verified berths below booked count flags overbooking -> pending pass icon.
                view.SetVerifiedBerths(0);
                Assert.That(view.RosterIcon.sprite.name,
                    Is.EqualTo(RpgStoryArtResolver.ResolveM07PassSprite(stamped: false).name));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.RosterIcon.sprite.name,
                    Is.EqualTo(RpgStoryArtResolver.ResolveM07PassSprite(stamped: false).name));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RosterIconDoesNotOverlapTextAndSurvivesEmptyLedgerAndScaling()
        {
            var root = new GameObject("M04 roster icon layout");
            M04RosterReconciliationView view = null;
            try
            {
                var ledger = new M04RosterLedger();
                view = new M04RosterReconciliationView(
                    root.transform, ledger, 0, _ => { }, () => { });

                Assert.That(view.RosterIcon, Is.Not.Null);
                Assert.That(view.RosterIcon.gameObject.activeSelf, Is.True);
                Assert.That(view.RosterIcon.sprite.name,
                    Is.EqualTo(RpgStoryArtResolver.ResolveM02LedgerSprite().name));

                RectTransform iconRect = view.RosterIcon.rectTransform;
                RectTransform summaryRect = view.Summary.rectTransform;
                RectTransform detailsRect = view.SelectedDetails.rectTransform;
                float iconRight = iconRect.anchoredPosition.x + iconRect.sizeDelta.x * 0.5f;
                float summaryLeft = summaryRect.anchoredPosition.x - summaryRect.sizeDelta.x * 0.5f;
                float detailsLeft = detailsRect.anchoredPosition.x - detailsRect.sizeDelta.x * 0.5f;
                Assert.That(iconRight, Is.LessThan(summaryLeft));
                Assert.That(iconRight, Is.LessThan(detailsLeft));

                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                {
                    view.LayoutForViewportAspect(aspect);
                    Assert.That(view.RosterIcon.gameObject.activeSelf, Is.True);
                    Assert.That(view.RosterIcon.sprite, Is.Not.Null);
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
