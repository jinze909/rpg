using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Runtime Resources-loaded manifest referencing the authored pixel-art
    /// sprite slices under Assets/Game/Art/ by direct GUID/fileID without
    /// relocating source textures. Gives all three 4x RPG maps, character
    /// hosts, story prop boards, and combat VFX directors zero-setup access
    /// to real hand-drawn 16-PPU pixel sprites.
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

        public static RpgPixelArtManifest LoadDefault()
        {
            if (cachedDefault == null)
                cachedDefault = Resources.Load<RpgPixelArtManifest>(DefaultResourcePath);
            return cachedDefault;
        }
    }
}
