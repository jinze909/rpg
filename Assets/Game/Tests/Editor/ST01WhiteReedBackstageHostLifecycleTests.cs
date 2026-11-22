using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ST01WhiteReedBackstageHostLifecycleTests
    {
        private static Func<string, int> Read(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static Action<string, int> Write(Dictionary<string, int> values)
        {
            return (key, value) => values[key] = value;
        }

        [Test]
        public void Constructor_NullDelegates_ThrowArgumentNullException()
        {
            var values = new Dictionary<string, int>();
            Func<bool> yes = () => true;
            Func<string, int> read = Read(values);
            Action<string, int> write = Write(values);

            Assert.Throws<ArgumentNullException>(() =>
                new ST01WhiteReedBackstageHost(null, yes, yes, yes, read, write));
            Assert.Throws<ArgumentNullException>(() =>
                new ST01WhiteReedBackstageHost(yes, null, yes, yes, read, write));
            Assert.Throws<ArgumentNullException>(() =>
                new ST01WhiteReedBackstageHost(yes, yes, null, yes, read, write));
            Assert.Throws<ArgumentNullException>(() =>
                new ST01WhiteReedBackstageHost(yes, yes, yes, null, read, write));
            Assert.Throws<ArgumentNullException>(() =>
                new ST01WhiteReedBackstageHost(yes, yes, yes, yes, null, write));
            Assert.Throws<ArgumentNullException>(() =>
                new ST01WhiteReedBackstageHost(yes, yes, yes, yes, read, null));
        }

        [Test]
        public void Close_ClearsUncommittedSession_AndAllowsCleanReopen()
        {
            var values = new Dictionary<string, int>();
            using var host = new ST01WhiteReedBackstageHost(
                () => true, () => true, () => true, () => true,
                Read(values), Write(values));

            Assert.That(host.IsOpen, Is.False);
            Assert.That(host.TryOpen(), Is.True);
            Assert.That(host.IsOpen, Is.True);
            ST01WhiteReedBackstageSession first = host.Session;
            Assert.That(first, Is.Not.Null);

            host.Close();
            Assert.That(host.IsOpen, Is.False);
            Assert.That(host.IsDisposed, Is.False);
            Assert.That(host.Session, Is.Null);

            Assert.That(host.TryOpen(), Is.True);
            Assert.That(host.IsOpen, Is.True);
            Assert.That(host.Session, Is.Not.SameAs(first));
        }

        [Test]
        public void Dispose_IsIdempotent_ClearsSession_AndBlocksFurtherOpenOrSave()
        {
            var values = new Dictionary<string, int>();
            var host = new ST01WhiteReedBackstageHost(
                () => true, () => true, () => true, () => true,
                Read(values), Write(values));

            Assert.That(host.TryOpen(), Is.True);
            Assert.DoesNotThrow(() => host.Dispose());
            Assert.DoesNotThrow(() => host.Dispose());
            Assert.That(host.IsDisposed, Is.True);
            Assert.That(host.IsOpen, Is.False);
            Assert.That(host.Session, Is.Null);
            Assert.That(host.TryOpen(), Is.False);
            Assert.That(host.TrySaveAfterPerformance(), Is.False);
        }
    }
}
