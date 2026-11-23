using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum ST04ValleyEntry
    {
        EnterQuietly = 1,
        SurveyEchoesFromRidge = 2,
        FollowSangJingAndBell = 3
    }

    public enum ST04ChamberEntry
    {
        ClearGuardBeast = 1,
        LureWithBell = 2,
        OpenVentGrate = 3
    }

    public enum ST04BeastHandling
    {
        Slain = 1,
        LedBackToOldHabitat = 2,
        ContainedAwayFromClosedRoad = 3
    }

    public enum ST04RoadResolution
    {
        KillAdultsAndCloseWell = 1,
        ReturnBeastsAndUseCostlyLongRoad = 2,
        PreserveRainWallAndCloseRoad = 3
    }

    public enum ST04StoneShipment
    {
        NoOrder = 0,
        OrderAccepted = 1,
        DeliveredToLinchao = 2
    }

    /// <summary>
    /// ST-04 ("钟岭的回声会咬人") regional commitment, without inventing
    /// a lost scout, a beast fight, or a shipment before the scene plays.
    /// The decision is permanent, and sound rules have no mind-reading effect.
    /// </summary>
    public readonly struct ST04EchoValleyRecord
    {
        public ST04EchoValleyRecord(
            ST04ValleyEntry entry, ST04ChamberEntry chamber,
            ST04BeastHandling handling, ST04RoadResolution resolution,
            int rescuedSurveyors, bool echoRulesVerified,
            bool companionsMarked, bool drainOpened,
            bool longRoadFunded, bool alternativeWorksiteReady)
        {
            Entry = entry;
            Chamber = chamber;
            Handling = handling;
            Resolution = resolution;
            RescuedSurveyors = rescuedSurveyors;
            EchoRulesVerified = echoRulesVerified;
            CompanionsMarked = companionsMarked;
            DrainOpened = drainOpened;
            LongRoadFunded = longRoadFunded;
            AlternativeWorksiteReady = alternativeWorksiteReady;
        }

        public ST04ValleyEntry Entry { get; }
        public ST04ChamberEntry Chamber { get; }
        public ST04BeastHandling Handling { get; }
        public ST04RoadResolution Resolution { get; }
        public int RescuedSurveyors { get; }
        public bool EchoRulesVerified { get; }
        public bool CompanionsMarked { get; }
        public bool DrainOpened { get; }
        public bool LongRoadFunded { get; }
        public bool AlternativeWorksiteReady { get; }

        public bool RainWallPartlyCollapsed =>
            Resolution == ST04RoadResolution.KillAdultsAndCloseWell;
        public bool UsesFundedLongRoad =>
            Resolution == ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad;
        public bool OriginalTransportRoadPermanentlyClosed =>
            Resolution == ST04RoadResolution.PreserveRainWallAndCloseRoad;
    }

    public readonly struct ST04EchoValleyOutcome
    {
        public ST04EchoValleyOutcome(ST04EchoValleyRecord story,
            bool alternativeStoneTransportVerified, ST04StoneShipment shipment)
        {
            Story = story;
            AlternativeStoneTransportVerified = alternativeStoneTransportVerified;
            Shipment = shipment;
        }

        public ST04EchoValleyRecord Story { get; }
        public bool AlternativeStoneTransportVerified { get; }
        public ST04StoneShipment Shipment { get; }

        // The first two endings have actual usable routes. Preserving the
        // installation shuts the original road permanently; an extra
        // independently verified transport route is required in that ending.
        public bool HasProvenZhonglingStoneRoute =>
            Story.Resolution != ST04RoadResolution.PreserveRainWallAndCloseRoad ||
            AlternativeStoneTransportVerified;
        public bool ShowZhonglingStoneOnM15Monument =>
            HasProvenZhonglingStoneRoute &&
            Shipment == ST04StoneShipment.DeliveredToLinchao;
    }

    /// <summary>
    /// Persistent Makinom facts for ST-04. A deliberate new-game reset is the
    /// ONLY reset path. Unlike an M03/M04 voyage replay, the quarry choice
    /// and later actual shipment remain permanent regional history.
    /// </summary>
    public static class ST04EchoValleyFacts
    {
        public const string ConfirmedKey = "rpg.return.st04.echo-valley-confirmed";
        public const string IntentKey = "rpg.return.st04.echo-valley-intent";
        public const string EntryKey = "rpg.return.st04.entry";
        public const string ChamberKey = "rpg.return.st04.chamber";
        public const string HandlingKey = "rpg.return.st04.beasts";
        public const string ResolutionKey = "rpg.return.st04.resolution";
        public const string SurveyorsKey = "rpg.return.st04.surveyors-rescued";
        public const string EchoVerifiedKey = "rpg.return.st04.echo-rules-verified";
        public const string CompanionsMarkedKey = "rpg.return.st04.companions-marked";
        public const string DrainOpenedKey = "rpg.return.st04.drain-opened";
        public const string FundedRoadKey = "rpg.return.st04.long-road-funded";
        public const string AlternativeWorksiteKey = "rpg.return.st04.alternative-worksite";
        public const string AlternativeTransportKey = "rpg.return.st04.extra-stone-transport-verified";
        public const string StoneShipmentKey = "rpg.return.st04.stone-shipment-status";
        public const string StoneOrderEvidenceKey = "rpg.return.st04.stone-order-acknowledged";
        public const string StoneDeliveryEvidenceKey = "rpg.return.st04.stone-delivery-acknowledged";

        public static bool HasOrphanedDownstreamFacts(Func<string, int> read)
        {
            return read == null ||
                M15FinalRollCallFacts.HasAnyPersistedFacts(read) ||
                M16EpilogueFacts.HasAnyPersistedFacts(read);
        }

        private static int Signature(ST04EchoValleyRecord record)
        {
            return 100000 + (int)record.Entry * 10000 +
                (int)record.Chamber * 1000 + (int)record.Handling * 100 +
                (int)record.Resolution * 10 + (record.LongRoadFunded ? 2 : 0) +
                (record.AlternativeWorksiteReady ? 1 : 0);
        }

        public static bool IsValidRecord(ST04EchoValleyRecord record)
        {
            if (!Enum.IsDefined(typeof(ST04ValleyEntry), record.Entry) ||
                !Enum.IsDefined(typeof(ST04ChamberEntry), record.Chamber) ||
                !Enum.IsDefined(typeof(ST04BeastHandling), record.Handling) ||
                !Enum.IsDefined(typeof(ST04RoadResolution), record.Resolution) ||
                record.RescuedSurveyors != 2 ||
                !record.EchoRulesVerified || !record.CompanionsMarked ||
                !record.DrainOpened)
                return false;

            switch (record.Resolution)
            {
                case ST04RoadResolution.KillAdultsAndCloseWell:
                    return record.Handling == ST04BeastHandling.Slain &&
                        !record.LongRoadFunded && !record.AlternativeWorksiteReady;
                case ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad:
                    return record.Handling == ST04BeastHandling.LedBackToOldHabitat &&
                        record.LongRoadFunded && !record.AlternativeWorksiteReady;
                case ST04RoadResolution.PreserveRainWallAndCloseRoad:
                    return record.Handling == ST04BeastHandling.ContainedAwayFromClosedRoad &&
                        !record.LongRoadFunded && record.AlternativeWorksiteReady;
                default:
                    return false;
            }
        }

        private static bool PendingMatches(Func<string, int> read,
            string key, int expected)
        {
            int raw = read(key);
            return raw == 0 || raw == expected;
        }

        private static bool AllUncommittedValuesEmpty(Func<string, int> read)
        {
            return read(IntentKey) == 0 && read(EntryKey) == 0 &&
                read(ChamberKey) == 0 && read(HandlingKey) == 0 &&
                read(ResolutionKey) == 0 && read(SurveyorsKey) == 0 &&
                read(EchoVerifiedKey) == 0 && read(CompanionsMarkedKey) == 0 &&
                read(DrainOpenedKey) == 0 && read(FundedRoadKey) == 0 &&
                read(AlternativeWorksiteKey) == 0 &&
                read(AlternativeTransportKey) == 0 && read(StoneShipmentKey) == 0 &&
                read(StoneOrderEvidenceKey) == 0 && read(StoneDeliveryEvidenceKey) == 0;
        }

        public static bool CanRecord(ST04EchoValleyRecord record, Func<string, int> read)
        {
            if (read == null || read(ConfirmedKey) != 0 ||
                !IsValidRecord(record) || HasOrphanedDownstreamFacts(read))
                return false;
            int intent = read(IntentKey);
            if (intent == 0)
                return AllUncommittedValuesEmpty(read);

            return intent == Signature(record) &&
                PendingMatches(read, EntryKey, (int)record.Entry) &&
                PendingMatches(read, ChamberKey, (int)record.Chamber) &&
                PendingMatches(read, HandlingKey, (int)record.Handling) &&
                PendingMatches(read, ResolutionKey, (int)record.Resolution) &&
                PendingMatches(read, SurveyorsKey, 2) &&
                PendingMatches(read, EchoVerifiedKey, 1) &&
                PendingMatches(read, CompanionsMarkedKey, 1) &&
                PendingMatches(read, DrainOpenedKey, 1) &&
                PendingMatches(read, FundedRoadKey, record.LongRoadFunded ? 1 : 0) &&
                PendingMatches(read, AlternativeWorksiteKey,
                    record.AlternativeWorksiteReady ? 1 : 0) &&
                read(AlternativeTransportKey) == 0 && read(StoneShipmentKey) == 0 &&
                read(StoneOrderEvidenceKey) == 0 && read(StoneDeliveryEvidenceKey) == 0;
        }

        public static bool TryRecord(ST04EchoValleyRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(record,
                key => variables.GetInt(key), (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(ST04EchoValleyRecord record,
            Func<string, int> read, Action<string, int> write)
        {
            if (write == null || !CanRecord(record, read))
                return false;

            // Write intent FIRST and the visible commit marker LAST.
            // A torn save can only resume this identical choice.
            write(IntentKey, Signature(record));
            write(EntryKey, (int)record.Entry);
            write(ChamberKey, (int)record.Chamber);
            write(HandlingKey, (int)record.Handling);
            write(ResolutionKey, (int)record.Resolution);
            write(SurveyorsKey, 2);
            write(EchoVerifiedKey, 1);
            write(CompanionsMarkedKey, 1);
            write(DrainOpenedKey, 1);
            write(FundedRoadKey, record.LongRoadFunded ? 1 : 0);
            write(AlternativeWorksiteKey, record.AlternativeWorksiteReady ? 1 : 0);
            write(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out ST04EchoValleyOutcome? outcome)
        {
            outcome = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(
                key => variables.GetInt(key), out outcome);
        }

        public static bool TryRead(Func<string, int> read,
            out ST04EchoValleyOutcome? outcome)
        {
            outcome = null;
            if (read == null)
                return false;
            int confirmed = read(ConfirmedKey);
            if (confirmed == 0)
                return AllUncommittedValuesEmpty(read);
            if (confirmed != 1)
                return false;

            int rawEcho = read(EchoVerifiedKey);
            int rawMarked = read(CompanionsMarkedKey);
            int rawDrain = read(DrainOpenedKey);
            int rawFunded = read(FundedRoadKey);
            int rawWorksite = read(AlternativeWorksiteKey);
            int rawExtraRoute = read(AlternativeTransportKey);
            int rawShipment = read(StoneShipmentKey);
            int rawOrder = read(StoneOrderEvidenceKey);
            int rawDelivery = read(StoneDeliveryEvidenceKey);
            if (rawEcho != 1 || rawMarked != 1 || rawDrain != 1 ||
                (rawFunded != 0 && rawFunded != 1) ||
                (rawWorksite != 0 && rawWorksite != 1) ||
                (rawExtraRoute != 0 && rawExtraRoute != 1) ||
                !Enum.IsDefined(typeof(ST04StoneShipment), rawShipment) ||
                (rawOrder != 0 && rawOrder != 1) ||
                (rawDelivery != 0 && rawDelivery != 1))
                return false;

            // A status enum by itself cannot establish real quarry delivery.
            // Every milestone is independently acknowledged. The intermediate
            // half-written state fails closed, without inventing a shipment.
            if ((rawShipment == 0 && (rawOrder != 0 || rawDelivery != 0)) ||
                (rawShipment == 1 && (rawOrder != 1 || rawDelivery != 0)) ||
                (rawShipment == 2 && (rawOrder != 1 || rawDelivery != 1)))
                return false;

            var record = new ST04EchoValleyRecord(
                (ST04ValleyEntry)read(EntryKey),
                (ST04ChamberEntry)read(ChamberKey),
                (ST04BeastHandling)read(HandlingKey),
                (ST04RoadResolution)read(ResolutionKey),
                read(SurveyorsKey), true, true, true,
                rawFunded == 1, rawWorksite == 1);
            if (!IsValidRecord(record) || read(IntentKey) != Signature(record))
                return false;
            if (rawExtraRoute == 1 &&
                record.Resolution != ST04RoadResolution.PreserveRainWallAndCloseRoad)
                return false;

            var saved = new ST04EchoValleyOutcome(
                record, rawExtraRoute == 1, (ST04StoneShipment)rawShipment);
            if (saved.Shipment != ST04StoneShipment.NoOrder &&
                !saved.HasProvenZhonglingStoneRoute)
                return false;
            outcome = saved;
            return true;
        }

        public static bool TryVerifyAdditionalStoneTransport(
            Func<string, int> read, Action<string, int> write)
        {
            if (write == null || HasOrphanedDownstreamFacts(read) ||
                !TryRead(read, out ST04EchoValleyOutcome? saved) ||
                !saved.HasValue ||
                saved.Value.Story.Resolution != ST04RoadResolution.PreserveRainWallAndCloseRoad)
                return false;
            if (saved.Value.AlternativeStoneTransportVerified)
                return true;
            if (saved.Value.Shipment != ST04StoneShipment.NoOrder)
                return false;
            // Only call after a playable scene verifies a real alternative
            // Zhongling transport route. An alternative quarry is NOT enough.
            write(AlternativeTransportKey, 1);
            return true;
        }

        // Read an interrupted milestone as the previous committed phase,
        // solely to authorize an identical retry. Public TryRead remains
        // fail-closed until the phase marker is complete.
        private static bool TryReadPreviousMilestone(Func<string, int> read,
            string pendingEvidence, out ST04EchoValleyOutcome? record)
        {
            return TryRead(key => key == pendingEvidence ? 0 : read(key),
                out record);
        }

        public static bool TryMarkStoneOrderAccepted(
            Func<string, int> read, Action<string, int> write)
        {
            if (read == null || write == null || HasOrphanedDownstreamFacts(read))
                return false;
            if (TryRead(read, out ST04EchoValleyOutcome? saved) &&
                saved.HasValue && saved.Value.HasProvenZhonglingStoneRoute)
            {
                if (saved.Value.Shipment != ST04StoneShipment.NoOrder)
                    return true;
            }
            else
            {
                if (read(StoneOrderEvidenceKey) != 1 ||
                    read(StoneDeliveryEvidenceKey) != 0 ||
                    read(StoneShipmentKey) != 0 ||
                    !TryReadPreviousMilestone(read, StoneOrderEvidenceKey,
                        out saved) || !saved.HasValue ||
                    !saved.Value.HasProvenZhonglingStoneRoute ||
                    saved.Value.Shipment != ST04StoneShipment.NoOrder)
                    return false;
                write(StoneShipmentKey, (int)ST04StoneShipment.OrderAccepted);
                return true;
            }

            write(StoneOrderEvidenceKey, 1);
            write(StoneShipmentKey, (int)ST04StoneShipment.OrderAccepted);
            return true;
        }

        public static bool TryMarkStoneDelivered(
            Func<string, int> read, Action<string, int> write)
        {
            if (read == null || write == null || HasOrphanedDownstreamFacts(read))
                return false;
            if (TryRead(read, out ST04EchoValleyOutcome? saved) &&
                saved.HasValue && saved.Value.HasProvenZhonglingStoneRoute)
            {
                if (saved.Value.Shipment == ST04StoneShipment.DeliveredToLinchao)
                    return true;
                if (saved.Value.Shipment != ST04StoneShipment.OrderAccepted)
                    return false;
            }
            else
            {
                if (read(StoneOrderEvidenceKey) != 1 ||
                    read(StoneDeliveryEvidenceKey) != 1 ||
                    read(StoneShipmentKey) != (int)ST04StoneShipment.OrderAccepted ||
                    !TryReadPreviousMilestone(read, StoneDeliveryEvidenceKey,
                        out saved) || !saved.HasValue ||
                    !saved.Value.HasProvenZhonglingStoneRoute ||
                    saved.Value.Shipment != ST04StoneShipment.OrderAccepted)
                    return false;
                write(StoneShipmentKey, (int)ST04StoneShipment.DeliveredToLinchao);
                return true;
            }

            write(StoneDeliveryEvidenceKey, 1);
            write(StoneShipmentKey, (int)ST04StoneShipment.DeliveredToLinchao);
            return true;
        }

        public static bool TryResetForNewGame(Action<string, int> write)
        {
            if (write == null) return false;
            write(ConfirmedKey, 0);
            write(IntentKey, 0);
            write(EntryKey, 0);
            write(ChamberKey, 0);
            write(HandlingKey, 0);
            write(ResolutionKey, 0);
            write(SurveyorsKey, 0);
            write(EchoVerifiedKey, 0);
            write(CompanionsMarkedKey, 0);
            write(DrainOpenedKey, 0);
            write(FundedRoadKey, 0);
            write(AlternativeWorksiteKey, 0);
            write(AlternativeTransportKey, 0);
            write(StoneShipmentKey, 0);
            write(StoneOrderEvidenceKey, 0);
            write(StoneDeliveryEvidenceKey, 0);
            return true;
        }
    }
}
