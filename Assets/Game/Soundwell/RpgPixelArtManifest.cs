using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Runtime Resources-loaded manifest referencing the authored pixel-art
    /// sprite slices under Assets/Game/Art/ by direct GUID/fileID without
    /// relocating source textures. Gives all three 4x RPG maps, character
    /// hosts, and combat VFX directors zero-setup access to real hand-drawn
    /// 16-PPU pixel sprites.
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

        public static RpgPixelArtManifest LoadDefault()
        {
            if (cachedDefault == null)
                cachedDefault = Resources.Load<RpgPixelArtManifest>(DefaultResourcePath);
            return cachedDefault;
        }
    }
}
