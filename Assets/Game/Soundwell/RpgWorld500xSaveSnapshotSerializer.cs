using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Rpg.Soundwell
{
    [Serializable]
    public struct RpgWorld500xIntKeyEntry
    {
        public string key;
        public int value;
    }

    [Serializable]
    public struct RpgWorld500xStringKeyEntry
    {
        public string key;
        public string value;
    }

    [Serializable]
    public sealed class RpgWorld500xSaveSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public int activeMapId;
        public float playerTileX;
        public float playerTileY;
        public List<RpgWorld500xIntKeyEntry> worldIntFlags = new List<RpgWorld500xIntKeyEntry>();
        public List<RpgWorld500xStringKeyEntry> atmosphereState = new List<RpgWorld500xStringKeyEntry>();
        public string checksum = string.Empty;
    }

    /// <summary>
    /// Deterministic, versioned, checksum-verified JSON snapshot serializer for the
    /// 500x-area RPG world state. Strictly filters keys to the rpg.return.world.*
    /// namespace so main-story M00-M16 / ST01-ST06 / RC01-RC05 flags are never clobbered.
    /// </summary>
    public static class RpgWorld500xSaveSnapshotSerializer
    {
        public static RpgWorld500xSaveSnapshot Capture(
            RpgWorldMapId activeMapId,
            Vector2 playerTile,
            IReadOnlyDictionary<string, int> intStore,
            RpgWorld500xWeatherAndTimeOfDayService atmosphere = null)
        {
            var snapshot = new RpgWorld500xSaveSnapshot
            {
                schemaVersion = RpgWorld500xSaveSnapshot.CurrentSchemaVersion,
                activeMapId = (int)activeMapId,
                playerTileX = IsFinite(playerTile.x) ? playerTile.x : 0f,
                playerTileY = IsFinite(playerTile.y) ? playerTile.y : 0f,
                worldIntFlags = new List<RpgWorld500xIntKeyEntry>(),
                atmosphereState = new List<RpgWorld500xStringKeyEntry>()
            };

            if (intStore != null)
            {
                var keys = new List<string>();
                foreach (string k in intStore.Keys)
                {
                    if (RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(k))
                    {
                        keys.Add(k);
                    }
                }

                keys.Sort(StringComparer.Ordinal);
                for (int i = 0; i < keys.Count; i++)
                {
                    string k = keys[i];
                    snapshot.worldIntFlags.Add(new RpgWorld500xIntKeyEntry
                    {
                        key = k,
                        value = intStore[k]
                    });
                }
            }

            if (atmosphere != null)
            {
                Dictionary<string, string> exported = atmosphere.ExportPersistenceState();
                var keys = new List<string>(exported.Keys);
                keys.Sort(StringComparer.Ordinal);
                for (int i = 0; i < keys.Count; i++)
                {
                    string k = keys[i];
                    if (RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(k))
                    {
                        snapshot.atmosphereState.Add(new RpgWorld500xStringKeyEntry
                        {
                            key = k,
                            value = exported[k] ?? string.Empty
                        });
                    }
                }
            }

            snapshot.checksum = ComputeChecksum(snapshot);
            return snapshot;
        }

        public static string Serialize(RpgWorld500xSaveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            snapshot.checksum = ComputeChecksum(snapshot);
            return JsonUtility.ToJson(snapshot, false);
        }

        public static bool TryDeserialize(
            string json,
            out RpgWorld500xSaveSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Save JSON is empty.";
                return false;
            }

            RpgWorld500xSaveSnapshot parsed;
            try
            {
                parsed = JsonUtility.FromJson<RpgWorld500xSaveSnapshot>(json);
            }
            catch (Exception ex)
            {
                error = "Malformed save JSON: " + ex.Message;
                return false;
            }

            if (parsed == null)
            {
                error = "Save JSON deserialized to null.";
                return false;
            }

            if (parsed.schemaVersion != RpgWorld500xSaveSnapshot.CurrentSchemaVersion)
            {
                error = "Unsupported schemaVersion: " + parsed.schemaVersion;
                return false;
            }

            if (!Enum.IsDefined(typeof(RpgWorldMapId), parsed.activeMapId))
            {
                error = "Invalid activeMapId: " + parsed.activeMapId;
                return false;
            }

            if (!IsFinite(parsed.playerTileX) || !IsFinite(parsed.playerTileY))
            {
                error = "Non-finite player coordinates.";
                return false;
            }

            string expectedChecksum = ComputeChecksum(parsed);
            if (!string.Equals(parsed.checksum, expectedChecksum, StringComparison.Ordinal))
            {
                error = "Save checksum mismatch (data corrupted or tampered).";
                return false;
            }

            snapshot = parsed;
            return true;
        }

        public static void ApplyToStore(
            RpgWorld500xSaveSnapshot snapshot,
            Action<string, int> setInt,
            RpgWorld500xWeatherAndTimeOfDayService atmosphere = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (setInt != null && snapshot.worldIntFlags != null)
            {
                for (int i = 0; i < snapshot.worldIntFlags.Count; i++)
                {
                    RpgWorld500xIntKeyEntry entry = snapshot.worldIntFlags[i];
                    if (RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(entry.key))
                    {
                        setInt(entry.key, entry.value);
                    }
                }
            }

            if (atmosphere != null && snapshot.atmosphereState != null)
            {
                var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 0; i < snapshot.atmosphereState.Count; i++)
                {
                    RpgWorld500xStringKeyEntry entry = snapshot.atmosphereState[i];
                    if (RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(entry.key))
                    {
                        dict[entry.key] = entry.value ?? string.Empty;
                    }
                }

                atmosphere.ImportPersistenceState(dict);
            }
        }

        public static string ComputeChecksum(RpgWorld500xSaveSnapshot snapshot)
        {
            var sb = new StringBuilder(512);
            sb.Append(snapshot.schemaVersion).Append('|')
              .Append(snapshot.activeMapId).Append('|')
              .Append(snapshot.playerTileX.ToString("F2", CultureInfo.InvariantCulture)).Append('|')
              .Append(snapshot.playerTileY.ToString("F2", CultureInfo.InvariantCulture)).Append('|');

            if (snapshot.worldIntFlags != null)
            {
                for (int i = 0; i < snapshot.worldIntFlags.Count; i++)
                {
                    sb.Append(snapshot.worldIntFlags[i].key).Append('=')
                      .Append(snapshot.worldIntFlags[i].value).Append(';');
                }
            }

            sb.Append('|');
            if (snapshot.atmosphereState != null)
            {
                for (int i = 0; i < snapshot.atmosphereState.Count; i++)
                {
                    sb.Append(snapshot.atmosphereState[i].key).Append('=')
                      .Append(snapshot.atmosphereState[i].value).Append(';');
                }
            }

            // Deterministic 64-bit FNV-1a hex digest.
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offsetBasis;
            string payload = sb.ToString();
            for (int i = 0; i < payload.Length; i++)
            {
                hash ^= payload[i];
                hash *= prime;
            }

            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
