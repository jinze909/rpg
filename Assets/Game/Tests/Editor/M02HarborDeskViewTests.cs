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
                Object.DestroyImmediate(root);
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
            view.GetActionButton(0).onClick.Invoke();
            Assert.That(view.Desk.LivingRegistryStamped, Is.False);
            view.GetActionButton(1).onClick.Invoke();
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(view.Dialogue.text, Does.Contain("祁雀的船贷"));
            view.LayoutForViewportAspect(9f / 16f);
            view.LayoutForViewportAspect(32f / 9f);
            view.LeaveButton.onClick.Invoke();
            Assert.That(outcome.OfficialShipOffered, Is.False);
            Assert.That(outcome.QiQueDebtDiscovered, Is.True);
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
    }
}
