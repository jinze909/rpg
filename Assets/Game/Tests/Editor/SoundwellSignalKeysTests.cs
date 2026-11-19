using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class SoundwellSignalKeysTests
    {
        [Test]
        public void RetryResetKeysIncludeSignalKeyWithNoDuplicatesOrEntryKeys()
        {
            var seen = new HashSet<string>();
            foreach (string key in SoundwellSignalKeys.RetryResetKeys)
            {
                Assert.That(string.IsNullOrWhiteSpace(key), Is.False);
                Assert.That(seen.Add(key), Is.True, "Duplicate retry reset key: " + key);
            }

            Assert.That(seen, Does.Contain(SoundwellSignalKeys.SignalKey));
            Assert.That(seen, Does.Not.Contain(SoundwellSignalKeys.EntryOutcomeKey));
            Assert.That(seen, Does.Not.Contain(SoundwellSignalKeys.EntryOutcomeRecordedKey));
            Assert.That(seen, Does.Not.Contain(SoundwellSignalKeys.EntryCartKitKey));
            Assert.That(seen, Does.Not.Contain(SoundwellSignalKeys.EntrySafeRouteKey));
            Assert.That(seen, Does.Not.Contain(SoundwellSignalKeys.EntryBeastContactsKey));
        }

        [Test]
        public void SignalKeyConstantsHaveUniqueMakinomIdsWithoutAliases()
        {
            FieldInfo[] fields = typeof(SoundwellSignalKeys).GetFields(
                BindingFlags.Public | BindingFlags.Static);
            var ids = new HashSet<string>();
            foreach (FieldInfo field in fields)
            {
                if (!field.IsLiteral || field.FieldType != typeof(string))
                    continue;
                string value = (string)field.GetRawConstantValue();
                Assert.That(ids.Add(value), Is.True,
                    "Duplicate alias constant found on SoundwellSignalKeys: " + field.Name);
            }
        }
    }
}
