using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Runtime Resources-loaded manifest referencing the authored pixel-art
    /// sprite slices under Assets/Game/Art/ by direct GUID/fileID without
    /// relocating source textures. Gives all three 4x RPG maps, character
    /// hosts, story prop boards, fog-crossing overlays, M07 evacuation props,
    /// and combat VFX directors zero-setup access to real hand-drawn 16-PPU
    /// pixel sprites.
    /// </summary>
    [CreateAssetMenu(menuName = "Return/Pixel Art Manifest", fileName = "RpgPixelArtManifest")]
    public sealed class RpgPixelArtManifest : ScriptableObject
    {
        public const string DefaultResourcePath = "Soundwell/RpgPixelArtManifest";

        [Header("16-PPU Map Tilesets (indexed by SoundwellTileKind / HarborTileKind)")]
        [SerializeField] private Sprite[] soundwellTiles;
        [SerializeField] private Sprite[] harborTiles;

        [Header("Core Characters & Story Cast")]
        [SerializeField] private Sprite ferryGuideIdle;
        [SerializeField] private Sprite ferryGuideWalk1;
        [SerializeField] private Sprite ferryGuideWalk2;
        [SerializeField] private Sprite ferryGuideBrace;
        [SerializeField] private Sprite rescueRiggerIdle;
        [SerializeField] private Sprite soundBorrowingBeastIdle;
        [SerializeField] private Sprite soundBorrowingBeastAlert;
        [SerializeField] private Sprite bridgeGuardIdle;
        [SerializeField] private Sprite bridgeGuardThrust;
        [SerializeField] private Sprite shenLiBridgeIdle;
        [SerializeField] private Sprite shenLiBraceCable;
        [SerializeField] private Sprite seranLetterCarrier;
        [SerializeField] private Sprite harborRepairer;
        [SerializeField] private Sprite outerHarborFisher;
        [SerializeField] private Sprite miningFamilySaltVendor;

        [Header("S00 Whitewater Battle Actions (24x24)")]
        [SerializeField] private Sprite s00PlayerReady;
        [SerializeField] private Sprite s00PlayerDraw;
        [SerializeField] private Sprite s00PlayerSlash;
        [SerializeField] private Sprite s00PlayerRecover;
        [SerializeField] private Sprite s00PlayerRaise;
        [SerializeField] private Sprite s00PlayerHold;
        [SerializeField] private Sprite s00PlayerParry;
        [SerializeField] private Sprite s00PlayerHit;
        [SerializeField] private Sprite s00SapperReady;
        [SerializeField] private Sprite s00SapperRush;
        [SerializeField] private Sprite s00SapperCut;
        [SerializeField] private Sprite s00SapperRepelled;

        [Header("Universal Combat Feedback VFX (24x24, 3 frames each)")]
        [SerializeField] private Sprite[] parrySparkFrames;
        [SerializeField] private Sprite[] bladeRepelTrailFrames;
        [SerializeField] private Sprite[] threatLockBreakFrames;
        [SerializeField] private Sprite[] acousticPulseFrames;

        [Header("M02 Bridge Evidence Props (32x32)")]
        [SerializeField] private Sprite m02CableFlatUncharredCut;
        [SerializeField] private Sprite m02BridgeKeeperOpenLedger;
        [SerializeField] private Sprite m02OverwrittenBattleReport;
        [SerializeField] private Sprite m02HarborOfficialSeal;
        [SerializeField] private Sprite m02IdentityDocumentUnsealed;
        [SerializeField] private Sprite m02IdentityDocumentSealed;

        [Header("M03 Lu Jun Camp Cast (16x24)")]
        [SerializeField] private Sprite m03LuJunStanding;
        [SerializeField] private Sprite m03LuJunRollCall;
        [SerializeField] private Sprite m03VeteranNetMender;
        [SerializeField] private Sprite m03VeteranForwardLookout;
        [SerializeField] private Sprite m03VeteranTidalGuide;
        [SerializeField] private Sprite m03VeteranStandardBearer;

        [Header("M03 Lu Jun Camp Objects (32x32)")]
        [SerializeField] private Sprite m03OldStandardRolled;
        [SerializeField] private Sprite m03OldStandardRaisedA;
        [SerializeField] private Sprite m03OldStandardRaisedB;
        [SerializeField] private Sprite m03NetDryingRackLoaded;
        [SerializeField] private Sprite m03NetDryingRackEmpty;
        [SerializeField] private Sprite m03TidalChartOpen;
        [SerializeField] private Sprite m03TidalChartFolded;
        [SerializeField] private Sprite m03CampCoals;

        [Header("M04 Fog Crossing Overlays (32x32)")]
        [SerializeField] private Sprite fogBankLowA;
        [SerializeField] private Sprite fogBankLowB;
        [SerializeField] private Sprite fogBankDense;
        [SerializeField] private Sprite fogReefHintSubmerged;
        [SerializeField] private Sprite fogReefHintExposed;
        [SerializeField] private Sprite fogReefWash;
        [SerializeField] private Sprite fogBowWakeA;
        [SerializeField] private Sprite fogBowWakeB;
        [SerializeField] private Sprite fogSternWakeA;
        [SerializeField] private Sprite fogSternWakeB;
        [SerializeField] private Sprite fogSignalLanternOff;
        [SerializeField] private Sprite fogSignalLanternOn;

        [Header("M07 Evacuation Route Props (16x16)")]
        [SerializeField] private Sprite m07OulinHiddenLockClosed;
        [SerializeField] private Sprite m07OulinHiddenLockOpen;
        [SerializeField] private Sprite m07MainPumpLeverRunning;
        [SerializeField] private Sprite m07MainPumpLeverCutoff;
        [SerializeField] private Sprite m07UpperMusterSign;
        [SerializeField] private Sprite m07UpperMusterChalk;
        [SerializeField] private Sprite m07CargoCoverCrateClosed;
        [SerializeField] private Sprite m07CargoCoverCrateOpen;
        [SerializeField] private Sprite m07InspectionPendingPass;
        [SerializeField] private Sprite m07InspectionStampedPass;
        [SerializeField] private Sprite m07BerthTagAvailable;
        [SerializeField] private Sprite m07BerthTagBoarded;

        [Header("M07 Evacuation Structures (32x32)")]
        [SerializeField] private Sprite m07BackupHandPumpIdle;
        [SerializeField] private Sprite m07BackupHandPumpWorking;
        [SerializeField] private Sprite m07SluiceLockedShut;
        [SerializeField] private Sprite m07SluiceReleasedOpen;
        [SerializeField] private Sprite m07BoardingGangwayRaised;
        [SerializeField] private Sprite m07BoardingGangwayLowered;
        [SerializeField] private Sprite m07InspectionBarrierClosed;
        [SerializeField] private Sprite m07InspectionBarrierOpen;

        [Header("Playable Opening and Soundwell Trial Art")]
        [SerializeField] private Sprite shenLiBridgeWrench;
        [SerializeField] private Sprite shenLiTurnClamp;
        [SerializeField] private Sprite hanQiaoInWater;
        [SerializeField] private Sprite hanQiaoBeingHauled;
        [SerializeField] private Sprite hanQiaoOnShore;
        [SerializeField] private Sprite clampCableLoose;
        [SerializeField] private Sprite clampCableTaut;
        [SerializeField] private Sprite clampCableSnapped;
        [SerializeField] private Sprite spareRescueRope;
        [SerializeField] private Sprite rescueStretcher;
        [SerializeField] private Sprite sinkingFarBridge;
        [SerializeField] private Sprite bridgeGuardAdvance;
        [SerializeField] private Sprite rescueRiggerWalk1;
        [SerializeField] private Sprite ferryGuideSideIdle;
        [SerializeField] private Sprite ferryGuideSideWalk1;
        [SerializeField] private Sprite rescueRiggerSideIdle;
        [SerializeField] private Sprite rescueRiggerSideBrace;
        [SerializeField] private Sprite whitewaterRescuerReady;
        [SerializeField] private Sprite whitewaterRescuerHauling;
        [SerializeField] private Sprite whitewaterRetreatCook;
        [SerializeField] private Sprite whitewaterYoungShieldBearer;
        [SerializeField] private Sprite whitewaterBraidedLine;
        [SerializeField] private Sprite whitewaterFerryDeck;
        [SerializeField] private Sprite m00MemorialWoman;
        [SerializeField] private Sprite m00Xiaodou;
        [SerializeField] private Sprite m00XiaodouPrompted;
        [SerializeField] private Sprite m00Veteran;
        [SerializeField] private Sprite m00MemorialStele;
        [SerializeField] private Sprite m00MemorialStatue;
        [SerializeField] private Sprite m00SmallStage;
        [SerializeField] private Sprite m00SquarePaving;
        [SerializeField] private Sprite m00DayWater;
        [SerializeField] private Sprite soundwellBeastRecoil;
        [SerializeField] private Sprite soundwellSurveyorTrapped;
        [SerializeField] private Sprite soundwellSurveyorFreed;
        [SerializeField] private Sprite soundwellSurveyorSupported1;
        [SerializeField] private Sprite soundwellSurveyorSupported2;
        [SerializeField] private Sprite soundwellRope;
        [SerializeField] private Sprite soundwellCart;
        [SerializeField] private Sprite soundwellMapKit;
        [SerializeField] private Sprite soundwellClawMarks;
        [SerializeField] private Sprite soundwellFalseTrail;
        [SerializeField] private Sprite soundwellSafeTrail;
        [SerializeField] private Sprite soundwellCopperBell;
        [SerializeField] private Sprite soundwellWrappedBell;
        [SerializeField] private Sprite soundwellGateLever;
        [SerializeField] private Sprite soundwellBeastIntent;
        [SerializeField] private Sprite soundwellAttackTarget;
        [SerializeField] private Sprite soundwellActiveRescuer;
        [SerializeField] private Sprite soundwellUpperPlank;
        [SerializeField] private Sprite soundwellWeakPlank;
        [SerializeField] private Sprite soundwellBrokenPlank;

        private static RpgPixelArtManifest cachedDefault;

        public Sprite[] SoundwellTiles => soundwellTiles;
        public Sprite[] HarborTiles => harborTiles;
        public Sprite FerryGuideIdle => ferryGuideIdle;
        public Sprite FerryGuideWalk1 => ferryGuideWalk1;
        public Sprite FerryGuideWalk2 => ferryGuideWalk2;
        public Sprite FerryGuideBrace => ferryGuideBrace;
        public Sprite RescueRiggerIdle => rescueRiggerIdle;
        public Sprite SoundBorrowingBeastIdle => soundBorrowingBeastIdle;
        public Sprite SoundBorrowingBeastAlert => soundBorrowingBeastAlert;
        public Sprite BridgeGuardIdle => bridgeGuardIdle;
        public Sprite BridgeGuardThrust => bridgeGuardThrust;
        public Sprite ShenLiBridgeIdle => shenLiBridgeIdle;
        public Sprite ShenLiBraceCable => shenLiBraceCable;
        public Sprite SeranLetterCarrier => seranLetterCarrier;
        public Sprite HarborRepairer => harborRepairer;
        public Sprite OuterHarborFisher => outerHarborFisher;
        public Sprite MiningFamilySaltVendor => miningFamilySaltVendor;

        public Sprite S00PlayerReady => s00PlayerReady;
        public Sprite S00PlayerDraw => s00PlayerDraw;
        public Sprite S00PlayerSlash => s00PlayerSlash;
        public Sprite S00PlayerRecover => s00PlayerRecover;
        public Sprite S00PlayerRaise => s00PlayerRaise;
        public Sprite S00PlayerHold => s00PlayerHold;
        public Sprite S00PlayerParry => s00PlayerParry;
        public Sprite S00PlayerHit => s00PlayerHit;
        public Sprite S00SapperReady => s00SapperReady;
        public Sprite S00SapperRush => s00SapperRush;
        public Sprite S00SapperCut => s00SapperCut;
        public Sprite S00SapperRepelled => s00SapperRepelled;

        public Sprite[] ParrySparkFrames => parrySparkFrames;
        public Sprite[] BladeRepelTrailFrames => bladeRepelTrailFrames;
        public Sprite[] ThreatLockBreakFrames => threatLockBreakFrames;
        public Sprite[] AcousticPulseFrames => acousticPulseFrames;

        public Sprite M02CableFlatUncharredCut => m02CableFlatUncharredCut;
        public Sprite M02BridgeKeeperOpenLedger => m02BridgeKeeperOpenLedger;
        public Sprite M02OverwrittenBattleReport => m02OverwrittenBattleReport;
        public Sprite M02HarborOfficialSeal => m02HarborOfficialSeal;
        public Sprite M02IdentityDocumentUnsealed => m02IdentityDocumentUnsealed;
        public Sprite M02IdentityDocumentSealed => m02IdentityDocumentSealed;

        public Sprite M03LuJunStanding => m03LuJunStanding;
        public Sprite M03LuJunRollCall => m03LuJunRollCall;
        public Sprite M03VeteranNetMender => m03VeteranNetMender;
        public Sprite M03VeteranForwardLookout => m03VeteranForwardLookout;
        public Sprite M03VeteranTidalGuide => m03VeteranTidalGuide;
        public Sprite M03VeteranStandardBearer => m03VeteranStandardBearer;

        public Sprite M03OldStandardRolled => m03OldStandardRolled;
        public Sprite M03OldStandardRaisedA => m03OldStandardRaisedA;
        public Sprite M03OldStandardRaisedB => m03OldStandardRaisedB;
        public Sprite M03NetDryingRackLoaded => m03NetDryingRackLoaded;
        public Sprite M03NetDryingRackEmpty => m03NetDryingRackEmpty;
        public Sprite M03TidalChartOpen => m03TidalChartOpen;
        public Sprite M03TidalChartFolded => m03TidalChartFolded;
        public Sprite M03CampCoals => m03CampCoals;

        public Sprite FogBankLowA => fogBankLowA;
        public Sprite FogBankLowB => fogBankLowB;
        public Sprite FogBankDense => fogBankDense;
        public Sprite FogReefHintSubmerged => fogReefHintSubmerged;
        public Sprite FogReefHintExposed => fogReefHintExposed;
        public Sprite FogReefWash => fogReefWash;
        public Sprite FogBowWakeA => fogBowWakeA;
        public Sprite FogBowWakeB => fogBowWakeB;
        public Sprite FogSternWakeA => fogSternWakeA;
        public Sprite FogSternWakeB => fogSternWakeB;
        public Sprite FogSignalLanternOff => fogSignalLanternOff;
        public Sprite FogSignalLanternOn => fogSignalLanternOn;

        public Sprite M07OulinHiddenLockClosed => m07OulinHiddenLockClosed;
        public Sprite M07OulinHiddenLockOpen => m07OulinHiddenLockOpen;
        public Sprite M07MainPumpLeverRunning => m07MainPumpLeverRunning;
        public Sprite M07MainPumpLeverCutoff => m07MainPumpLeverCutoff;
        public Sprite M07UpperMusterSign => m07UpperMusterSign;
        public Sprite M07UpperMusterChalk => m07UpperMusterChalk;
        public Sprite M07CargoCoverCrateClosed => m07CargoCoverCrateClosed;
        public Sprite M07CargoCoverCrateOpen => m07CargoCoverCrateOpen;
        public Sprite M07InspectionPendingPass => m07InspectionPendingPass;
        public Sprite M07InspectionStampedPass => m07InspectionStampedPass;
        public Sprite M07BerthTagAvailable => m07BerthTagAvailable;
        public Sprite M07BerthTagBoarded => m07BerthTagBoarded;

        public Sprite M07BackupHandPumpIdle => m07BackupHandPumpIdle;
        public Sprite M07BackupHandPumpWorking => m07BackupHandPumpWorking;
        public Sprite M07SluiceLockedShut => m07SluiceLockedShut;
        public Sprite M07SluiceReleasedOpen => m07SluiceReleasedOpen;
        public Sprite M07BoardingGangwayRaised => m07BoardingGangwayRaised;
        public Sprite M07BoardingGangwayLowered => m07BoardingGangwayLowered;
        public Sprite M07InspectionBarrierClosed => m07InspectionBarrierClosed;
        public Sprite M07InspectionBarrierOpen => m07InspectionBarrierOpen;

        public Sprite ShenLiBridgeWrench => shenLiBridgeWrench;
        public Sprite ShenLiTurnClamp => shenLiTurnClamp;
        public Sprite HanQiaoInWater => hanQiaoInWater;
        public Sprite HanQiaoBeingHauled => hanQiaoBeingHauled;
        public Sprite HanQiaoOnShore => hanQiaoOnShore;
        public Sprite ClampCableLoose => clampCableLoose;
        public Sprite ClampCableTaut => clampCableTaut;
        public Sprite ClampCableSnapped => clampCableSnapped;
        public Sprite SpareRescueRope => spareRescueRope;
        public Sprite RescueStretcher => rescueStretcher;
        public Sprite SinkingFarBridge => sinkingFarBridge;
        public Sprite BridgeGuardAdvance => bridgeGuardAdvance;
        public Sprite RescueRiggerWalk1 => rescueRiggerWalk1;
        public Sprite FerryGuideSideIdle => ferryGuideSideIdle;
        public Sprite FerryGuideSideWalk1 => ferryGuideSideWalk1;
        public Sprite RescueRiggerSideIdle => rescueRiggerSideIdle;
        public Sprite RescueRiggerSideBrace => rescueRiggerSideBrace;
        public Sprite WhitewaterRescuerReady => whitewaterRescuerReady;
        public Sprite WhitewaterRescuerHauling => whitewaterRescuerHauling;
        public Sprite WhitewaterRetreatCook => whitewaterRetreatCook;
        public Sprite WhitewaterYoungShieldBearer => whitewaterYoungShieldBearer;
        public Sprite WhitewaterBraidedLine => whitewaterBraidedLine;
        public Sprite WhitewaterFerryDeck => whitewaterFerryDeck;
        public Sprite M00MemorialWoman => m00MemorialWoman;
        public Sprite M00Xiaodou => m00Xiaodou;
        public Sprite M00XiaodouPrompted => m00XiaodouPrompted;
        public Sprite M00Veteran => m00Veteran;
        public Sprite M00MemorialStele => m00MemorialStele;
        public Sprite M00MemorialStatue => m00MemorialStatue;
        public Sprite M00SmallStage => m00SmallStage;
        public Sprite M00SquarePaving => m00SquarePaving;
        public Sprite M00DayWater => m00DayWater;
        public Sprite SoundwellBeastRecoil => soundwellBeastRecoil;
        public Sprite SoundwellSurveyorTrapped => soundwellSurveyorTrapped;
        public Sprite SoundwellSurveyorFreed => soundwellSurveyorFreed;
        public Sprite SoundwellSurveyorSupported1 => soundwellSurveyorSupported1;
        public Sprite SoundwellSurveyorSupported2 => soundwellSurveyorSupported2;
        public Sprite SoundwellRope => soundwellRope;
        public Sprite SoundwellCart => soundwellCart;
        public Sprite SoundwellMapKit => soundwellMapKit;
        public Sprite SoundwellClawMarks => soundwellClawMarks;
        public Sprite SoundwellFalseTrail => soundwellFalseTrail;
        public Sprite SoundwellSafeTrail => soundwellSafeTrail;
        public Sprite SoundwellCopperBell => soundwellCopperBell;
        public Sprite SoundwellWrappedBell => soundwellWrappedBell;
        public Sprite SoundwellGateLever => soundwellGateLever;
        public Sprite SoundwellBeastIntent => soundwellBeastIntent;
        public Sprite SoundwellAttackTarget => soundwellAttackTarget;
        public Sprite SoundwellActiveRescuer => soundwellActiveRescuer;
        public Sprite SoundwellUpperPlank => soundwellUpperPlank;
        public Sprite SoundwellWeakPlank => soundwellWeakPlank;
        public Sprite SoundwellBrokenPlank => soundwellBrokenPlank;

        public static RpgPixelArtManifest LoadDefault()
        {
            if (cachedDefault == null)
                cachedDefault = Resources.Load<RpgPixelArtManifest>(DefaultResourcePath);
            return cachedDefault;
        }
    }
}
