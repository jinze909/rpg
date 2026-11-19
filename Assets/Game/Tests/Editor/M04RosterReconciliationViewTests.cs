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
            try
            {
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
                Assert.That(view.FinishButton.interactable, Is.False);
                view.FinishButton.onClick.Invoke();
                Assert.That(completed, Is.Zero,
                    "Invoking a disabled button must not bypass the capacity guard.");

                view.SetVerifiedBerths(0);
                Assert.That(view.FinishButton.interactable, Is.False);
                view.SetVerifiedBerths(1);
                view.UnbookButton.onClick.Invoke();
                Assert.That(ledger.BookedCount, Is.EqualTo(1));
                Assert.That(view.FinishButton.interactable, Is.True);

                view.FinishButton.onClick.Invoke();
                Assert.That(completed, Is.EqualTo(1));
                view.FinishButton.onClick.Invoke();
                Assert.That(completed, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
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
    }
}
