using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionMapIntegrityValidatorE2ETests
    {
        [Test]
        public void AllFourRegions_MapIntegrityValidatorAndFootprintBudget_PassWithZeroIssues()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            List<RpgMapValidationIssue> issues = RpgWorld500xMapIntegrityValidator.ValidateAllFourMaps();
            Assert.IsEmpty(issues, string.Join("\n", issues));

            long totalBytes = 0;
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            var keyIssues = new List<RpgMapValidationIssue>();

            foreach (RpgExpandedMapSpec spec in specs)
            {
                Assert.AreEqual(0, spec.ExpandedWidth % RpgLargeMapChunkRenderer.ChunkSizeTiles, $"Width must be chunk-aligned for {spec.MapId}.");
                Assert.AreEqual(0, spec.ExpandedHeight % RpgLargeMapChunkRenderer.ChunkSizeTiles, $"Height must be chunk-aligned for {spec.MapId}.");
                totalBytes += (long)spec.ExpandedWidth * spec.ExpandedHeight;

                string mapName = spec.MapId.ToString();
                foreach (RpgMapLandmarkMarker marker in RpgWorldLandmarkCatalog.GetMarkers(spec.MapId))
                {
                    string markerKey = RpgMapExplorationTracker.MarkerDiscoveryKey(marker.Id);
                    StringAssert.StartsWith(RpgWorld500xMapIntegrityValidator.RequiredWorldKeyPrefix, markerKey);
                    RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene(mapName, markerKey, seenKeys, keyIssues);
                }

                foreach (RpgLandmarkQuestDefinition quest in RpgWorldLandmarkCatalog.GetQuests(spec.MapId))
                {
                    string progressKey = RpgMapExplorationTracker.QuestProgressKey(quest.QuestId);
                    string rewardedKey = RpgWorldOrkExplorationBridge.QuestRewardedKey(quest.QuestId);
                    RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene(mapName, progressKey, seenKeys, keyIssues);
                    RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene(mapName, rewardedKey, seenKeys, keyIssues);
                }
            }

            Assert.LessOrEqual(totalBytes, RpgWorld500xMapIntegrityValidator.MaxTotalGridBytes, "Four-region total grid footprint exceeds budget.");
            Assert.IsEmpty(keyIssues, string.Join("\n", keyIssues));
        }

        [Test]
        public void AllFourRegions_MakinomKeyHygieneGuard_FlagsEmptyDuplicatePrefixAndNarrativeCollisions()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var issues = new List<RpgMapValidationIssue>();

            RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene("Soundwell", "   ", seen, issues);
            Assert.IsTrue(issues.Exists(i => i.RuleId == "KEY-EMPTY"));

            issues.Clear();
            RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene("RedSaltHarbor", "custom.other.key", seen, issues);
            Assert.IsTrue(issues.Exists(i => i.RuleId == "KEY-PREFIX"));

            foreach (string reserved in new[] { "rpg.return.m00.flag", "rpg.return.st01.flag", "rpg.return.rc05.flag" })
            {
                issues.Clear();
                RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene("MineCamp", reserved, seen, issues);
                Assert.IsTrue(issues.Exists(i => i.RuleId == "KEY-COLLISION"), $"Expected KEY-COLLISION for '{reserved}'.");
            }

            issues.Clear();
            const string validKey = "rpg.return.world.integrity.e2e.sample";
            RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene("WhitewaterCrossing", validKey, seen, issues);
            Assert.IsEmpty(issues);

            RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene("WhitewaterCrossing", validKey, seen, issues);
            Assert.IsTrue(issues.Exists(i => i.RuleId == "KEY-DUP"));

            var withTile = new RpgMapValidationIssue("Soundwell", "TEST-RULE", "msg", new Vector2Int(12, 34));
            var withoutTile = new RpgMapValidationIssue("Soundwell", "TEST-RULE", "msg");
            StringAssert.Contains("(12,34)", withTile.ToString());
            Assert.AreEqual("[Soundwell][TEST-RULE] msg", withoutTile.ToString());
        }
    }
}
