using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Runtime Resources-loaded manifest referencing the authored pixel-art
    /// sprite slices under Assets/Game/Art/ by direct GUID/fileID without
    /// relocating source textures. Gives all three 4x RPG maps and character
    /// hosts zero-setup access to real hand-drawn 16-PPU pixel sprites.
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

        public static RpgPixelArtManifest LoadDefault()
        {
            if (cachedDefault == null)
                cachedDefault = Resources.Load<RpgPixelArtManifest>(DefaultResourcePath);
            return cachedDefault;
        }
    }
}
