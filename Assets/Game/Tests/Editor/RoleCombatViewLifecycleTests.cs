using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RoleCombatViewLifecycleTests
    {
        private GameObject root;
        private GameObject encounterHost;
        private OrkRoleEncounter encounter;
        private RoleCombatView view;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("RoleCombatViewTestRoot");
            encounterHost = new GameObject("RoleEncounterStub");
            encounter = encounterHost.AddComponent<OrkRoleEncounter>();
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            view = null;
            if (encounterHost != null)
                UnityEngine.Object.DestroyImmediate(encounterHost);
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void Constructor_NullParentOrEncounter_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RoleCombatView(null, encounter));
            Assert.Throws<ArgumentNullException>(() => new RoleCombatView(root.transform, null));
        }

        [Test]
        public void Refresh_WhenChildDestroyedExternally_DisposesViewWithoutThrowing()
        {
            view = new RoleCombatView(root.transform, encounter);
            Assert.That(view.IsDisposed, Is.False);

            UnityEngine.Object.DestroyImmediate(view.ThreatLabel.gameObject);

            Assert.DoesNotThrow(() => view.Refresh("test exchange", false));
            Assert.That(view.IsDisposed, Is.True);
        }

        [Test]
        public void CommandButtonClick_WhenPartyNullOrChildDestroyedExternally_DoesNotThrow()
        {
            view = new RoleCombatView(root.transform, encounter);

            Assert.DoesNotThrow(() => view.PrimaryButton.onClick.Invoke());
            Assert.DoesNotThrow(() => view.SecondaryButton.onClick.Invoke());
            Assert.DoesNotThrow(() => view.RetreatButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.False);

            UnityEngine.Object.DestroyImmediate(view.SecondaryButton.gameObject);

            Assert.DoesNotThrow(() => view.PrimaryButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
        }

        [Test]
        public void Dispose_IsIdempotent_AndSafeBeforeSubsequentRefresh()
        {
            view = new RoleCombatView(root.transform, encounter);

            Assert.DoesNotThrow(() => view.Dispose());
            Assert.DoesNotThrow(() => view.Dispose());
            Assert.That(view.IsDisposed, Is.True);
            Assert.DoesNotThrow(() => view.Refresh("after dispose", true));
        }
    }
}
