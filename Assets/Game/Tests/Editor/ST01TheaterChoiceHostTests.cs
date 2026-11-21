using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST01TheaterChoiceHostTests
    {
        private static Func<string, int> Read(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void TheaterNeedsActualScriptRehearsalAndDisplacedResidentsEvidence()
        {
            var root = new GameObject("ST01 host evidence");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Read(values);
            Action<string, int> write = (key, value) => values[key] = value;
            bool atTheater = false;
            bool scripts = false;
            bool rehearsed = false;
            bool workers = false;
            ST01TheaterChoiceHost host = null;
            try
            {
                host = new ST01TheaterChoiceHost(root.transform,
                    () => atTheater, () => true,
                    () => scripts, () => scripts,
                    () => rehearsed, () => workers,
                    () => true, () => true, () => true, read, write);
                Assert.That(host.TryOpen(), Is.False);
                atTheater = true;
                Assert.That(host.TryOpen(), Is.False);
                scripts = true;
                Assert.That(host.TryOpen(), Is.False);
                rehearsed = true;
                Assert.That(host.TryOpen(), Is.False);
                workers = true;
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.ActiveView.Panel.anchorMin.y, Is.EqualTo(0f));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void JingwanFirstMeetingCannotInventWhiteReedAndBoycottDoesNotMailTicket()
        {
            var root = new GameObject("ST01 no backstage memory");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Read(values);
            Action<string, int> write = (key, value) => values[key] = value;
            ST01TheaterChoiceHost host = null;
            string previous = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                host = new ST01TheaterChoiceHost(root.transform,
                    () => true, () => true, () => true, () => true,
                    () => true, () => true,
                    () => false, () => false, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView.Body.text, Does.Contain("第一次相遇"));
                Assert.That(host.ActiveView.RewriteButton.interactable, Is.False);
                Assert.That(host.ActiveView.ContractButton.interactable, Is.False);
                Assert.That(host.ActiveView.BoycottButton.interactable, Is.True);
                host.ActiveView.BoycottButton.onClick.Invoke();
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(ST01RegionalStoryFacts.TryRead(read, out ST01RegionalOutcome saved),
                    Is.True);
                Assert.That(saved.Departure.HasValue, Is.False);
                Assert.That(saved.Theater, Is.EqualTo(ST01TheaterChoice.PublicBoycott));
                Assert.That(saved.ShowM15Ticket, Is.False);
                Assert.That(ST01RegionalStoryFacts.TryMarkTicketSent(read, write), Is.False);
                Assert.That(host.TryOpen(), Is.False);
            }
            finally
            {
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(previous);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CommitRechecksLivePrerequisitesNotJustButtonAvailability()
        {
            var root = new GameObject("ST01 live proof");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Read(values);
            Action<string, int> write = (key, value) => values[key] = value;
            bool forumPlanned = true;
            bool rehearsed = true;
            ST01TheaterChoiceHost host = null;
            try
            {
                host = new ST01TheaterChoiceHost(root.transform,
                    () => true, () => true, () => true, () => true,
                    () => rehearsed, () => true,
                    () => false, () => forumPlanned, () => false, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var button = host.ActiveView.ContractButton;
                Assert.That(button.interactable, Is.True);

                rehearsed = false;
                button.onClick.Invoke();
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(ST01RegionalStoryFacts.TryRead(read, out ST01RegionalOutcome pending),
                    Is.True);
                Assert.That(pending.Theater.HasValue, Is.False);
                rehearsed = true;
                forumPlanned = false;
                host.RefreshView();
                Assert.That(button.interactable, Is.False);
                forumPlanned = true;
                host.RefreshView();
                Assert.That(button.interactable, Is.True);
                button.onClick.Invoke();
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(ST01RegionalStoryFacts.TryRead(read, out ST01RegionalOutcome settled),
                    Is.True);
                Assert.That(settled.Theater,
                    Is.EqualTo(ST01TheaterChoice.KeepContractAndHearResidents));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedTheaterWriteAllowsOnlySameChoiceAndRecoversOnReopen()
        {
            var root = new GameObject("ST01 interrupted write");
            var values = new Dictionary<string, int>();
            var read = Read(values);
            bool dropAck = true;
            Action<string, int> interrupted = (key, value) =>
            {
                if (key == ST01RegionalStoryFacts.TheaterConfirmedKey && dropAck)
                {
                    dropAck = false;
                    throw new InvalidOperationException("Lost confirm before commit");
                }
                values[key] = value;
            };
            ST01TheaterChoiceHost host = null;
            ST01TheaterChoiceHost reopened = null;
            try
            {
                host = new ST01TheaterChoiceHost(root.transform,
                    () => true, () => true, () => true, () => true,
                    () => true, () => true, () => true, () => true,
                    () => true, read, interrupted);
                Assert.That(host.TryOpen(), Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    host.ActiveView.RewriteButton.onClick.Invoke());
                Assert.That(ST01RegionalStoryFacts.TryRead(read, out _), Is.False);
                host.Dispose();

                reopened = new ST01TheaterChoiceHost(root.transform,
                    () => true, () => true, () => true, () => true,
                    () => true, () => true, () => true, () => true,
                    () => true, read, interrupted);
                Assert.That(reopened.TryOpen(), Is.True);
                Assert.That(reopened.ActiveView.ContractButton.interactable, Is.False);
                Assert.That(reopened.ActiveView.BoycottButton.interactable, Is.False);
                reopened.ActiveView.RewriteButton.onClick.Invoke();
                Assert.That(reopened.ActiveView, Is.Null);
                Assert.That(ST01RegionalStoryFacts.TryRead(read,
                    out ST01RegionalOutcome saved), Is.True);
                Assert.That(saved.Theater,
                    Is.EqualTo(ST01TheaterChoice.RewritePlayWithFundingRisk));
            }
            finally
            {
                reopened?.Dispose();
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LanguageAspectAndDisposedChoiceEventsRemainSafe()
        {
            var root = new GameObject("ST01 theme");
            ST01TheaterChoiceHost host = null;
            var values = new Dictionary<string, int>();
            var read = Read(values);
            string previous = SoundwellUiLocalization.Language;
            try
            {
                Assert.That(ST01RegionalStoryFacts.TryRecordDeparture(
                    ST01DepartureChoice.StagedAbduction,
                    read, (key, value) => values[key] = value), Is.True);
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                host = new ST01TheaterChoiceHost(root.transform,
                    () => true, () => true, () => true, () => true,
                    () => true, () => true, () => false, () => false,
                    () => true, read, (key, value) => values[key] = value);
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView.Heading.text, Does.Contain("JINGWAN"));
                Assert.That(host.ActiveView.Body.text, Does.Contain("staged abduction"));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(host.ActiveView.Heading.text, Does.Contain("镜湾"));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() =>
                        host.ActiveView.LayoutForViewportAspect(aspect));
                var button = host.ActiveView.BoycottButton;
                host.Dispose();
                button.onClick.Invoke();
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(ST01RegionalStoryFacts.TryRead(read,
                    out ST01RegionalOutcome outcome), Is.True);
                Assert.That(outcome.Theater.HasValue, Is.False);
            }
            finally
            {
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(previous);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
