using System;

namespace Rpg.Soundwell
{
    public enum FogBearing
    {
        Port,
        Ahead,
        Starboard
    }

    public enum FogCueKind
    {
        ClearWater,
        ReefSurf,
        FogBell,
        SignalResponse,
        FalseEcho
    }

    public enum FogHelmAction
    {
        HoldCourse,
        SlowAndSound,
        TurnPort,
        TurnStarboard,
        AnswerSignal
    }

    public enum FogResolution
    {
        SafePassage,
        HazardAvoided,
        SignalMatched,
        HullScraped,
        LostBearing
    }

    /// <summary>
    /// One restricted auditory or visual fog cue during a sea leg.
    /// Clarity starts partial so listening requires deliberate focus rather
    /// than acting as omniscient sonar.
    /// </summary>
    public readonly struct FogListeningCue
    {
        public FogListeningCue(
            FogBearing bearing,
            FogCueKind kind,
            int clarity,
            bool requiresReceiverLamp = false)
        {
            if (!Enum.IsDefined(typeof(FogBearing), bearing))
                throw new ArgumentOutOfRangeException(nameof(bearing));
            if (!Enum.IsDefined(typeof(FogCueKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (clarity < 0 || clarity > 100)
                throw new ArgumentOutOfRangeException(nameof(clarity));

            Bearing = bearing;
            Kind = kind;
            Clarity = clarity;
            RequiresReceiverLamp = requiresReceiverLamp;
        }

        public FogBearing Bearing { get; }
        public FogCueKind Kind { get; }
        public int Clarity { get; }
        public bool RequiresReceiverLamp { get; }
    }

    /// <summary>
    /// Factual in-memory state machine for restricted fog-listening sailing.
    /// Tracks bounded listen focus, hull integrity, hazard avoidance, and
    /// signal matching without creating a parallel save authority or inventing
    /// unauthored story branches.
    /// </summary>
    public sealed class FogListeningSession
    {
        public const int FocusClarityBonus = 35;

        private readonly FogListeningCue[] cues;
        private readonly int maxListenFocus;

        public FogListeningSession(
            FogListeningCue[] cues,
            int maxListenFocus,
            int maxHullIntegrity)
        {
            if (cues == null || cues.Length == 0)
                throw new ArgumentException("At least one fog cue is required.", nameof(cues));
            if (maxListenFocus <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxListenFocus));
            if (maxHullIntegrity <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHullIntegrity));

            this.cues = new FogListeningCue[cues.Length];
            Array.Copy(cues, this.cues, cues.Length);
            this.maxListenFocus = maxListenFocus;
            RemainingFocus = maxListenFocus;
            HullIntegrity = maxHullIntegrity;
        }

        public int CurrentStep { get; private set; }
        public int TotalSteps => cues.Length;
        public int MaxListenFocus => maxListenFocus;
        public int RemainingFocus { get; private set; }
        public int HullIntegrity { get; private set; }
        public int MatchedSignals { get; private set; }
        public int AvoidedHazards { get; private set; }
        public bool CurrentCueFocused { get; private set; }
        public bool IsComplete => CurrentStep >= cues.Length || HullIntegrity <= 0;
        public bool Succeeded => CurrentStep >= cues.Length && HullIntegrity > 0;

        public FogListeningCue CurrentCue
        {
            get
            {
                if (IsComplete)
                    throw new InvalidOperationException("Fog-listening session is already complete.");
                return cues[CurrentStep];
            }
        }

        public int EffectiveClarity()
        {
            FogListeningCue cue = CurrentCue;
            int boosted = cue.Clarity + (CurrentCueFocused ? FocusClarityBonus : 0);
            return boosted > 100 ? 100 : boosted;
        }

        public bool TryFocusListen()
        {
            if (IsComplete || CurrentCueFocused || RemainingFocus <= 0)
                return false;
            RemainingFocus--;
            CurrentCueFocused = true;
            return true;
        }

        public void RecoverFocus(int amount)
        {
            if (IsComplete)
                throw new InvalidOperationException("Cannot recover focus after the session ends.");
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            // Saturate before adding: large recovery requests must never
            // overflow a valid focus budget into a negative value.
            int needed = maxListenFocus - RemainingFocus;
            RemainingFocus = amount >= needed ? maxListenFocus : RemainingFocus + amount;
        }

        public FogResolution ApplyHelmAction(FogHelmAction action)
        {
            if (IsComplete)
                throw new InvalidOperationException("Fog-listening session is already complete.");
            if (!Enum.IsDefined(typeof(FogHelmAction), action))
                throw new ArgumentOutOfRangeException(nameof(action));

            FogListeningCue cue = cues[CurrentStep];
            FogResolution resolution = ResolveCue(cue, action);

            if (resolution == FogResolution.HullScraped)
                HullIntegrity = Math.Max(0, HullIntegrity - 1);
            else if (resolution == FogResolution.HazardAvoided)
                AvoidedHazards++;
            else if (resolution == FogResolution.SignalMatched)
                MatchedSignals++;

            // A lost bearing is not passage through this stretch of fog.
            // Keep the same clue visible so the player can reassess or use
            // SlowAndSound; consuming the cue would reward blind button spam.
            if (resolution == FogResolution.LostBearing)
            {
                CurrentCueFocused = false;
                return resolution;
            }

            CurrentStep++;
            CurrentCueFocused = false;
            return resolution;
        }

        private static FogResolution ResolveCue(FogListeningCue cue, FogHelmAction action)
        {
            switch (cue.Kind)
            {
                case FogCueKind.ReefSurf:
                    return ResolveReef(cue.Bearing, action);

                case FogCueKind.FogBell:
                case FogCueKind.SignalResponse:
                    if (action == FogHelmAction.AnswerSignal)
                        return FogResolution.SignalMatched;
                    if (action == FogHelmAction.HoldCourse || action == FogHelmAction.SlowAndSound)
                        return FogResolution.SafePassage;
                    return FogResolution.LostBearing;

                case FogCueKind.FalseEcho:
                    if (action == FogHelmAction.HoldCourse || action == FogHelmAction.SlowAndSound)
                        return FogResolution.SafePassage;
                    return FogResolution.LostBearing;

                case FogCueKind.ClearWater:
                    if (action == FogHelmAction.HoldCourse || action == FogHelmAction.SlowAndSound)
                        return FogResolution.SafePassage;
                    return FogResolution.LostBearing;

                default:
                    throw new ArgumentOutOfRangeException(nameof(cue));
            }
        }

        private static FogResolution ResolveReef(FogBearing bearing, FogHelmAction action)
        {
            switch (bearing)
            {
                case FogBearing.Ahead:
                    if (action == FogHelmAction.TurnPort ||
                        action == FogHelmAction.TurnStarboard ||
                        action == FogHelmAction.SlowAndSound)
                        return FogResolution.HazardAvoided;
                    return FogResolution.HullScraped;

                case FogBearing.Port:
                    if (action == FogHelmAction.TurnStarboard ||
                        action == FogHelmAction.SlowAndSound)
                        return FogResolution.HazardAvoided;
                    if (action == FogHelmAction.TurnPort)
                        return FogResolution.HullScraped;
                    return FogResolution.SafePassage;

                case FogBearing.Starboard:
                    if (action == FogHelmAction.TurnPort ||
                        action == FogHelmAction.SlowAndSound)
                        return FogResolution.HazardAvoided;
                    if (action == FogHelmAction.TurnStarboard)
                        return FogResolution.HullScraped;
                    return FogResolution.SafePassage;

                default:
                    throw new ArgumentOutOfRangeException(nameof(bearing));
            }
        }
    }
}
