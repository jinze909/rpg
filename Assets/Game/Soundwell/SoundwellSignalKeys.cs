namespace Rpg.Soundwell
{
    // Values are persisted by Makinom. Keep numeric ordering stable for
    // previously saved scenes and the existing Play Mode replay tests.
    public enum BellSignal { Ready, Ringing, Wrapped, Searching }

    // One source of truth for the game's Makinom-owned scene signals.
    // These are not a second quest, item, combat or save authority.
    public static class SoundwellSignalKeys
    {
        public const string SignalKey = "rpg.soundwell.signal-trial.bell";
        public const string CarryKey = "rpg.soundwell.signal-trial.carrying-bell";
        public const string GateKey = "rpg.soundwell.signal-trial.inner-gate";
        public const string SurveyorClueKey = "rpg.soundwell.signal-trial.surveyor-route";
        public const string PlankSecuredKey = "rpg.soundwell.signal-trial.upper-plank-secured";
        public const string LowerPlankBrokenKey = "rpg.soundwell.signal-trial.lower-plank-broken";
        public const string EscortKey = "rpg.soundwell.signal-trial.surveyor-escorted";
        public const string BeastContactKey = "rpg.soundwell.signal-trial.player-caught-by-beast";
        public const string RescueKey = "rpg.soundwell.signal-trial.surveyor-rescued";
        public const string FalseMarkerClearedKey = "rpg.soundwell.signal-trial.false-route-corrected";
        public const string SafeRouteMarkedKey = "rpg.soundwell.signal-trial.safe-route-marked";
        public const string RopeObservedKey = "rpg.soundwell.signal-trial.rope-observed";
        public const string CartObservedKey = "rpg.soundwell.signal-trial.cart-observed";
        public const string CartKitReclaimedKey = "rpg.soundwell.signal-trial.cart-kit-reclaimed";
        public const string CartClueSharedKey = "rpg.soundwell.signal-trial.cart-clue-shared";
        public const string RopeRecoveredKey = "rpg.soundwell.signal-trial.rope-recovered";
        public const string EscortLineKey = "rpg.soundwell.signal-trial.surveyor-guide-line";
        public const string ClawsObservedKey = "rpg.soundwell.signal-trial.claws-observed";
        public const string RescueApproachKey = "rpg.soundwell.signal-trial.rescue-approach";
        public const string DirectCombatStartedKey = "rpg.soundwell.signal-trial.direct-combat-started";
        public const string DirectCombatCompleteKey = "rpg.soundwell.signal-trial.direct-combat-complete";
        public const string EntryOutcomeKey = "rpg.soundwell.signal-trial.last-return-outcome";
        public const string EntryOutcomeRecordedKey = "rpg.soundwell.signal-trial.last-return-recorded";
        public const string EntryCartKitKey = "rpg.soundwell.signal-trial.last-return-cart-kit";
        public const string EntrySafeRouteKey = "rpg.soundwell.signal-trial.last-return-safe-route";
        public const string EntryBeastContactsKey = "rpg.soundwell.signal-trial.last-return-beast-contacts";
    }
}
