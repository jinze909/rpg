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
        public void BlockedVoyageStillListsVerifiedResourcesWithoutInventingMissingOnes()
        {
            var root = new GameObject("Partially ready departure");
            M04DepartureReadinessView view = null;
            string previousLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var noStamp = Plan(true, stamped: false);
                view = new M04DepartureReadinessView(root.transform, noStamp,
                    _ => Assert.Fail("Incomplete official passage cannot depart."), () => { });

                Assert.That(view.ConfirmButton.interactable, Is.False);
                Assert.That(view.Details.text, Does.Contain("Official passage lacks a valid stamp"));
                Assert.That(view.Details.text, Does.Contain("Verified so far:"));
                Assert.That(view.Details.text, Does.Contain("Vessel: Inspected hull"));
                Assert.That(view.Details.text, Does.Contain("Pilot: Confirmed pilot"));
                Assert.That(view.Details.text, Does.Contain("Crew: 3"));
                Assert.That(view.Details.text, Does.Contain("Tender: Inspected tender"));
                Assert.That(view.Details.text, Does.Contain("Landing receiver: Named landing receiver"));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Details.text, Does.Contain("官方航路缺少有效的登记批准"));
                Assert.That(view.Details.text, Does.Contain("已经核实的准备："));
                Assert.That(view.Details.text, Does.Contain("船只：Inspected hull"));

                view.SetConfirmedManifest(Plan(false, stamped: false));
                Assert.That(view.Details.text, Does.Contain("尚无核实的船只"));
                Assert.That(view.Details.text, Does.Not.Contain("已经核实的准备："));
                Assert.That(view.Details.text, Does.Not.Contain("Inspected hull"));
                Assert.That(view.ConfirmButton.interactable, Is.False);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
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
                Assert.That(view.Details.text, Does.Contain("rescue berths to reserve: 13"));
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
        public void ConfirmedDepartureShowsFinalReadinessNotAnotherInvitationOrClaimOfSailing()
        {
            var root = new GameObject("M04 readiness confirmed copy");
            M04DepartureReadinessView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var ready = Plan(true);
                int writes = 0;
                view = new M04DepartureReadinessView(root.transform, ready,
                    accepted => { writes++; Assert.That(accepted.DepartureConfirmed, Is.True); },
                    () => Assert.Fail("Confirmed departure cannot navigate back."));

                Assert.That(view.Details.text, Does.Contain("You may confirm this plan"));
                Assert.That(view.ConfirmButton.interactable, Is.True);
                view.ConfirmButton.onClick.Invoke();

                Assert.That(writes, Is.EqualTo(1));
                Assert.That(ready.DepartureConfirmed, Is.True);
                Assert.That(view.ConfirmButton.interactable, Is.False);
                Assert.That(view.Details.text, Does.Contain("Departure readiness has been confirmed"));
                Assert.That(view.Details.text, Does.Contain("does not mean the vessel has already sailed"));
                Assert.That(view.Details.text, Does.Not.Contain("You may confirm this plan"));
                Assert.That(view.ConfirmButton.GetComponentInChildren<UnityEngine.UI.Text>(true).text,
                    Is.EqualTo("Readiness confirmed"));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Details.text, Does.Contain("本航次启航准备已经确认"));
                Assert.That(view.Details.text, Does.Contain("并不表示船只已经实际启航"));
                Assert.That(view.Details.text, Does.Not.Contain("可以申请正式确认"));
                Assert.That(view.ConfirmButton.GetComponentInChildren<UnityEngine.UI.Text>(true).text,
                    Is.EqualTo("准备已确认"));
                view.ConfirmButton.onClick.Invoke();
                Assert.That(writes, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FailedHostDepartureSaveLetsPlayerRetryWithoutDuplicateCallbacks()
        {
            var root = new GameObject("Readiness interrupted host write");
            M04DepartureReadinessView view = null;
            try
            {
                var verified = Plan(true);
                int attempts = 0;
                int durableWrites = 0;
                bool failFirst = true;

                view = new M04DepartureReadinessView(root.transform, verified,
                    accepted =>
                    {
                        attempts++;
                        Assert.That(accepted.DepartureConfirmed, Is.True);
                        Assert.That(view.ConfirmButton.interactable, Is.False);
                        Assert.That(view.BackButton.interactable, Is.False);
                        // A stale/reentrant submit cannot dispatch again.
                        view.ConfirmButton.onClick.Invoke();
                        view.BackButton.onClick.Invoke();
                        if (failFirst)
                            throw new InvalidOperationException("Simulated failed Makinom save");
                        durableWrites++;
                    }, () => Assert.Fail("Navigation must not occur while saving."));

                Assert.Throws<InvalidOperationException>(() =>
                    view.ConfirmButton.onClick.Invoke());
                Assert.That(attempts, Is.EqualTo(1));
                Assert.That(durableWrites, Is.Zero);
                Assert.That(verified.DepartureConfirmed, Is.False);
                Assert.That(view.ConfirmButton.interactable, Is.True);
                Assert.That(view.BackButton.interactable, Is.True);

                failFirst = false;
                view.ConfirmButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(durableWrites, Is.EqualTo(1));
                Assert.That(verified.DepartureConfirmed, Is.True);
                Assert.That(view.ConfirmButton.interactable, Is.False);
                view.ConfirmButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(durableWrites, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BackTransitionCannotReenterConfirmAndCanRetryOnHostFailure()
        {
            var root = new GameObject("M04 readiness back retry");
            M04DepartureReadinessView view = null;
            try
            {
                int returned = 0;
                int confirmed = 0;
                bool failFirst = true;
                var ready = Plan(true);
                view = new M04DepartureReadinessView(root.transform, ready,
                    _ => confirmed++,
                    () =>
                    {
                        returned++;
                        Assert.That(view.BackButton.interactable, Is.False);
                        Assert.That(view.ConfirmButton.interactable, Is.False);
                        view.BackButton.onClick.Invoke();
                        view.ConfirmButton.onClick.Invoke();
                        Assert.Throws<InvalidOperationException>(() =>
                            view.SetConfirmedManifest(Plan(true)));
                        if (failFirst)
                            throw new InvalidOperationException("Interrupted scene transition");
                    });
                Assert.That(view.BackButton.interactable, Is.True);
                Assert.That(view.ConfirmButton.interactable, Is.True);

                Assert.Throws<InvalidOperationException>(() => view.BackButton.onClick.Invoke());
                Assert.That(returned, Is.EqualTo(1));
                Assert.That(confirmed, Is.Zero);
                Assert.That(ready.DepartureConfirmed, Is.False);
                Assert.That(view.BackButton.interactable, Is.True);
                Assert.That(view.ConfirmButton.interactable, Is.True);

                failFirst = false;
                view.BackButton.onClick.Invoke();
                Assert.That(returned, Is.EqualTo(2));
                Assert.That(confirmed, Is.Zero);
                Assert.That(ready.DepartureConfirmed, Is.False);
                Assert.That(view.BackButton.interactable, Is.False);
                Assert.That(view.ConfirmButton.interactable, Is.False);
                view.BackButton.onClick.Invoke();
                view.ConfirmButton.onClick.Invoke();
                Assert.That(returned, Is.EqualTo(2));
                Assert.That(confirmed, Is.Zero);
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
