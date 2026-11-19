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
        public void ReadinessShowsActualDraftAndOnlyTheChosenRoutesVerifiedAuthorization()
        {
            M04DepartureManifest ReadyOn(M03VoyageChoice route, bool stamp, bool flag)
            {
                return new M04DepartureManifest(route, "Inspected hull", "Named pilot", 3,
                    "Shallow tender", true, 18, 13, "Landing receiver", stamp, flag);
            }

            string officialEnglish = M04DepartureReadinessCopy.Describe(
                ReadyOn(M03VoyageChoice.HanQiaoOfficial, true, false), false);
            Assert.That(officialEnglish, Does.Contain("Draft: checked"));
            Assert.That(officialEnglish, Does.Contain("Official registration: stamped"));
            Assert.That(officialEnglish, Does.Not.Contain("Public escort flag: accepted"));
            Assert.That(officialEnglish, Does.Not.Contain("has already sailed"));

            string mixedChinese = M04DepartureReadinessCopy.Describe(
                ReadyOn(M03VoyageChoice.MixedWithOfficial, true, false), true);
            Assert.That(mixedChinese, Does.Contain("吃水：已核查"));
            Assert.That(mixedChinese, Does.Contain("官署登记：已盖印"));
            Assert.That(mixedChinese, Does.Not.Contain("公开护航旗：已接受"));

            string escortedEnglish = M04DepartureReadinessCopy.Describe(
                ReadyOn(M03VoyageChoice.LuJunEscort, false, true), false);
            Assert.That(escortedEnglish, Does.Contain("Public escort flag: accepted"));
            Assert.That(escortedEnglish, Does.Not.Contain("Official registration: stamped"));

            string civilianEnglish = M04DepartureReadinessCopy.Describe(
                ReadyOn(M03VoyageChoice.QiQueMerchant, false, false), false);
            Assert.That(civilianEnglish, Does.Contain("Draft: checked"));
            Assert.That(civilianEnglish, Does.Not.Contain("Official registration: stamped"));
            Assert.That(civilianEnglish, Does.Not.Contain("Public escort flag: accepted"));

            // An M03 route choice cannot invent approval if the voyage is blocked.
            string unapproved = M04DepartureReadinessCopy.Describe(
                ReadyOn(M03VoyageChoice.HanQiaoOfficial, false, false), false);
            Assert.That(unapproved, Does.Contain("Official passage lacks a valid stamp"));
            Assert.That(unapproved, Does.Not.Contain("Official registration: stamped"));
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

        [Test]
        public void DestroyedDeparturePanelCannotPersistOrNavigateFromStaleButtons()
        {
            var root = new GameObject("Readiness unmounted during confirmation");
            M04DepartureReadinessView view = null;
            try
            {
                int confirmed = 0;
                int returned = 0;
                var ready = Plan(true);
                view = new M04DepartureReadinessView(root.transform, ready,
                    _ => confirmed++, () => returned++);
                var oldConfirm = view.ConfirmButton.onClick;
                var oldBack = view.BackButton.onClick;

                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                // UnityEvents can still be held by a host after their UGUI
                // components have been destroyed. No stale event may sail.
                Assert.DoesNotThrow(() => oldConfirm.Invoke());
                Assert.DoesNotThrow(() => oldBack.Invoke());
                Assert.That(confirmed, Is.Zero);
                Assert.That(returned, Is.Zero);
                Assert.That(ready.DepartureConfirmed, Is.False);
                Assert.Throws<ObjectDisposedException>(() =>
                    view.SetConfirmedManifest(Plan(true)));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(9f / 16f));
            }
            finally
            {
                view?.Dispose();
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedReadinessDetailCannotBreakLocaleOrCommitDeparture()
        {
            var root = new GameObject("Readiness partial child unload");
            M04DepartureReadinessView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                var ready = Plan(true);
                int confirmed = 0;
                int returned = 0;
                view = new M04DepartureReadinessView(root.transform, ready,
                    _ => confirmed++, () => returned++);
                var oldConfirm = view.ConfirmButton.onClick;
                var oldBack = view.BackButton.onClick;
                UnityEngine.Object.DestroyImmediate(view.Details.gameObject);

                SoundwellUiLocalization.SetLanguage(
                    savedLanguage == SoundwellUiLocalization.Chinese
                        ? SoundwellUiLocalization.English
                        : SoundwellUiLocalization.Chinese);
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(32f / 9f));
                oldConfirm.Invoke();
                oldBack.Invoke();
                Assert.That(confirmed, Is.Zero);
                Assert.That(returned, Is.Zero);
                Assert.That(ready.DepartureConfirmed, Is.False);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedViewCannotReceiveStaleLocaleChangesOrFailOnDispose()
        {
            var root = new GameObject("Readiness destroyed");
            M04DepartureReadinessView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                view = new M04DepartureReadinessView(root.transform, Plan(true),
                    _ => { }, () => { });

                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(9f / 16f));
                Assert.DoesNotThrow(() => view.Dispose());
                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
