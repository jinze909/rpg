using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xSaveSlotId : byte
    {
        AutoSave = 0,
        Slot1 = 1,
        Slot2 = 2,
        Slot3 = 3
    }

    public interface IRpgWorld500xSaveSlotStorage
    {
        bool TryRead(string storageKey, out string payload);
        void Write(string storageKey, string payload);
        void Delete(string storageKey);
    }

    public sealed class RpgWorld500xInMemorySaveSlotStorage : IRpgWorld500xSaveSlotStorage
    {
        private readonly Dictionary<string, string> _entries =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public bool TryRead(string storageKey, out string payload)
        {
            if (string.IsNullOrEmpty(storageKey))
            {
                payload = string.Empty;
                return false;
            }

            return _entries.TryGetValue(storageKey, out payload);
        }

        public void Write(string storageKey, string payload)
        {
            if (string.IsNullOrEmpty(storageKey))
            {
                return;
            }

            _entries[storageKey] = payload ?? string.Empty;
        }

        public void Delete(string storageKey)
        {
            if (string.IsNullOrEmpty(storageKey))
            {
                return;
            }

            _entries.Remove(storageKey);
        }
    }

    public sealed class RpgWorld500xPlayerPrefsSaveSlotStorage : IRpgWorld500xSaveSlotStorage
    {
        public bool TryRead(string storageKey, out string payload)
        {
            payload = string.Empty;
            if (string.IsNullOrEmpty(storageKey) || !PlayerPrefs.HasKey(storageKey))
            {
                return false;
            }

            payload = PlayerPrefs.GetString(storageKey, string.Empty);
            return !string.IsNullOrEmpty(payload);
        }

        public void Write(string storageKey, string payload)
        {
            if (string.IsNullOrEmpty(storageKey))
            {
                return;
            }

            PlayerPrefs.SetString(storageKey, payload ?? string.Empty);
            PlayerPrefs.Save();
        }

        public void Delete(string storageKey)
        {
            if (string.IsNullOrEmpty(storageKey))
            {
                return;
            }

            if (PlayerPrefs.HasKey(storageKey))
            {
                PlayerPrefs.DeleteKey(storageKey);
                PlayerPrefs.Save();
            }
        }
    }

    public readonly struct RpgWorld500xSaveSlotSummary
    {
        public RpgWorld500xSaveSlotSummary(
            RpgWorld500xSaveSlotId slotId,
            string slotDisplayNameZh,
            bool isOccupied,
            bool isCorrupted,
            string diagnosticMessageZh,
            RpgWorldMapId mapId,
            string mapDisplayNameZh,
            int saltSilverBalance,
            int worldFlagCount,
            int dayCount,
            string formattedClock,
            string savedAtUtcIso,
            string customLabelZh)
        {
            SlotId = slotId;
            SlotDisplayNameZh = slotDisplayNameZh ?? string.Empty;
            IsOccupied = isOccupied;
            IsCorrupted = isCorrupted;
            DiagnosticMessageZh = diagnosticMessageZh ?? string.Empty;
            MapId = mapId;
            MapDisplayNameZh = mapDisplayNameZh ?? string.Empty;
            SaltSilverBalance = saltSilverBalance;
            WorldFlagCount = worldFlagCount;
            DayCount = dayCount;
            FormattedClock = formattedClock ?? string.Empty;
            SavedAtUtcIso = savedAtUtcIso ?? string.Empty;
            CustomLabelZh = customLabelZh ?? string.Empty;
        }

        public RpgWorld500xSaveSlotId SlotId { get; }
        public string SlotDisplayNameZh { get; }
        public bool IsOccupied { get; }
        public bool IsCorrupted { get; }
        public string DiagnosticMessageZh { get; }
        public RpgWorldMapId MapId { get; }
        public string MapDisplayNameZh { get; }
        public int SaltSilverBalance { get; }
        public int WorldFlagCount { get; }
        public int DayCount { get; }
        public string FormattedClock { get; }
        public string SavedAtUtcIso { get; }
        public string CustomLabelZh { get; }
    }

    /// <summary>
    /// Multi-slot expedition save manager for the 500x-area RPG world (1 AutoSave + 3 manual slots).
    /// Wraps RpgWorld500xSaveSnapshotSerializer with versioned envelopes, corrupt-slot isolation,
    /// deterministic metadata summaries, and strict rpg.return.world.save_slot.* key isolation.
    /// </summary>
    public sealed class RpgWorld500xSaveSlotManager
    {
        public const string StorageKeyPrefix = "rpg.return.world.save_slot.";
        private const string SaltSilverKey = "rpg.return.world.supply.salt_silver_balance";
        private const int CurrentEnvelopeVersion = 1;
        private const string LegacyTimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
        private const string PreciseTimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

        private static readonly RpgWorld500xSaveSlotId[] OrderedSlots =
        {
            RpgWorld500xSaveSlotId.AutoSave,
            RpgWorld500xSaveSlotId.Slot1,
            RpgWorld500xSaveSlotId.Slot2,
            RpgWorld500xSaveSlotId.Slot3
        };

        [Serializable]
        private sealed class SaveSlotEnvelope
        {
            public int envelopeVersion = CurrentEnvelopeVersion;
            public string savedAtUtcIso = string.Empty;
            public string customLabelZh = string.Empty;
            public string snapshotJson = string.Empty;
        }

        private readonly IRpgWorld500xSaveSlotStorage _storage;
        private readonly Func<DateTime> _utcNowProvider;

        public RpgWorld500xSaveSlotManager(
            IRpgWorld500xSaveSlotStorage storage = null,
            Func<DateTime> utcNowProvider = null)
        {
            _storage = storage ?? new RpgWorld500xInMemorySaveSlotStorage();
            _utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
        }

        public static IReadOnlyList<RpgWorld500xSaveSlotId> AllSlots => OrderedSlots;

        private static bool IsKnownSlot(RpgWorld500xSaveSlotId slotId) =>
            slotId == RpgWorld500xSaveSlotId.AutoSave || slotId == RpgWorld500xSaveSlotId.Slot1 ||
            slotId == RpgWorld500xSaveSlotId.Slot2 || slotId == RpgWorld500xSaveSlotId.Slot3;

        public static string GetStorageKey(RpgWorld500xSaveSlotId slotId)
        {
            switch (slotId)
            {
                case RpgWorld500xSaveSlotId.Slot1: return StorageKeyPrefix + "slot_1";
                case RpgWorld500xSaveSlotId.Slot2: return StorageKeyPrefix + "slot_2";
                case RpgWorld500xSaveSlotId.Slot3: return StorageKeyPrefix + "slot_3";
                case RpgWorld500xSaveSlotId.AutoSave: return StorageKeyPrefix + "autosave";
                default: throw new ArgumentOutOfRangeException(nameof(slotId), slotId, "Unknown save slot.");
            }
        }

        public static string GetSlotDisplayNameZh(RpgWorld500xSaveSlotId slotId)
        {
            switch (slotId)
            {
                case RpgWorld500xSaveSlotId.Slot1: return "远征存档一";
                case RpgWorld500xSaveSlotId.Slot2: return "远征存档二";
                case RpgWorld500xSaveSlotId.Slot3: return "远征存档三";
                case RpgWorld500xSaveSlotId.AutoSave: return "自动存档";
                default: return "无效档位";
            }
        }

        public bool TrySaveSlot(
            RpgWorld500xSceneBootstrapper bootstrapper,
            RpgWorld500xSaveSlotId slotId,
            out RpgWorld500xSaveSlotSummary summary,
            out string error,
            string customLabelZh = null)
        {
            summary = BuildEmptySummary(slotId);
            error = string.Empty;

            if (!IsKnownSlot(slotId))
            {
                error = "Unknown save slot.";
                return false;
            }

            if (bootstrapper == null || bootstrapper.IsDisposed)
            {
                error = "Bootstrapper is null or disposed.";
                return false;
            }

            // In-flight encounters are deliberately transient and are not
            // serialized. Never overwrite a usable checkpoint with an
            // incomplete encounter state, including via the quick-save key.
            if (bootstrapper.Driver.Facade.Session.Host.Bridge.IsInDangerEncounter)
            {
                error = "遭遇战进行中，结束后可存档";
                return false;
            }

            string snapshotJson = bootstrapper.ExportSaveJson();
            if (!RpgWorld500xSaveSnapshotSerializer.TryDeserialize(
                snapshotJson, out RpgWorld500xSaveSnapshot verifiedSnapshot, out error))
            {
                return false;
            }

            DateTime utcNow = _utcNowProvider().ToUniversalTime();
            // Preserve legacy whole-second strings, but retain sub-second ordering
            // when a manual checkpoint follows an autosave within the same second.
            string utcIso = utcNow.ToString(
                utcNow.Ticks % TimeSpan.TicksPerSecond == 0 ? LegacyTimestampFormat : PreciseTimestampFormat,
                CultureInfo.InvariantCulture);
            var envelope = new SaveSlotEnvelope
            {
                envelopeVersion = CurrentEnvelopeVersion,
                savedAtUtcIso = utcIso,
                customLabelZh = string.IsNullOrWhiteSpace(customLabelZh) ? string.Empty : customLabelZh.Trim(),
                snapshotJson = snapshotJson
            };

            string encoded = JsonUtility.ToJson(envelope, false);
            _storage.Write(GetStorageKey(slotId), encoded);
            summary = BuildValidSummary(slotId, envelope, verifiedSnapshot);
            return true;
        }

        public bool TryAutoSave(
            RpgWorld500xSceneBootstrapper bootstrapper,
            out RpgWorld500xSaveSlotSummary summary,
            out string error,
            string reasonLabelZh = null)
        {
            string label = string.IsNullOrWhiteSpace(reasonLabelZh) ? "营地自动记录" : reasonLabelZh.Trim();
            return TrySaveSlot(bootstrapper, RpgWorld500xSaveSlotId.AutoSave, out summary, out error, label);
        }

        public bool TryLoadSlot(
            RpgWorld500xSceneBootstrapper bootstrapper,
            RpgWorld500xSaveSlotId slotId,
            out RpgWorld500xSaveSlotSummary summary,
            out string error)
        {
            summary = BuildEmptySummary(slotId);
            error = string.Empty;

            if (!IsKnownSlot(slotId))
            {
                error = "Unknown save slot.";
                return false;
            }

            if (bootstrapper == null || bootstrapper.IsDisposed)
            {
                error = "Bootstrapper is null or disposed.";
                return false;
            }

            if (!TryReadAndVerifyEnvelope(
                slotId, out SaveSlotEnvelope envelope, out RpgWorld500xSaveSnapshot snapshot, out bool occupied, out error))
            {
                summary = occupied ? BuildCorruptedSummary(slotId, error) : BuildEmptySummary(slotId);
                return false;
            }

            if (!bootstrapper.TryImportSaveJson(envelope.snapshotJson, out error))
            {
                summary = BuildCorruptedSummary(slotId, error);
                return false;
            }

            summary = BuildValidSummary(slotId, envelope, snapshot);
            return true;
        }

        public bool TryCopySlot(
            RpgWorld500xSaveSlotId sourceSlotId,
            RpgWorld500xSaveSlotId targetSlotId,
            out string error)
        {
            error = string.Empty;
            if (!IsKnownSlot(sourceSlotId) || !IsKnownSlot(targetSlotId))
            {
                error = "Unknown save slot.";
                return false;
            }

            if (sourceSlotId == targetSlotId)
            {
                error = "Source and target save slots must differ.";
                return false;
            }

            if (!TryReadAndVerifyEnvelope(sourceSlotId, out SaveSlotEnvelope envelope, out _, out _, out error))
            {
                return false;
            }

            string encoded = JsonUtility.ToJson(envelope, false);
            _storage.Write(GetStorageKey(targetSlotId), encoded);
            return true;
        }

        public bool DeleteSlot(RpgWorld500xSaveSlotId slotId)
        {
            if (!IsKnownSlot(slotId))
            {
                return false;
            }

            string key = GetStorageKey(slotId);
            if (!_storage.TryRead(key, out _))
            {
                return false;
            }

            _storage.Delete(key);
            return true;
        }

        public RpgWorld500xSaveSlotSummary InspectSlot(RpgWorld500xSaveSlotId slotId)
        {
            if (!TryReadAndVerifyEnvelope(
                slotId, out SaveSlotEnvelope envelope, out RpgWorld500xSaveSnapshot snapshot, out bool occupied, out string error))
            {
                return occupied ? BuildCorruptedSummary(slotId, error) : BuildEmptySummary(slotId);
            }

            return BuildValidSummary(slotId, envelope, snapshot);
        }

        public List<RpgWorld500xSaveSlotSummary> ListAllSlots()
        {
            var result = new List<RpgWorld500xSaveSlotSummary>(OrderedSlots.Length);
            for (int i = 0; i < OrderedSlots.Length; i++)
            {
                result.Add(InspectSlot(OrderedSlots[i]));
            }

            return result;
        }

        public bool TryFindMostRecentValidSlot(out RpgWorld500xSaveSlotSummary summary)
        {
            summary = default;
            bool found = false;
            DateTime bestTimestamp = DateTime.MinValue;

            for (int i = 0; i < OrderedSlots.Length; i++)
            {
                RpgWorld500xSaveSlotSummary candidate = InspectSlot(OrderedSlots[i]);
                if (!candidate.IsOccupied || candidate.IsCorrupted)
                {
                    continue;
                }

                // Compare actual UTC instants, not strings: a legacy timestamp
                // ending in Z sorts after a fractional timestamp in the same second.
                DateTime timestamp = ParseSavedAtUtc(candidate.SavedAtUtcIso);
                if (!found || timestamp > bestTimestamp)
                {
                    found = true;
                    bestTimestamp = timestamp;
                    summary = candidate;
                }
            }

            return found;
        }

        private static DateTime ParseSavedAtUtc(string timestamp)
        {
            const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
            if (DateTime.TryParseExact(timestamp, PreciseTimestampFormat, CultureInfo.InvariantCulture, styles, out DateTime precise) ||
                DateTime.TryParseExact(timestamp, LegacyTimestampFormat, CultureInfo.InvariantCulture, styles, out precise))
            {
                return precise;
            }

            // A valid snapshot with damaged or missing date metadata stays loadable,
            // but cannot outrank a checkpoint with a known creation time.
            return DateTime.MinValue;
        }

        private bool TryReadAndVerifyEnvelope(
            RpgWorld500xSaveSlotId slotId,
            out SaveSlotEnvelope envelope,
            out RpgWorld500xSaveSnapshot snapshot,
            out bool occupied,
            out string error)
        {
            envelope = null;
            snapshot = null;
            occupied = false;
            error = string.Empty;

            if (!IsKnownSlot(slotId))
            {
                error = "Unknown save slot.";
                return false;
            }

            if (!_storage.TryRead(GetStorageKey(slotId), out string rawPayload) ||
                string.IsNullOrWhiteSpace(rawPayload))
            {
                error = "Save slot is empty.";
                return false;
            }

            occupied = true;
            try
            {
                envelope = JsonUtility.FromJson<SaveSlotEnvelope>(rawPayload);
            }
            catch (Exception ex)
            {
                error = "Corrupted save envelope JSON: " + ex.Message;
                return false;
            }

            if (envelope == null || envelope.envelopeVersion != CurrentEnvelopeVersion)
            {
                error = "Unsupported or null save slot envelope.";
                return false;
            }

            if (!RpgWorld500xSaveSnapshotSerializer.TryDeserialize(
                envelope.snapshotJson, out snapshot, out error))
            {
                return false;
            }

            return true;
        }

        private static RpgWorld500xSaveSlotSummary BuildEmptySummary(RpgWorld500xSaveSlotId slotId)
        {
            return new RpgWorld500xSaveSlotSummary(
                slotId,
                GetSlotDisplayNameZh(slotId),
                isOccupied: false,
                isCorrupted: false,
                diagnosticMessageZh: "空档位",
                mapId: RpgWorldMapId.Soundwell,
                mapDisplayNameZh: string.Empty,
                saltSilverBalance: 0,
                worldFlagCount: 0,
                dayCount: 0,
                formattedClock: string.Empty,
                savedAtUtcIso: string.Empty,
                customLabelZh: string.Empty);
        }

        private static RpgWorld500xSaveSlotSummary BuildCorruptedSummary(
            RpgWorld500xSaveSlotId slotId,
            string error)
        {
            return new RpgWorld500xSaveSlotSummary(
                slotId,
                GetSlotDisplayNameZh(slotId),
                isOccupied: true,
                isCorrupted: true,
                diagnosticMessageZh: "存档校验失败（已隔离保护）：" + (error ?? string.Empty),
                mapId: RpgWorldMapId.Soundwell,
                mapDisplayNameZh: string.Empty,
                saltSilverBalance: 0,
                worldFlagCount: 0,
                dayCount: 0,
                formattedClock: string.Empty,
                savedAtUtcIso: string.Empty,
                customLabelZh: string.Empty);
        }

        private static RpgWorld500xSaveSlotSummary BuildValidSummary(
            RpgWorld500xSaveSlotId slotId,
            SaveSlotEnvelope envelope,
            RpgWorld500xSaveSnapshot snapshot)
        {
            RpgWorldMapId mapId = (RpgWorldMapId)snapshot.activeMapId;
            int saltSilver = 0;
            int flagCount = snapshot.worldIntFlags != null ? snapshot.worldIntFlags.Count : 0;
            if (snapshot.worldIntFlags != null)
            {
                for (int i = 0; i < snapshot.worldIntFlags.Count; i++)
                {
                    if (string.Equals(snapshot.worldIntFlags[i].key, SaltSilverKey, StringComparison.Ordinal))
                    {
                        saltSilver = snapshot.worldIntFlags[i].value;
                        break;
                    }
                }
            }

            var atmo = new RpgWorld500xWeatherAndTimeOfDayService();
            if (snapshot.atmosphereState != null && snapshot.atmosphereState.Count > 0)
            {
                var atmoDict = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 0; i < snapshot.atmosphereState.Count; i++)
                {
                    atmoDict[snapshot.atmosphereState[i].key] = snapshot.atmosphereState[i].value ?? string.Empty;
                }

                atmo.ImportPersistenceState(atmoDict);
            }

            RpgWorld500xAtmosphereSnapshot atmoSnap = atmo.GetSnapshot(mapId);

            return new RpgWorld500xSaveSlotSummary(
                slotId,
                GetSlotDisplayNameZh(slotId),
                isOccupied: true,
                isCorrupted: false,
                diagnosticMessageZh: "校验通过",
                mapId: mapId,
                mapDisplayNameZh: ResolveMapDisplayNameZh(mapId),
                saltSilverBalance: saltSilver,
                worldFlagCount: flagCount,
                dayCount: atmoSnap.DayCount,
                formattedClock: atmoSnap.FormattedClock,
                savedAtUtcIso: envelope.savedAtUtcIso ?? string.Empty,
                customLabelZh: envelope.customLabelZh ?? string.Empty);
        }

        private static string ResolveMapDisplayNameZh(RpgWorldMapId mapId)
        {
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                if (spec.MapId == mapId)
                {
                    return spec.DisplayNameZh;
                }
            }

            return mapId.ToString();
        }
    }
}
