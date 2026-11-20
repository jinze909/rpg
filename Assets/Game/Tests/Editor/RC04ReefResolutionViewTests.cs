using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RC04ReefResolutionViewTests
    {
        private static RC04DingYueReefSession AfterListening()
        {
            var session = new RC04DingYueReefSession();
            for (int i = 0; i < 3; i++)
                Assert.That(session.SubmitEchoDiscrimination(
                    RC04EchoChoice.SlowHumanEcho), Is.True);
            return session;
        }

        [Test]
        public void NoResolutionBeforeThreeTrueFogRoundsOrVerifiedSceneFacts()
        {
            var root = new GameObject("RC04 no fake outcomes");
            var session = new RC04DingYueReefSession();
            RC04ReefResolutionView view = null;
            try
            {
                int calls = 0;
                view = new RC04ReefResolutionView(root.transform, session,
                    _ => true, _ => { calls++; return true; });
                Assert.That(view.BringDingYueButton.interactable, Is.False);
                Assert.That(view.SongKillButton.interactable, Is.False);
                Assert.That(view.DecoyKillButton.interactable, Is.False);
                Assert.That(view.BeaconButton.interactable, Is.False);
                view.BringDingYueButton.onClick.Invoke();
                Assert.That(calls, Is.Zero);
                Assert.That(view.ResolutionDelivered, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NoAyuanDecoyMustHavePreparedAndActuallyDefeatedBeast()
        {
            var root = new GameObject("RC04 decoy resolved in scene");
            var session = AfterListening();
            RC04ReefResolutionView view = null;
            var values = new Dictionary<string, int>();
            Func<string, int> read = k => values.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => values[k] = v;
            int calls = 0;
            try
            {
                view = new RC04ReefResolutionView(root.transform, session,
                    choice => choice == RC04Resolution.KillMimicBeastWithPreparedDecoy,
                    choice =>
                    {
                        calls++;
                        return session.TryResolveReef(choice, read, write);
                    });
                Assert.That(view.DecoyKillButton.interactable, Is.False);
                Assert.That(session.TryPrepareConsentedDecoyAmbush(
                    true, true, true, true), Is.True);
                view.Refresh();
                Assert.That(view.DecoyKillButton.interactable, Is.False);
                Assert.That(session.TryConfirmDecoyBeastDefeat(false), Is.False);
                Assert.That(session.TryConfirmDecoyBeastDefeat(true), Is.True);
                view.Refresh();
                Assert.That(view.DecoyKillButton.interactable, Is.True);
                Assert.That(view.SongKillButton.interactable, Is.False);
                view.DecoyKillButton.onClick.Invoke();
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(session.IsResolved, Is.True);
                Assert.That(view.ResolutionDelivered, Is.True);
                Assert.That(RC04DingYueReefFacts.TryRead(read,
                    out RC04ReefRecord? saved), Is.True);
                Assert.That(saved.Value.Resolution,
                    Is.EqualTo(RC04Resolution.KillMimicBeastWithPreparedDecoy));
                Assert.That(saved.Value.AyuanSongUsed, Is.False);
                view.DecoyKillButton.onClick.Invoke();
                Assert.That(calls, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SongKillAlsoNeedsSceneConfirmedVictoryAndIsNotAutoRewarded()
        {
            var root = new GameObject("RC04 A Yuan real song");
            var session = AfterListening();
            bool actuallyWon = false;
            RC04ReefResolutionView view = null;
            try
            {
                int calls = 0;
                view = new RC04ReefResolutionView(root.transform, session,
                    choice => choice == RC04Resolution.KillMimicBeastWithAyuanSong &&
                        actuallyWon,
                    _ => { calls++; return true; });
                Assert.That(session.TryPlayAyuanVaryingSong(), Is.True);
                view.Refresh();
                Assert.That(view.SongKillButton.interactable, Is.False);
                actuallyWon = true;
                view.Refresh();
                Assert.That(view.SongKillButton.interactable, Is.True);
                view.SongKillButton.onClick.Invoke();
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(view.ResolutionDelivered, Is.True);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ADeclinedOrInterruptedTransferCanBeRetriedWithoutFakingReturn()
        {
            var root = new GameObject("RC04 return outcome");
            var session = AfterListening();
            var values = new Dictionary<string, int>();
            Func<string, int> read = k => values.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => values[k] = v;
            bool dingYueActuallyAshore = false;
            RC04ReefResolutionView view = null;
            try
            {
                int attempts = 0;
                view = new RC04ReefResolutionView(root.transform, session,
                    choice => choice == RC04Resolution.BringDingYueBack &&
                        dingYueActuallyAshore,
                    choice =>
                    {
                        attempts++;
                        if (attempts == 1)
                            throw new InvalidOperationException("Interrupted Makinom handoff");
                        return session.TryResolveReef(choice, read, write);
                    });
                Assert.That(view.BringDingYueButton.interactable, Is.False);
                dingYueActuallyAshore = true;
                view.Refresh();
                Assert.That(view.BringDingYueButton.interactable, Is.True);
                dingYueActuallyAshore = false;
                view.BringDingYueButton.onClick.Invoke();
                Assert.That(attempts, Is.Zero);
                dingYueActuallyAshore = true;
                view.Refresh();
                Assert.Throws<InvalidOperationException>(() =>
                    view.BringDingYueButton.onClick.Invoke());
                Assert.That(session.IsResolved, Is.False);
                Assert.That(view.BringDingYueButton.interactable, Is.True);
                view.BringDingYueButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(session.IsResolved, Is.True);
                Assert.That(session.CompletedRecord.Value.Resolution,
                    Is.EqualTo(RC04Resolution.BringDingYueBack));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BeaconRequiresRealVolunteerAndSuppliesNotJustButtonPress()
        {
            var root = new GameObject("RC04 signal beacon");
            var session = AfterListening();
            bool beaconInstalled = true;
            bool patrolVolunteered = false;
            bool provisionsCommitted = true;
            RC04ReefResolutionView view = null;
            try
            {
                int calls = 0;
                view = new RC04ReefResolutionView(root.transform, session,
                    choice => choice == RC04Resolution.LeaveWithSignalBeacon &&
                        beaconInstalled && patrolVolunteered && provisionsCommitted,
                    _ => { calls++; return true; });
                Assert.That(view.BeaconButton.interactable, Is.False);
                patrolVolunteered = true;
                view.Refresh();
                Assert.That(view.BeaconButton.interactable, Is.True);
                provisionsCommitted = false;
                view.BeaconButton.onClick.Invoke();
                Assert.That(calls, Is.Zero);
                provisionsCommitted = true;
                view.Refresh();
                view.BeaconButton.onClick.Invoke();
                Assert.That(calls, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocalizationSafeAreaAndDisposalLeaveTheSessionUntouched()
        {
            var root = new GameObject("RC04 resolution view locale");
            var session = AfterListening();
            RC04ReefResolutionView view = null;
            string language = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                view = new RC04ReefResolutionView(root.transform, session,
                    _ => false, _ => true);
                Assert.That(view.Heading.text, Does.Contain("DING YUE"));
                Assert.That(view.Story.text, Does.Contain("Ding Yue"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Story.text, Does.Contain("渔婆"));
                var oldButton = view.BeaconButton;
                view.Dispose();
                oldButton.onClick.Invoke();
                Assert.That(session.IsResolved, Is.False);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(language);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
