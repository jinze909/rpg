using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04RosterReconciliationViewTests
    {
        [Test]
        public void DiscrepancyBlocksFinishUntilResolvedAndPreventsDirectClickBypass()
        {
            var root = new GameObject("Roster discrepancy view");
            M04RosterReconciliationView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var ledger = new M04RosterLedger();
                ledger.AddEntry("roll-01", "Memorial listed person", M04RosterStatus.ListedDead);

                int finished = 0;
                view = new M04RosterReconciliationView(
                    root.transform, ledger, 2, _ => finished++, () => { });

                Assert.That(view.ConfirmDockButton.interactable, Is.True);
                Assert.That(view.ResolveRollButton.interactable, Is.False);
                Assert.That(view.BookButton.interactable, Is.False);

                view.ConfirmDockButton.onClick.Invoke();
                Assert.That(ledger.UnresolvedDiscrepancyCount, Is.EqualTo(1));
                Assert.That(view.ResolveRollButton.interactable, Is.True);
                Assert.That(view.FinishButton.interactable, Is.False);
                Assert.That(view.SelectedDetails.text, Does.Contain("Discrepancy"));

                // Direct onClick invocation must not bypass the unresolved discrepancy guard.
                view.FinishButton.onClick.Invoke();
                Assert.That(finished, Is.Zero);

                view.ResolveRollButton.onClick.Invoke();
                Assert.That(ledger.UnresolvedDiscrepancyCount, Is.Zero);
                Assert.That(view.FinishButton.interactable, Is.True);

                view.FinishButton.onClick.Invoke();
                Assert.That(finished, Is.EqualTo(1));
                view.FinishButton.onClick.Invoke();
                Assert.That(finished, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeadRollCorrectionMustPrecedeBookingInThePlayerUi()
        {
            var root = new GameObject("Roster correction booking view");
            M04RosterReconciliationView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var ledger = new M04RosterLedger();
                ledger.AddEntry("roll-01", "Survivor on dead roll", M04RosterStatus.ListedDead);
                view = new M04RosterReconciliationView(
                    root.transform, ledger, 1, _ => { }, () => { });

                view.ConfirmDockButton.onClick.Invoke();
                Assert.That(ledger.UnresolvedDiscrepancyCount, Is.EqualTo(1));
                Assert.That(view.ResolveRollButton.interactable, Is.True);
                Assert.That(view.BookButton.interactable, Is.False);
                Assert.That(view.SelectedDetails.text, Does.Contain("before booking a berth"));

                // Tests can invoke onClick on disabled controls; the action
                // boundary must enforce the same rule as the visual state.
                view.BookButton.onClick.Invoke();
                Assert.That(ledger.BookedCount, Is.Zero);
                Assert.That(ledger.GetEntry("roll-01").Status,
                    Is.EqualTo(M04RosterStatus.ConfirmedAtDock));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.BookButton.interactable, Is.False);
                Assert.That(view.SelectedDetails.text, Does.Contain("预约铺位"));
                view.ResolveRollButton.onClick.Invoke();
                Assert.That(ledger.UnresolvedDiscrepancyCount, Is.Zero);
                Assert.That(view.BookButton.interactable, Is.True);
                view.BookButton.onClick.Invoke();
                Assert.That(ledger.BookedCount, Is.EqualTo(1));
                Assert.That(view.BookButton.interactable, Is.False);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ConfirmedDockCountDoesNotDisappearAfterBookingDecliningOrReconsidering()
        {
            var root = new GameObject("M04 confirmed dock summary");
            M04RosterReconciliationView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var ledger = new M04RosterLedger();
                ledger.AddEntry("dock-1", "First confirmed returnee",
                    M04RosterStatus.ConfirmedAtDock);
                ledger.AddEntry("dock-2", "Second confirmed returnee",
                    M04RosterStatus.ConfirmedAtDock);
                ledger.AddEntry("dead-1", "Person only listed dead",
                    M04RosterStatus.ListedDead);
                view = new M04RosterReconciliationView(root.transform, ledger, 1,
                    _ => { }, () => { });

                Assert.That(view.Summary.text, Does.Contain("confirmed at dock: 2"));
                Assert.That(view.Summary.text, Does.Contain("unbooked confirmed: 2"));
                Assert.That(view.Summary.text, Does.Contain("booked berths: 0/1"));

                view.BookButton.onClick.Invoke();
                Assert.That(view.Summary.text, Does.Contain("confirmed at dock: 2"));
                Assert.That(view.Summary.text, Does.Contain("unbooked confirmed: 1"));
                Assert.That(view.Summary.text, Does.Contain("booked berths: 1/1"));

                view.DeclineButton.onClick.Invoke();
                Assert.That(ledger.DeclinedCount, Is.EqualTo(1));
                Assert.That(view.Summary.text, Does.Contain("confirmed at dock: 2"));
                Assert.That(view.Summary.text, Does.Contain("unbooked confirmed: 1"));
                Assert.That(view.Summary.text, Does.Contain("booked berths: 0/1"));
                Assert.That(view.Summary.text, Does.Contain("Declined: 1"));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Summary.text, Does.Contain("码头已核：2"));
                Assert.That(view.Summary.text, Does.Contain("尚未占铺位：1"));
                Assert.That(view.Summary.text, Does.Contain("放弃本趟：1"));

                view.UnbookButton.onClick.Invoke();
                Assert.That(ledger.DeclinedCount, Is.Zero);
                Assert.That(view.Summary.text, Does.Contain("码头已核：2"));
                Assert.That(view.Summary.text, Does.Contain("尚未占铺位：2"));

                view.NextButton.onClick.Invoke();
                view.NextButton.onClick.Invoke();
                view.ConfirmDockButton.onClick.Invoke();
                Assert.That(ledger.UnresolvedDiscrepancyCount, Is.EqualTo(1));
                Assert.That(view.Summary.text, Does.Contain("码头已核：3"));
                Assert.That(view.Summary.text, Does.Contain("尚未占铺位：3"));
                Assert.That(view.FinishButton.interactable, Is.False);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BookingAndUnbookingRespectVerifiedBerthCapAndHostUpdates()
        {
            var root = new GameObject("Roster berth cap view");
            M04RosterReconciliationView view = null;
            try
            {
                var ledger = new M04RosterLedger();
                ledger.AddEntry("dock-01", "Confirmed 1", M04RosterStatus.ConfirmedAtDock);
                ledger.AddEntry("dock-02", "Confirmed 2", M04RosterStatus.ConfirmedAtDock);

                view = new M04RosterReconciliationView(
                    root.transform, ledger, 1, _ => { }, () => { });

                Assert.That(view.BookButton.interactable, Is.True);
                view.BookButton.onClick.Invoke();
                Assert.That(ledger.BookedCount, Is.EqualTo(1));
                Assert.That(view.UnbookButton.interactable, Is.True);

                view.NextButton.onClick.Invoke();
                Assert.That(view.SelectedIndex, Is.EqualTo(1));
                Assert.That(view.BookButton.interactable, Is.False);
                view.BookButton.onClick.Invoke();
                Assert.That(ledger.BookedCount, Is.EqualTo(1));

                Assert.Throws<ArgumentOutOfRangeException>(() => view.SetVerifiedBerths(-1));
                view.SetVerifiedBerths(2);
                Assert.That(view.BookButton.interactable, Is.True);
                view.BookButton.onClick.Invoke();
                Assert.That(ledger.BookedCount, Is.EqualTo(2));

                view.UnbookButton.onClick.Invoke();
                Assert.That(ledger.BookedCount, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LoweredVerifiedBerthsBlocksCompletionUntilExcessBookingIsRemoved()
        {
            var root = new GameObject("Roster capacity reduction view");
            M04RosterReconciliationView view = null;
            string previousLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var ledger = new M04RosterLedger();
                ledger.AddEntry("dock-01", "Confirmed 1", M04RosterStatus.ConfirmedAtDock);
                ledger.AddEntry("dock-02", "Confirmed 2", M04RosterStatus.ConfirmedAtDock);
                int completed = 0;
                view = new M04RosterReconciliationView(
                    root.transform, ledger, 2, _ => completed++, () => { });
                view.BookButton.onClick.Invoke();
                view.NextButton.onClick.Invoke();
                view.BookButton.onClick.Invoke();
                Assert.That(ledger.BookedCount, Is.EqualTo(2));
                Assert.That(view.FinishButton.interactable, Is.True);

                // The host can discover fewer actual berths after reservations.
                // Do not silently publish an overbooked reconciliation.
                view.SetVerifiedBerths(1);
                Assert.That(view.Summary.text, Does.Contain("2/1"));
                Assert.That(view.Summary.text, Does.Contain("Overbooked: unbook 1 berth before finishing."));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Summary.text, Does.Contain("铺位不足：请先取消 1 个占位"));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(view.FinishButton.interactable, Is.False);
                view.FinishButton.onClick.Invoke();
                Assert.That(completed, Is.Zero,
                    "Invoking a disabled button must not bypass the capacity guard.");

                view.SetVerifiedBerths(0);
                Assert.That(view.FinishButton.interactable, Is.False);
                view.SetVerifiedBerths(1);
                view.UnbookButton.onClick.Invoke();
                Assert.That(ledger.BookedCount, Is.EqualTo(1));
                Assert.That(view.Summary.text, Does.Not.Contain("Overbooked"));
                Assert.That(view.FinishButton.interactable, Is.True);

                view.FinishButton.onClick.Invoke();
                Assert.That(completed, Is.EqualTo(1));
                view.FinishButton.onClick.Invoke();
                Assert.That(completed, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DecliningFreesBerthAndCyclesEntriesSafely()
        {
            var root = new GameObject("Roster decline view");
            M04RosterReconciliationView view = null;
            try
            {
                var ledger = new M04RosterLedger();
                ledger.AddEntry("dock-01", "Confirmed 1", M04RosterStatus.ConfirmedAtDock);
                ledger.AddEntry("dock-02", "Confirmed 2", M04RosterStatus.ConfirmedAtDock);

                int backCount = 0;
                view = new M04RosterReconciliationView(
                    root.transform, ledger, 1, _ => { }, () => backCount++);

                view.BookButton.onClick.Invoke();
                Assert.That(ledger.BookedCount, Is.EqualTo(1));

                view.DeclineButton.onClick.Invoke();
                Assert.That(ledger.BookedCount, Is.Zero);
                Assert.That(ledger.DeclinedCount, Is.EqualTo(1));
                Assert.That(view.BookButton.interactable, Is.False);

                view.PrevButton.onClick.Invoke();
                Assert.That(view.SelectedIndex, Is.EqualTo(1));
                Assert.That(view.BookButton.interactable, Is.True);
                view.BookButton.onClick.Invoke();
                Assert.That(ledger.BookedCount, Is.EqualTo(1));

                view.BackButton.onClick.Invoke();
                Assert.That(backCount, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlayerCanUndoDeclineBeforeFinishingWithoutStealingABerth()
        {
            var root = new GameObject("Roster undo decline view");
            M04RosterReconciliationView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var ledger = new M04RosterLedger();
                ledger.AddEntry("dock-a", "First returnee", M04RosterStatus.ConfirmedAtDock);
                ledger.AddEntry("dock-b", "Second returnee", M04RosterStatus.ConfirmedAtDock);
                view = new M04RosterReconciliationView(
                    root.transform, ledger, 1, _ => { }, () => { });

                view.BookButton.onClick.Invoke();
                view.DeclineButton.onClick.Invoke();
                Assert.That(ledger.DeclinedCount, Is.EqualTo(1));
                Assert.That(view.UnbookButton.interactable, Is.True);
                Assert.That(view.UnbookButton.GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Is.EqualTo("Reconsider voyage"));

                view.NextButton.onClick.Invoke();
                view.BookButton.onClick.Invoke();
                Assert.That(ledger.BookedCount, Is.EqualTo(1));
                view.PrevButton.onClick.Invoke();

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.UnbookButton.GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Is.EqualTo("撤销放弃"));
                view.UnbookButton.onClick.Invoke();
                Assert.That(ledger.GetEntry("dock-a").Status,
                    Is.EqualTo(M04RosterStatus.ConfirmedAtDock));
                Assert.That(ledger.DeclinedCount, Is.Zero);
                Assert.That(ledger.BookedCount, Is.EqualTo(1));
                Assert.That(view.BookButton.interactable, Is.False,
                    "The other returnee still owns the only verified berth.");
                Assert.That(view.UnbookButton.interactable, Is.False);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ChangingLocaleUpdatesHeadingsStatusesAndKeepsDomainGuards()
        {
            var root = new GameObject("Roster locale view");
            M04RosterReconciliationView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var ledger = new M04RosterLedger();
                ledger.AddEntry("roll-01", "Listed person", M04RosterStatus.ListedDead);
                ledger.MarkConfirmedAtDock("roll-01");

                view = new M04RosterReconciliationView(
                    root.transform, ledger, 1, _ => { }, () => { });

                Assert.That(view.Summary.text, Does.Contain("pending dead-roll discrepancies: 1"));
                Assert.That(view.SelectedDetails.text, Does.Contain("Confirmed at dock"));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Summary.text, Does.Contain("待更正阵亡差异：1"));
                Assert.That(view.SelectedDetails.text, Does.Contain("码头已核实"));
                Assert.That(view.FinishButton.interactable, Is.False);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FailedHostSaveRestoresFinishSoThePlayerCanRetry()
        {
            var root = new GameObject("Roster retry after save failure");
            M04RosterReconciliationView view = null;
            try
            {
                var ledger = new M04RosterLedger();
                ledger.AddEntry("dock-01", "Confirmed returnee", M04RosterStatus.ConfirmedAtDock);
                int attempts = 0;
                int delivered = 0;
                bool failFirst = true;

                view = new M04RosterReconciliationView(
                    root.transform, ledger, 1, _ =>
                    {
                        attempts++;
                        if (failFirst)
                            throw new InvalidOperationException("Simulated interrupted host save");
                        delivered++;
                    }, () => { });

                Assert.That(view.FinishButton.interactable, Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    view.FinishButton.onClick.Invoke());
                Assert.That(attempts, Is.EqualTo(1));
                Assert.That(delivered, Is.Zero);
                Assert.That(view.FinishButton.interactable, Is.True,
                    "An interrupted host save must allow retrying the same roster.");

                failFirst = false;
                view.FinishButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(delivered, Is.EqualTo(1));
                Assert.That(view.FinishButton.interactable, Is.False);
                view.FinishButton.onClick.Invoke();
                Assert.That(delivered, Is.EqualTo(1),
                    "Successful completion must still deliver exactly once.");
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BackNavigationDisablesOtherActionsAndCanRetryAfterHostFailure()
        {
            var root = new GameObject("Roster back transition guard");
            M04RosterReconciliationView view = null;
            try
            {
                var ledger = new M04RosterLedger();
                ledger.AddEntry("dock-01", "Confirmed passenger",
                    M04RosterStatus.ConfirmedAtDock);
                int finishes = 0;
                int backAttempts = 0;
                bool failFirst = true;

                view = new M04RosterReconciliationView(
                    root.transform, ledger, 1, _ => finishes++, () =>
                    {
                        backAttempts++;
                        int previousCapacity = view.VerifiedBerths;
                        Assert.Throws<InvalidOperationException>(() =>
                            view.SetVerifiedBerths(previousCapacity + 1));
                        Assert.That(view.VerifiedBerths, Is.EqualTo(previousCapacity),
                            "An in-flight Back transition cannot change verified capacity.");
                        if (failFirst)
                            throw new InvalidOperationException("Interrupted navigation");

                        Assert.That(view.BackButton.interactable, Is.False);
                        Assert.That(view.FinishButton.interactable, Is.False);
                        Assert.That(view.BookButton.interactable, Is.False);
                        // Direct onClick invocation can bypass a disabled
                        // button; reentrant submits must still be rejected.
                        view.BackButton.onClick.Invoke();
                        view.FinishButton.onClick.Invoke();
                        view.BookButton.onClick.Invoke();
                    });

                Assert.Throws<InvalidOperationException>(() =>
                    view.BackButton.onClick.Invoke());
                Assert.That(backAttempts, Is.EqualTo(1));
                Assert.That(view.BackButton.interactable, Is.True,
                    "Navigation failure should let the player try again.");
                Assert.That(view.BookButton.interactable, Is.True);
                Assert.That(finishes, Is.Zero);
                Assert.That(ledger.BookedCount, Is.Zero);
                // The same host update is allowed once an unsuccessful
                // transition has restored the panel.
                view.SetVerifiedBerths(2);
                Assert.That(view.VerifiedBerths, Is.EqualTo(2));

                failFirst = false;
                view.BackButton.onClick.Invoke();
                Assert.That(backAttempts, Is.EqualTo(2));
                Assert.That(finishes, Is.Zero);
                Assert.That(ledger.BookedCount, Is.Zero);
                view.BackButton.onClick.Invoke();
                Assert.That(backAttempts, Is.EqualTo(2),
                    "A successful back transition must dispatch only once.");
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EmptyLedgerAndViewportAspectChangesStaySafeAndFinishOnce()
        {
            var root = new GameObject("Roster empty view");
            M04RosterReconciliationView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var ledger = new M04RosterLedger();
                int finished = 0;

                view = new M04RosterReconciliationView(
                    root.transform, ledger, 0, _ => finished++, () => { });

                Assert.That(view.SelectedDetails.text, Does.Contain("No roster entries"));
                Assert.That(view.PrevButton.interactable, Is.False);
                Assert.That(view.NextButton.interactable, Is.False);
                Assert.That(view.BookButton.interactable, Is.False);
                Assert.That(view.FinishButton.interactable, Is.True);

                foreach (float ratio in new[] { 9f / 16f, 32f / 9f, 16f / 9f })
                    view.LayoutForViewportAspect(ratio);

                view.FinishButton.onClick.Invoke();
                Assert.That(finished, Is.EqualTo(1));
                Assert.Throws<InvalidOperationException>(() => view.SetVerifiedBerths(3));
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
            var root = new GameObject("Roster destroyed view");
            M04RosterReconciliationView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var ledger = new M04RosterLedger();
                ledger.AddEntry("dock-01", "Confirmed returnee", M04RosterStatus.ConfirmedAtDock);
                view = new M04RosterReconciliationView(
                    root.transform, ledger, 1, _ => { }, () => { });

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
