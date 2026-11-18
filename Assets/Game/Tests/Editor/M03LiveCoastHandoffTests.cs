using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M03LiveCoastHandoffTests
    {
        [UnityTest]
        public IEnumerator FinishingQiQueNightOpensLuJunAndPersistsOnlyTheChosenPassage()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return null;

            GameObject owner = null;
            try
            {
                EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/Game/Scenes/Bootstrap.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));

                float deadline = Time.realtimeSinceStartup + 8f;
                while ((!Maki.Initialized || Maki.Game?.Variables == null) &&
                       Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(Maki.Initialized && Maki.Game?.Variables != null, Is.True,
                    "The continuous opening needs the live Makinom story store.");

                var variables = Maki.Game.Variables;
                variables.Set(M02BridgeEvidenceFacts.EvidenceKey, (int)M02BridgeEvidence.WarReportDraft);
                variables.Set(M02OfficialDockFacts.LivingRegistrationStampedKey, 0);
                variables.Set(M02MissingReturnFacts.ApprovalKey, 0);
                variables.Set(M02QiQueDebtFacts.DebtKnownKey, 1);
                variables.Set(M03LuJunFacts.ConfessionKey, 0);
                variables.Set(M03LuJunFacts.SecrecyKey, 0);
                variables.Set(M03LuJunFacts.VoyageKey, 0);

                owner = new GameObject("M02 to M03 live opening owner");
                var opening = owner.AddComponent<ReturnOpeningContinuation>();
                int deliveries = 0;
                var completion = typeof(ReturnOpeningContinuation).GetField(
                    "onFinished", BindingFlags.Instance | BindingFlags.NonPublic);
                var nextNight = typeof(ReturnOpeningContinuation).GetMethod(
                    "QueueQiQueNight", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(completion, Is.Not.Null);
                Assert.That(nextNight, Is.Not.Null);
                completion.SetValue(opening, new Action(() => deliveries++));

                // The M00 -> M02 path is verified separately. Run the actual
                // night UI callback that this story owner wires to M03.
                nextNight.Invoke(opening, null);
                Button night = FindButton(owner, "M02 Qi Que continue");
                Assert.That(night.gameObject.activeInHierarchy, Is.True);
                for (int i = 0; i < 4; i++)
                    night.onClick.Invoke();

                Assert.That(night.gameObject.activeInHierarchy, Is.False,
                    "The finished night should not remain interactive.");
                Button roll = FindButton(owner, "M03 Lu Jun advance");
                Assert.That(roll.gameObject.activeInHierarchy, Is.True,
                    "The night-to-camp handoff must open an actual M03 scene.");
                Assert.That(variables.GetInt(M03LuJunFacts.VoyageKey), Is.Zero,
                    "Night-time chart discussion must not book a ship.");
                Assert.That(deliveries, Is.Zero);

                // Both the original and the extended three-name roll call
                // must finish before opening Lu Jun's conversation.
                for (int beat = 0; beat < 6 && roll.gameObject.activeInHierarchy; beat++)
                    roll.onClick.Invoke();
                Assert.That(roll.gameObject.activeInHierarchy, Is.False,
                    "After the roll call Lu Jun should present dialogue options.");
                FindButton(owner, "M03 Lu Jun option 2").onClick.Invoke();
                Assert.That(FindButton(owner, "M03 Lu Jun option 2").gameObject.activeInHierarchy,
                    Is.False, "An unstamped visit must not offer the official ship.");
                FindButton(owner, "M03 Lu Jun option 1").onClick.Invoke();

                Assert.That(deliveries, Is.EqualTo(1));
                Assert.That(M03LuJunFacts.TryRead(out M03LuConfession confession,
                    out M03SecrecyAnswer secrecy, out M03VoyageChoice? voyage), Is.True);
                Assert.That(confession, Is.EqualTo(M03LuConfession.NotAsked));
                Assert.That(secrecy, Is.EqualTo(M03SecrecyAnswer.NotTriggered));
                Assert.That(voyage, Is.EqualTo(M03VoyageChoice.QiQueMerchant));
                Assert.That(variables.GetInt(M03LuJunFacts.VoyageKey), Is.GreaterThan(0));
                Assert.That(M02OfficialDockFacts.TryReadEffective(out bool official), Is.True);
                Assert.That(official, Is.False);
            }
            finally
            {
                if (owner != null)
                    UnityEngine.Object.Destroy(owner);
            }
            yield return new ExitPlayMode();
        }

        private static Button FindButton(GameObject owner, string name)
        {
            return owner.GetComponentsInChildren<Button>(true)
                .Single(button => button.gameObject.name == name);
        }
    }
}
