using System;
using System.Collections;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rpg.Tests.Editor
{
    public sealed class M04QiQueCupFactsTests
    {
        [UnityTest]
        public IEnumerator CupStateDistinguishesUnplayedDepartureFromCompletedGiftAndNoGift()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return null;
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
                    "The live Makinom story store must own the cup state.");

                var variables = Maki.Game.Variables;
                variables.Set(M04QiQueCupFacts.CupCarriedKey, 1);
                Assert.That(M04QiQueCupFacts.TryRead(out bool carrying), Is.True);
                Assert.That(carrying, Is.True);

                Assert.That(M04QiQueCupFacts.TryResetForNewVisit(), Is.True);
                Assert.That(variables.GetInt(M04QiQueCupFacts.CupCarriedKey), Is.Zero);
                Assert.That(M04QiQueCupFacts.TryRead(out carrying), Is.False,
                    "An unplayed M04 farewell is not evidence of a no-cup outcome.");
                Assert.That(carrying, Is.False);
                variables.Set(M04QiQueCupFacts.CupCarriedKey, 99);
                Assert.That(M04QiQueCupFacts.TryRead(out carrying), Is.False,
                    "Unknown save codes must not masquerade as no gift.");

                var absent = new M04QiQueCupEncounter(false, true);
                absent.Begin();
                absent.Finish();
                Assert.That(M04QiQueCupFacts.TryRecord(absent), Is.True);
                Assert.That(variables.GetInt(M04QiQueCupFacts.CupCarriedKey), Is.EqualTo(2));
                Assert.That(M04QiQueCupFacts.TryRead(out carrying), Is.True);
                Assert.That(carrying, Is.False,
                    "Only a completed no-encounter branch confirms the ordinary lamp.");

                Assert.That(M04QiQueCupFacts.TryResetForNewVisit(), Is.True);
                var received = new M04QiQueCupEncounter(true, false);
                received.Begin();
                received.ExamineGift();
                Assert.Throws<InvalidOperationException>(() =>
                    M04QiQueCupFacts.TryRecord(received));
                Assert.That(M04QiQueCupFacts.TryRead(out carrying), Is.False);
                received.Choose(M04QiQueCupReply.TeaseUglyCup);
                received.Finish();
                Assert.That(M04QiQueCupFacts.TryRecord(received), Is.True);
                Assert.That(M04QiQueCupFacts.TryRead(out carrying), Is.True);
                Assert.That(carrying, Is.True);
            }
            finally
            {
                // ExitPlayMode restores the clean editor state; no alternate
                // persistent state is introduced by this test.
            }
            yield return new ExitPlayMode();
        }
    }
}
