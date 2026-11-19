using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M02HarborDeskViewTests
    {
        private GameObject root;
        private M02HarborDeskView view;
        private string previousLanguage;

        [SetUp]
        public void SetUp()
        {
            previousLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M02 harbor desk test root");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
            SoundwellUiLocalization.SetLanguage(previousLanguage);
        }

        [Test]
        public void PublicPapersRequireTheActualStampButtonAndCallbackFiresOnce()
        {
            int called = 0;
            M02HarborDeskOutcome recorded = null;
            view = new M02HarborDeskView(root.transform,
                M01PensionChoice.RegisterLivingIdentity, result =>
                {
                    called++;
                    recorded = result;
                });
            Assert.That(view.Desk.LivingRegistryStamped, Is.False);
            Assert.That(view.GetActionButton(0).gameObject.activeSelf, Is.True);
            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.GetActionButton(0)),
                "Public registration must be the first focused action when available.");
            view.GetActionButton(0).onClick.Invoke();
            Assert.That(view.Desk.LivingRegistryStamped, Is.True);
            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.GetActionButton(1)),
                "After stamping, the focus fallback must not skip all conversations to Leave.");
            view.GetActionButton(0).onClick.Invoke();
            view.GetActionButton(1).onClick.Invoke();
            view.LeaveButton.onClick.Invoke();
            view.LeaveButton.onClick.Invoke();
            Assert.That(called, Is.EqualTo(1));
            Assert.That(recorded.OfficialShipOffered, Is.True);
            Assert.That(recorded.QiQueDebtDiscovered, Is.True);
            Assert.That(view.PreferredFocusTarget, Is.Null,
                "Completed dialogue must not try to focus a hidden exit control.");
        }

        [Test]
        public void UnregisteredPlayerCannotUseOfficialStampAndCanStillLearnDebt()
        {
            M02HarborDeskOutcome outcome = null;
            view = new M02HarborDeskView(root.transform,
                M01PensionChoice.KeepIdentityUnregistered, result => outcome = result);
            Assert.That(view.GetActionButton(0).gameObject.activeSelf, Is.False);
            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.GetActionButton(1)),
                "An unregistered player should first focus the available Qi Que inquiry.");
            var shipQuestion = view.GetActionButton(1);
            var label = shipQuestion.GetComponentInChildren<UnityEngine.UI.Text>();
            Assert.That(label.text, Is.EqualTo("Ask about Qi Que's ship"));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(label.text, Is.EqualTo("打听祁雀的船"));
            view.GetActionButton(0).onClick.Invoke();
            view.GetActionButton(2).onClick.Invoke();
            Assert.That(view.Desk.LivingRegistryStamped, Is.False);
            Assert.That(view.Desk.QiQueDebtDiscovered, Is.False);
            Assert.That(view.Dialogue.text, Does.Not.Contain("船贷"));
            shipQuestion.onClick.Invoke();
            Assert.That(view.Dialogue.text, Does.Contain("祁雀的船贷").And.Contain("下个月初五"));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            Assert.That(view.Dialogue.text, Does.Contain("ship loan").And.Contain("fifth of next month"));
            view.LayoutForViewportAspect(9f / 16f);
            view.LayoutForViewportAspect(32f / 9f);
            view.LeaveButton.onClick.Invoke();
            Assert.That(outcome.OfficialShipOffered, Is.False);
            Assert.That(outcome.QiQueDebtDiscovered, Is.True);
        }

        [Test]
        public void FocusMovesThroughUnheardSubjectsAndNeverCreatesAStampOrVoyage()
        {
            int delivered = 0;
            view = new M02HarborDeskView(root.transform,
                M01PensionChoice.KeepIdentityUnregistered, _ => delivered++);

            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.GetActionButton(1)));
            view.GetActionButton(1).onClick.Invoke();
            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.GetActionButton(2)));
            Assert.That(view.GetActionButton(1)
                .GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("(discussed)"));

            // Repeating a known question remains possible, but is never the
            // next suggested action. Nor does it grant official registration.
            view.GetActionButton(1).onClick.Invoke();
            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.GetActionButton(2)));
            Assert.That(view.Desk.LivingRegistryStamped, Is.False);
            view.GetActionButton(2).onClick.Invoke();
            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.GetActionButton(3)));
            view.GetActionButton(3).onClick.Invoke();
            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.LeaveButton));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(view.GetActionButton(2)
                .GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain(" (已谈)"));
            Assert.That(delivered, Is.Zero);
            Assert.That(view.Desk.Stage, Is.EqualTo(M02HarborDeskStage.Discussing));
            view.LeaveButton.onClick.Invoke();
            Assert.That(delivered, Is.EqualTo(1));
            Assert.That(view.PreferredFocusTarget, Is.Null);
        }

        [Test]
        public void FailedHostCallbackKeepsHarborOutcomeRetryableAndBlocksReentrancy()
        {
            int attempts = 0;
            M02HarborDeskOutcome delivered = null;
            view = new M02HarborDeskView(root.transform,
                M01PensionChoice.RegisterLivingIdentity, result =>
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        view.LeaveButton.onClick.Invoke();
                        throw new InvalidOperationException("Simulated Makinom save failure");
                    }
                    delivered = result;
                });
            view.GetActionButton(0).onClick.Invoke();
            view.GetActionButton(1).onClick.Invoke();
            view.GetActionButton(3).onClick.Invoke();

            Assert.Throws<InvalidOperationException>(() => view.LeaveButton.onClick.Invoke());
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(view.Desk.Stage, Is.EqualTo(M02HarborDeskStage.Discussing));
            Assert.That(view.Desk.LivingRegistryStamped, Is.True);
            Assert.That(view.Desk.QiQueDebtDiscovered, Is.True);
            Assert.That(view.Desk.TransferAssistanceAccepted, Is.True);
            Assert.That(view.LeaveButton.gameObject.activeSelf, Is.True);

            view.LeaveButton.onClick.Invoke();
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(view.Desk.Stage, Is.EqualTo(M02HarborDeskStage.Finished));
            Assert.That(delivered, Is.Not.Null);
            Assert.That(delivered.OfficialShipOffered, Is.True);
            Assert.That(delivered.QiQueDebtDiscovered, Is.True);
            Assert.That(delivered.TransferAssistanceAccepted, Is.True);
            Assert.That(view.LeaveButton.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void DisposalDetachesLocaleListenerAndDropsUndeliveredOutcome()
        {
            int calls = 0;
            view = new M02HarborDeskView(root.transform,
                M01PensionChoice.RequestMissingReturnReview, _ => calls++);
            view.Dispose();
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void DestroyedViewSafelyHandlesLayoutBeforeLocaleChange()
        {
            view = new M02HarborDeskView(root.transform,
                M01PensionChoice.RegisterLivingIdentity, _ => { });

            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.That(view.PreferredFocusTarget, Is.Null);
            Assert.DoesNotThrow(() => view.LayoutForViewportAspect(9f / 16f));
            Assert.DoesNotThrow(() =>
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
            Assert.DoesNotThrow(() => view.Dispose());
        }

        [Test]
        public void HarborVisitGuardsNullArgumentsInvalidAspectsAndDestroyedParent()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new M02HarborVisit(null, M01PensionChoice.RegisterLivingIdentity, () => { }));
            Assert.Throws<ArgumentNullException>(() =>
                new M02HarborVisit(root.transform, M01PensionChoice.RegisterLivingIdentity, null));

            int finished = 0;
            using (var visit = new M02HarborVisit(root.transform,
                M01PensionChoice.RegisterLivingIdentity, () => finished++))
            {
                Assert.DoesNotThrow(() => visit.Tick(float.NaN));
                Assert.DoesNotThrow(() => visit.Tick(float.PositiveInfinity));
                Assert.DoesNotThrow(() => visit.Tick(0f));
                Assert.DoesNotThrow(() => visit.Tick(9f / 16f));

                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.DoesNotThrow(() => visit.Tick(16f / 9f));
                Assert.DoesNotThrow(() => visit.Dispose());
            }

            Assert.That(finished, Is.Zero);
        }
    }
}
