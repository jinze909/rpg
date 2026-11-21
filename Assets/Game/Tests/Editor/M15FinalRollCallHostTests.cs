using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M15FinalRollCallHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void HostCannotOpenBeforeCeremonyReadyWithNegativeCountOrNonZeroConfirmedKey()
        {
            var root = new GameObject("M15 host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool ready = false;
            int consented = 3;
            M15FinalRollCallHost host = null;
            try
            {
                host = new M15FinalRollCallHost(
                    root.transform, () => ready, () => false, () => consented, null, read, write);
                Assert.That(host.TryOpen(), Is.False);

                ready = true;
                consented = -1;
                Assert.That(host.TryOpen(), Is.False);

                consented = 3;
                values[M15FinalRollCallFacts.ConfirmedKey] = 2;
                Assert.That(host.TryOpen(), Is.False);

                values[M15FinalRollCallFacts.ConfirmedKey] = 0;
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.IsCompleted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PublicRollCallFlowCarvesConsentedNamesPersistsOnceAndBlocksReopen()
        {
            var root = new GameObject("M15 host public carve");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M15FinalRollCallHost host = null;
            M15FinalRollCallHost duplicate = null;
            try
            {
                host = new M15FinalRollCallHost(
                    root.transform, () => true, () => false, () => 4, () => null, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.HanQiaoReaderButton.onClick.Invoke();
                view.PublicTrueNameButton.onClick.Invoke();
                view.CarveNamesSteleButton.onClick.Invoke();

                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(M15FinalRollCallFacts.TryRead(
                    read, out M15FinalRollCallRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.Reader, Is.EqualTo(M15RollCallReader.HanQiao));
                Assert.That(saved.Value.PensionClosure, Is.EqualTo(M15PensionClosure.StoppedAndReconciled));
                Assert.That(saved.Value.SteleInscription, Is.EqualTo(M15SteleInscription.CarveConsentedVerifiedNames));
                Assert.That(saved.Value.XiaoqiCalledByNumberSeventyTwo, Is.True);

                duplicate = new M15FinalRollCallHost(
                    root.transform, () => true, () => false, () => 4, null, read, write);
                Assert.That(duplicate.TryOpen(), Is.False);
            }
            finally
            {
                duplicate?.Dispose();
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedWriteAndCommitThenThrowRecoverWithoutDuplicateWrites()
        {
            var root = new GameObject("M15 host retry");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == M15FinalRollCallFacts.SteleInscriptionKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == M15FinalRollCallFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };
            M15FinalRollCallHost host = null;
            try
            {
                host = new M15FinalRollCallHost(
                    root.transform, () => true, () => true, () => 0, () => "陈安", read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.AuntJiangReaderButton.onClick.Invoke();
                view.PrivateCustodianButton.onClick.Invoke();
                Assert.That(view.CarveNamesSteleButton.interactable, Is.False);

                Assert.Throws<InvalidOperationException>(() =>
                    view.AddReturnedAliveSteleButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);

                Assert.Throws<InvalidOperationException>(() =>
                    view.AddReturnedAliveSteleButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(confirmCommits, Is.EqualTo(1));

                view.AddReturnedAliveSteleButton.onClick.Invoke();
                Assert.That(confirmCommits, Is.EqualTo(1));
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CeremonyWithdrawalAtCommitTimeBlocksWriteUntilReadyAgain()
        {
            var root = new GameObject("M15 host readiness");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool ready = true;
            M15FinalRollCallHost host = null;
            try
            {
                host = new M15FinalRollCallHost(
                    root.transform, () => ready, () => false, () => 2, null, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.ProtagonistReaderButton.onClick.Invoke();
                view.PublicTrueNameButton.onClick.Invoke();

                ready = false;
                view.KeepOriginalSteleButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);

                ready = true;
                view.KeepOriginalSteleButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposalCleansUpViewAndIgnoresStaleSteleButtonClicks()
        {
            var root = new GameObject("M15 host dispose");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M15FinalRollCallHost host = null;
            try
            {
                host = new M15FinalRollCallHost(
                    root.transform, () => true, () => false, () => 1, null, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.ProtagonistReaderButton.onClick.Invoke();
                view.PrivateCustodianButton.onClick.Invoke();
                var leaveBlankBtn = view.LeaveBlankSteleButton;

                host.Dispose();
                leaveBlankBtn.onClick.Invoke();
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.IsCompleted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
