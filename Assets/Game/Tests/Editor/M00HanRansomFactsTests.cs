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
    public sealed class M00HanRansomFactsTests
    {
        [Test]
        public void CountIsTheFixedV12NarrativeFact()
        {
            Assert.That(M00HanRansomFacts.ConfirmedCount, Is.EqualTo(41));
            Assert.That(M00HanRansomFacts.CountKey,
                Is.EqualTo("rpg.return.m00.han-ransom-count"));
        }

        [UnityTest]
        public IEnumerator PersistsFixedFortyOneAndReadsLegacyMissingAsFortyOne()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return null;
            EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Game/Scenes/Bootstrap.unity",
                new LoadSceneParameters(LoadSceneMode.Single));

            float deadline = Time.realtimeSinceStartup + 30f;
            while ((!Maki.Initialized || Maki.Game?.Variables == null) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(Maki.Initialized, Is.True);
            Assert.That(Maki.Game?.Variables, Is.Not.Null);

            var variables = Maki.Game.Variables;
            int previous = variables.GetInt(M00HanRansomFacts.CountKey);
            try
            {
                Assert.That(M00HanRansomFacts.TryRecordConfirmedCount(), Is.True);
                Assert.That(variables.GetInt(M00HanRansomFacts.CountKey),
                    Is.EqualTo(M00HanRansomFacts.ConfirmedCount));
                Assert.That(M00HanRansomFacts.TryRead(out int recorded), Is.True);
                Assert.That(recorded, Is.EqualTo(41));

                variables.Set(M00HanRansomFacts.CountKey, 0);
                Assert.That(M00HanRansomFacts.TryRead(out int missing), Is.True);
                Assert.That(missing, Is.EqualTo(41));

                variables.Set(M00HanRansomFacts.CountKey, 99);
                Assert.That(M00HanRansomFacts.TryRead(out int stale), Is.True);
                Assert.That(stale, Is.EqualTo(41));
            }
            finally
            {
                if (Maki.Initialized && Maki.Game?.Variables != null)
                    Maki.Game.Variables.Set(M00HanRansomFacts.CountKey, previous);
            }

            yield return new ExitPlayMode();
        }
    }
}
