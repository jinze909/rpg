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
                variables.Set(M00MemorialSquare.XiaodouPromptedKey, 1);
                variables.Set(M04XiaodouFarewellFacts.RealLineKey, 2);
                variables.Set(M04QiQueCupFacts.CupCarriedKey, 1);
                // A previous visit qualified for an official ship and heard the secret.
                var previousVisit = new M03LuJunEncounter(M02BridgeEvidence.WarReportDraft, true);
                previousVisit.Begin();
                previousVisit.ObserveRollCall();
                previousVisit.Ask(M03LuJunOpening.AskWhoCutBridge);
                previousVisit.Respond(M03ConfessionResponse.RemainSilent);
                previousVisit.AnswerSecrecy(M03SecrecyAnswer.PromiseSecrecy);
                previousVisit.ChooseVoyage(M03VoyageChoice.MixedWithOfficial);
                Assert.That(M03LuJunFacts.TryRecord(previousVisit), Is.True);

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
                Assert.That(M03LuJunFacts.TryRead(out M03LuConfession pendingConfession,
                    out M03SecrecyAnswer pendingSecrecy, out M03VoyageChoice? pendingVoyage), Is.True);
                Assert.That(pendingConfession, Is.EqualTo(M03LuConfession.NotAsked));
                Assert.That(pendingSecrecy, Is.EqualTo(M03SecrecyAnswer.NotTriggered));
                Assert.That(pendingVoyage, Is.Null,
                    "A fresh camp visit must not inherit the previously selected official route.");
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

                Assert.That(deliveries, Is.Zero,
                    "M03 should lead into M04, not skip the playable farewell.");
                Button sweet = FindButton(owner, "M04 Xiaodou advance");
                Assert.That(sweet.gameObject.activeInHierarchy, Is.True);
                Assert.That(variables.GetInt(M04XiaodouFarewellFacts.RealLineKey), Is.Zero,
                    "A replay must clear the previous voyage's answer.");
                sweet.onClick.Invoke();
                Assert.That(sweet.gameObject.activeInHierarchy, Is.False,
                    "Taking the sweet must not silently answer Xiaodou.");
                Button teach = FindButton(owner, "M04 Xiaodou choice 0");
                Assert.That(teach.gameObject.activeInHierarchy, Is.True);
                teach.onClick.Invoke();
                Assert.That(deliveries, Is.Zero,
                    "The opening should finish only after the farewell is spoken.");
                sweet.onClick.Invoke();
                Assert.That(deliveries, Is.Zero,
                    "The opening must include the dockside gift after Xiaodou.");
                Assert.That(variables.GetInt(M04XiaodouFarewellFacts.RealLineKey), Is.EqualTo(1),
                    "A completed choice is saved before the cup scene.");

                Button cupAdvance = FindButton(owner, "M04 Qi cup continue");
                Assert.That(cupAdvance.gameObject.activeInHierarchy, Is.True);
                Assert.That(variables.GetInt(M04QiQueCupFacts.CupCarriedKey), Is.Zero,
                    "An old voyage's cup cannot teleport onto this sailing.");
                cupAdvance.onClick.Invoke();
                Assert.That(cupAdvance.gameObject.activeInHierarchy, Is.False,
                    "Inspecting the cup is not accepting the gift.");
                Button debtReply = FindButton(owner, "M04 Qi cup choice 2");
                Assert.That(debtReply.gameObject.activeInHierarchy, Is.True,
                    "Only the known debt grants this third reply.");
                debtReply.onClick.Invoke();
                Assert.That(variables.GetInt(M04QiQueCupFacts.CupCarriedKey), Is.Zero,
                    "Do not save the carried cup until farewell completion.");
                cupAdvance.onClick.Invoke();
                Assert.That(deliveries, Is.Zero,
                    "The cup now hands off to M04 dock preflight, not straight to Entry.");
                Assert.That(variables.GetInt(M04QiQueCupFacts.CupCarriedKey), Is.EqualTo(1));

                Button preflight = FindButton(owner, "M04 dock preflight advance");
                Assert.That(preflight.gameObject.activeInHierarchy, Is.True);
                Assert.That(variables.GetInt(M04DepartureFacts.ConfirmedKey), Is.Zero);
                preflight.onClick.Invoke();
                preflight.onClick.Invoke();
                Assert.That(deliveries, Is.Zero,
                    "Route review and the physical-check beat must be heard before leaving.");
                preflight.onClick.Invoke();
                Assert.That(deliveries, Is.EqualTo(1));
                Assert.That(variables.GetInt(M04DepartureFacts.SailedKey), Is.Zero,
                    "Dockside preflight must never be mistaken for actually sailing.");
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
