using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M09NightCrossingOrphanedStateTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void ReachJetty(M09EastShoreNightCrossingView view)
        {
            view.ListenOrProceedButton.onClick.Invoke();
            view.ListenOrProceedButton.onClick.Invoke();
            view.ListenOrProceedButton.onClick.Invoke();
        }

        [Test]
        public void NightCrossingHostRefusesTryOpenWhenTornM09PayloadOrOrphanedAct3FactsExist()
        {
            var root = new GameObject("M09 night crossing torn/act3 open guard");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            M09EastShoreNightCrossingHost host = null;
            try
            {
                host = new M09EastShoreNightCrossingHost(
                    root.transform, () => true, () => 4, () => 2,
                    _ => true, _ => true, read);

                foreach (string key in new[]
                {
                    M09BerthTableFacts.PendingSignatureKey,
                    M09BerthTableFacts.TotalBerthsKey,
                    M09BerthTableFacts.RuiyaOutcomeKey,
                    M09BerthTableFacts.ShipwrightFeePaidKey,
                    M10AssemblyFacts.HanLedgerUseKey,
                    M16EpilogueFacts.ConfirmedKey
                })
                {
                    values[key] = 1;
                    Assert.That(host.TryOpen(), Is.False, key);
                    Assert.That(host.ActiveView, Is.Null, key);
                    values[key] = 0;
                }

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView, Is.Not.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NightCrossingHostRefusesLiveBoardingAndCommitIfTornM09OrAct3FactsAppearAfterOpen()
        {
            var root = new GameObject("M09 night crossing live torn/act3 guard");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int boardChecks = 0;
            int commits = 0;
            M09EastShoreNightCrossingHost host = null;
            try
            {
                host = new M09EastShoreNightCrossingHost(
                    root.transform, () => true, () => 4, () => 2,
                    count =>
                    {
                        boardChecks++;
                        return count == 2;
                    },
                    _ =>
                    {
                        commits++;
                        return true;
                    },
                    read);

                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                ReachJetty(view);
                view.ListenOrProceedButton.onClick.Invoke();
                Assert.That(host.Session.Stage,
                    Is.EqualTo(M09NightCrossingStage.AwaitingActualBoarding));

                values[M09BerthTableFacts.PendingSignatureKey] = 42;
                view.ListenOrProceedButton.onClick.Invoke();
                Assert.That(boardChecks, Is.Zero);
                Assert.That(host.Session.IsComplete, Is.False);
                values[M09BerthTableFacts.PendingSignatureKey] = 0;

                values[M16EpilogueFacts.StandingSpotKey] = (int)M16StandingSpot.PierSeaBreeze;
                view.ListenOrProceedButton.onClick.Invoke();
                Assert.That(boardChecks, Is.Zero);
                Assert.That(host.Session.IsComplete, Is.False);
                values[M16EpilogueFacts.StandingSpotKey] = 0;

                view.ListenOrProceedButton.onClick.Invoke();
                Assert.That(boardChecks, Is.EqualTo(1));
                Assert.That(host.Session.IsComplete, Is.True);

                values[M10AssemblyFacts.ConfirmedKey] = 1;
                Assert.Throws<InvalidOperationException>(() =>
                    view.FinishButton.onClick.Invoke());
                Assert.That(commits, Is.Zero);
                Assert.That(host.IsCommitted, Is.False);
                values[M10AssemblyFacts.ConfirmedKey] = 0;

                view.FinishButton.onClick.Invoke();
                Assert.That(commits, Is.EqualTo(1));
                Assert.That(host.IsCommitted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
