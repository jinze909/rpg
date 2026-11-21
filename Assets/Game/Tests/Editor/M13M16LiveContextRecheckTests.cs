using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M13M16LiveContextRecheckTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void M13HostBlocksDepartureCommitWhenSafetyPrepStatusChangesBeforeConfirm()
        {
            var root = new GameObject("M13 live safety prep recheck");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool safetyPrep = true;

            try
            {
                using (var host = new M13DepartureRiskHost(
                    root.transform, () => true, () => safetyPrep, () => true, read, write))
                {
                    Assert.That(host.TryOpen(), Is.True);
                    var view = host.ActiveView;

                    safetyPrep = false;
                    view.RouteAButton.onClick.Invoke();
                    Assert.That(host.IsDeparted, Is.False);
                    Assert.That(host.ActiveView, Is.Not.Null);

                    safetyPrep = true;
                    view.RouteAButton.onClick.Invoke();
                    Assert.That(host.IsDeparted, Is.True);
                    Assert.That(host.ActiveView, Is.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void M16HostBlocksEpilogueCommitWhenShenWillingnessOrClosingToneChanges()
        {
            var root = new GameObject("M16 live context recheck");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M15FinalRollCallFacts.TryRecord(new M15FinalRollCallRecord(
                M15RollCallReader.Protagonist, M15FinalIdentityResponse.PublicTrueName,
                M15PensionClosure.StoppedAndReconciled, M15SteleInscription.KeepOriginal,
                0, false), read, write), Is.True);

            var tones = (M16RespectAgencyTone[])Enum.GetValues(typeof(M16RespectAgencyTone));
            M16RespectAgencyTone initialTone = M16RespectAgencyTone.RespectedOthersChoices;
            M16RespectAgencyTone changedTone = tones[0] == initialTone && tones.Length > 1
                ? tones[1]
                : tones[0];
            Assert.That(changedTone, Is.Not.EqualTo(initialTone));

            bool shenWilling = true;
            M16RespectAgencyTone currentTone = initialTone;

            try
            {
                using (var host = new M16EpilogueHost(
                    root.transform, () => true,
                    () => shenWilling, () => currentTone, read, write))
                {
                    Assert.That(host.TryOpen(), Is.True);
                    var view = host.ActiveView;
                    view.ProceedToDeskButton.onClick.Invoke();
                    view.StepOutsideButton.onClick.Invoke();

                    shenWilling = false;
                    view.PierButton.onClick.Invoke();
                    Assert.That(host.IsCompleted, Is.False);
                    Assert.That(host.ActiveView, Is.Not.Null);

                    shenWilling = true;
                    currentTone = changedTone;
                    view.PierButton.onClick.Invoke();
                    Assert.That(host.IsCompleted, Is.False);
                    Assert.That(host.ActiveView, Is.Not.Null);

                    currentTone = initialTone;
                    view.PierButton.onClick.Invoke();
                    Assert.That(host.IsCompleted, Is.True);
                    Assert.That(host.ActiveView, Is.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
