using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class FogListeningAudioCuePlayerLifecycleTests
    {
        private GameObject root;
        private FogListeningAudioCuePlayer player;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("FogAudioCuePlayerLifecycleRoot");
        }

        [TearDown]
        public void TearDown()
        {
            player?.Dispose();
            player = null;
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        private static FogListeningSession CreateSession()
        {
            return new FogListeningSession(new[]
            {
                new FogListeningCue(FogBearing.Port, FogCueKind.ReefSurf, 30),
                new FogListeningCue(FogBearing.Starboard, FogCueKind.SignalResponse, 80)
            }, maxListenFocus: 1, maxHullIntegrity: 2);
        }

        [Test]
        public void Constructor_NullParent_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new FogListeningAudioCuePlayer(null));
        }

        [Test]
        public void Sync_WhenLowPassDestroyedExternally_DisposesAndReleasesClipWithoutThrowing()
        {
            var session = CreateSession();
            player = new FogListeningAudioCuePlayer(root.transform);
            player.Sync(session);
            AudioClip clip = player.Source.clip;
            Assert.That(clip, Is.Not.Null);
            Assert.That(player.IsDisposed, Is.False);

            UnityEngine.Object.DestroyImmediate(player.LowPass);
            Assert.That(player.IsDisposed, Is.True);

            Assert.DoesNotThrow(() => player.Sync(session));
            Assert.That(clip == null, Is.True,
                "External component teardown must release the generated AudioClip on Sync.");
        }

        [Test]
        public void Sync_WhenParentDestroyedExternally_DisposesAndReleasesClipWithoutThrowing()
        {
            var session = CreateSession();
            player = new FogListeningAudioCuePlayer(root.transform);
            player.Sync(session);
            AudioClip clip = player.Source.clip;
            Assert.That(clip, Is.Not.Null);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.That(player.IsDisposed, Is.True);

            Assert.DoesNotThrow(() => player.Sync(session));
            Assert.That(clip == null, Is.True,
                "External parent teardown must release the generated AudioClip on Sync.");
        }

        [Test]
        public void Dispose_IsIdempotent_AndIgnoresSubsequentSync()
        {
            var session = CreateSession();
            player = new FogListeningAudioCuePlayer(root.transform);
            player.Sync(session);
            AudioClip clip = player.Source.clip;
            Assert.That(clip, Is.Not.Null);

            Assert.DoesNotThrow(() => player.Dispose());
            Assert.DoesNotThrow(() => player.Dispose());
            Assert.That(player.IsDisposed, Is.True);
            Assert.That(clip == null, Is.True);
            Assert.DoesNotThrow(() => player.Sync(session));
            Assert.DoesNotThrow(() => player.Sync(null));
        }
    }
}
