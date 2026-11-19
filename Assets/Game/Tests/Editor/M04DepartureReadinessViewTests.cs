using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04DepartureReadinessViewTests
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
        public void BlockedVoyageCannotBeConfirmedEvenByDirectUiCallback()
        {
            var root = new GameObject("Readiness blocked");
            M04DepartureReadinessView view = null;
            try
            {
                int completed = 0;
                int returned = 0;
                var blocked = Plan(false, stamped: false);
                view = new M04DepartureReadinessView(root.transform, blocked,
                    _ => completed++, () => returned++);
                Assert.That(view.ConfirmButton.interactable, Is.False);
                Assert.That(view.BackButton.interactable, Is.True);
                Assert.That(view.Details.text, Does.Contain("Not enough verified berths"));
                Assert.That(view.Details.text, Does.Contain("Official passage lacks a valid stamp"));
                view.ConfirmButton.onClick.Invoke();
                Assert.That(completed, Is.Zero);
                Assert.That(blocked.DepartureConfirmed, Is.False);
                view.BackButton.onClick.Invoke();
                Assert.That(returned, Is.EqualTo(1));
                Assert.That(completed, Is.Zero);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RealUpdatedManifestMayBeConfirmedOnlyOnce()
        {
            var root = new GameObject("Readiness accepted");
            M04DepartureReadinessView view = null;
            try
            {
                int accepted = 0;
                M04DepartureManifest delivered = null;
                view = new M04DepartureReadinessView(root.transform, Plan(false),
                    manifest => { accepted++; delivered = manifest; }, () => { });
                var actuallyVerified = Plan(true);
                view.SetConfirmedManifest(actuallyVerified);
                Assert.That(view.ConfirmButton.interactable, Is.True);
                Assert.That(view.Details.text, Does.Contain("Verified berths: 18"));
                Assert.That(view.Details.text, Does.Contain("returnees on this trip: 13"));
                Assert.That(view.Details.text, Does.Contain("Vessel: Inspected hull"));
                Assert.That(view.Details.text, Does.Contain("pilot: Confirmed pilot"));
                Assert.That(view.Details.text, Does.Contain("Crew: 3"));
                Assert.That(view.Details.text, Does.Contain("tender: Inspected tender"));
                Assert.That(view.Details.text, Does.Contain("Landing receiver: Named landing receiver"));
                view.ConfirmButton.onClick.Invoke();
                Assert.That(accepted, Is.EqualTo(1));
                Assert.That(delivered, Is.SameAs(actuallyVerified));
                Assert.That(actuallyVerified.DepartureConfirmed, Is.True);
                view.ConfirmButton.onClick.Invoke();
                view.BackButton.onClick.Invoke();
                Assert.That(accepted, Is.EqualTo(1));
                Assert.That(view.ConfirmButton.interactable, Is.False);
                Assert.Throws<InvalidOperationException>(() =>
                    view.SetConfirmedManifest(Plan(true)));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ChangingLocaleDoesNotApproveAnUnreadySailing()
        {
            var root = new GameObject("Readiness language");
            M04DepartureReadinessView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var blocked = Plan(false, stamped: false);
                int accepted = 0;
                view = new M04DepartureReadinessView(root.transform, blocked,
                    _ => accepted++, () => { });
                Assert.That(view.Details.text, Does.Contain("No confirmed vessel"));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Details.text, Does.Contain("尚无核实的船只"));
                Assert.That(view.ConfirmButton.interactable, Is.False);
                view.ConfirmButton.onClick.Invoke();
                Assert.That(accepted, Is.Zero);
                Assert.That(blocked.DepartureConfirmed, Is.False);
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(view.Details.text, Does.Contain("No confirmed vessel"));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VerifiedDepartureDetailsUseLocalizedLabelsForTheSameResources()
        {
            var root = new GameObject("Readiness translated resources");
            M04DepartureReadinessView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var manifest = Plan(true);
                view = new M04DepartureReadinessView(root.transform, manifest,
                    _ => { }, () => { });
                Assert.That(view.Details.text, Does.Contain("Vessel: Inspected hull"));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Details.text, Does.Contain("船只：Inspected hull"));
                Assert.That(view.Details.text, Does.Contain("领航：Confirmed pilot"));
                Assert.That(view.Details.text, Does.Contain("登岸接应：Named landing receiver"));
                Assert.That(view.ConfirmButton.interactable, Is.True);
                Assert.That(manifest.DepartureConfirmed, Is.False);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidHostManifestDoesNotReplaceTheCurrentCheck()
        {
            var root = new GameObject("Readiness null manifest");
            M04DepartureReadinessView view = null;
            try
            {
                var blocked = Plan(false);
                view = new M04DepartureReadinessView(root.transform, blocked,
                    _ => { }, () => { });
                Assert.Throws<ArgumentNullException>(() => view.SetConfirmedManifest(null));
                Assert.That(view.Manifest, Is.SameAs(blocked));
                Assert.That(view.ConfirmButton.interactable, Is.False);
                foreach (var ratio in new[] { 9f / 16f, 32f / 9f, 16f / 9f })
                    view.LayoutForViewportAspect(ratio);
                Assert.That(view.ConfirmButton.interactable, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
