using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST05TornFactsOpenGateTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void HostRefusesOpeningWhenAnyUnconfirmedOrTornFactKeyRemainsPersisted()
        {
            var root = new GameObject("ST05 torn facts open gate");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var tornCases = new (string Key, int Value)[]
            {
                (ST05TwoDoorsFacts.HomeConfirmedKey, 2),
                (ST05TwoDoorsFacts.HomeIntentKey, 1210),
                (ST05TwoDoorsFacts.BedKey, (int)ST05BedResolution.RemoveDoorFrame),
                (ST05TwoDoorsFacts.HomeArrangementKey, (int)ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable),
                (ST05TwoDoorsFacts.MutualTrialConsentKey, 1),
                (ST05TwoDoorsFacts.AssignmentConfirmedKey, 1),
                (ST05TwoDoorsFacts.AssignmentKey, (int)ST05ChengAssignment.StayLocal),
                (ST05TwoDoorsFacts.OutsideWorkStartedKey, 1),
                (ST05TwoDoorsFacts.LetterStatusKey, (int)ST05LetterStatus.Sent),
                (ST05TwoDoorsFacts.YuheArrivedKey, 1),
                (ST05TwoDoorsFacts.BreadBroughtKey, 1),
                (ST05TwoDoorsFacts.ChengArrivedKey, 1),
                (ST05TwoDoorsFacts.MapBroughtKey, 1),
                (ST05TwoDoorsFacts.YuheArrivalIntentKey, 1),
                (ST05TwoDoorsFacts.ChengArrivalIntentKey, 2)
            };

            try
            {
                foreach (var torn in tornCases)
                {
                    values.Clear();
                    values[torn.Key] = torn.Value;

                    using (var host = new ST05TwoDoorsHost(
                        root.transform, () => true, () => true, () => true,
                        () => true, () => true, () => true, () => true, read, write))
                    {
                        Assert.That(host.TryOpen(), Is.False,
                            $"ST05TwoDoorsHost should refuse opening when torn key '{torn.Key}' = {torn.Value} persists.");
                        Assert.That(host.ActiveView, Is.Null);
                        Assert.That(host.Session, Is.Null);
                    }
                }

                values.Clear();
                using (var cleanHost = new ST05TwoDoorsHost(
                    root.transform, () => true, () => true, () => true,
                    () => true, () => true, () => true, () => true, read, write))
                {
                    Assert.That(cleanHost.TryOpen(), Is.True);
                    Assert.That(cleanHost.ActiveView, Is.Not.Null);
                    Assert.That(cleanHost.Session, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HostResumesConfirmedHomeWithoutReplayingHouseholdDecision()
        {
            var root = new GameObject("ST05 confirmed home open gate");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                var home = new ST05HouseholdRecord(
                    ST05BedResolution.ConvertBedToCourtyardBenches,
                    ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable,
                    mutualTrialConsent: false);
                Assert.That(ST05TwoDoorsFacts.TryRecordHome(home, read, write), Is.True);
                var before = new Dictionary<string, int>(values);
                bool atYard = false;
                bool termsReady = false;
                int writes = 0;
                using (var host = new ST05TwoDoorsHost(
                    root.transform, () => atYard, () => false, () => false,
                    () => false, () => false, () => false, () => termsReady,
                    read, (k, value) => { writes++; write(k, value); }))
                {
                    Assert.That(host.TryOpen(), Is.False);
                    atYard = true;
                    Assert.That(host.TryOpen(), Is.True);
                    Assert.That(host.Session.Phase, Is.EqualTo(ST05TwoDoorsPhase.AwaitChengOwnJobDecision));
                    Assert.That(host.Session.CompletedHome.Value.Bed, Is.EqualTo(home.Bed));
                    Assert.That(host.Session.CompletedHome.Value.Arrangement, Is.EqualTo(home.Arrangement));
                    Assert.That(writes, Is.Zero);
                    Assert.That(host.ActiveView.ChengStayLocalButton.gameObject.activeSelf, Is.True);
                    host.ActiveView.ChengStayLocalButton.onClick.Invoke();
                    Assert.That(host.IsCompleted, Is.False);
                    Assert.That(writes, Is.Zero);

                    termsReady = true;
                    host.ActiveView.ChengStayLocalButton.onClick.Invoke();
                    Assert.That(host.IsCompleted, Is.True);
                    Assert.That(host.ActiveView, Is.Null);
                    Assert.That(writes, Is.EqualTo(2));
                    foreach (var pair in before)
                        Assert.That(values[pair.Key], Is.EqualTo(pair.Value), pair.Key);
                    Assert.That(host.TryOpen(), Is.False);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
