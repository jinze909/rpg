using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Pure-presentation pixel-art sprite resolver and UGUI icon helper backed
    /// by RpgPixelArtManifest for M02, M03, M04, and M07 story views. Owns no
    /// ORK 3 or Makinom state, never mutates texture import settings, and hides
    /// Image components whenever a sprite is null to prevent white-box fallbacks.
    /// </summary>
    public static class RpgStoryArtResolver
    {
        private static RpgPixelArtManifest ResolveManifest(RpgPixelArtManifest manifest)
        {
            return manifest != null ? manifest : RpgPixelArtManifest.LoadDefault();
        }

        // --- M02 Bridge Evidence & Harbor Desk ---

        public static Sprite ResolveM02EvidenceSprite(M02BridgeEvidence clue,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null || clue == 0) return null;
            if ((clue & M02BridgeEvidence.CleanCableCut) != 0)
                return m.M02CableFlatUncharredCut;
            if ((clue & M02BridgeEvidence.WarReportDraft) != 0)
                return m.M02OverwrittenBattleReport;
            if (clue.ToString().IndexOf("Ledger", StringComparison.OrdinalIgnoreCase) >= 0)
                return m.M02BridgeKeeperOpenLedger;
            return null;
        }

        public static Sprite ResolveM02LedgerSprite(RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            return m != null ? m.M02BridgeKeeperOpenLedger : null;
        }

        public static Sprite ResolveM02DocumentSprite(bool sealedDocument,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            return sealedDocument ? m.M02IdentityDocumentSealed : m.M02IdentityDocumentUnsealed;
        }

        public static Sprite ResolveM02OfficialSeal(RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            return m != null ? m.M02HarborOfficialSeal : null;
        }

        // --- M03 Lu Jun Camp ---

        public static Sprite ResolveM03LuJunSprite(bool rollCallPose,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            return rollCallPose ? m.M03LuJunRollCall : m.M03LuJunStanding;
        }

        public static Sprite ResolveM03VeteranSprite(int index,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            switch (index)
            {
                case 0: return m.M03VeteranNetMender;
                case 1: return m.M03VeteranForwardLookout;
                case 2: return m.M03VeteranTidalGuide;
                case 3: return m.M03VeteranStandardBearer;
                default: return null;
            }
        }

        public static Sprite ResolveM03StandardSprite(bool raised, int frameIndex,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            if (!raised) return m.M03OldStandardRolled;
            return (frameIndex & 1) == 0 ? m.M03OldStandardRaisedA : m.M03OldStandardRaisedB;
        }

        public static Sprite ResolveM03NetRackSprite(bool loaded,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            return loaded ? m.M03NetDryingRackLoaded : m.M03NetDryingRackEmpty;
        }

        public static Sprite ResolveM03TidalChartSprite(bool open,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            return open ? m.M03TidalChartOpen : m.M03TidalChartFolded;
        }

        public static Sprite ResolveM03CampCoalsSprite(RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            return m != null ? m.M03CampCoals : null;
        }

        // --- M04 Fog Crossing Overlays ---

        public static Sprite ResolveM04LanternSprite(bool lit,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            return lit ? m.FogSignalLanternOn : m.FogSignalLanternOff;
        }

        public static Sprite ResolveM04FogLayerSprite(bool dense, int frameIndex,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            if (dense) return m.FogBankDense;
            return (frameIndex & 1) == 0 ? m.FogBankLowA : m.FogBankLowB;
        }

        public static Sprite ResolveM04ReefSprite(bool exposed, bool withWash,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            if (withWash) return m.FogReefWash;
            return exposed ? m.FogReefHintExposed : m.FogReefHintSubmerged;
        }

        public static Sprite ResolveM04WakeSprite(bool bow, int frameIndex,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            bool even = (frameIndex & 1) == 0;
            if (bow) return even ? m.FogBowWakeA : m.FogBowWakeB;
            return even ? m.FogSternWakeA : m.FogSternWakeB;
        }

        // --- M07 Red Salt Evacuation Props & Structures ---

        public static Sprite ResolveM07HiddenLockSprite(bool open,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            return open ? m.M07OulinHiddenLockOpen : m.M07OulinHiddenLockClosed;
        }

        public static Sprite ResolveM07MainPumpLeverSprite(bool cutoff,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            return cutoff ? m.M07MainPumpLeverCutoff : m.M07MainPumpLeverRunning;
        }

        public static Sprite ResolveM07MusterMarkSprite(bool chalk,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            return chalk ? m.M07UpperMusterChalk : m.M07UpperMusterSign;
        }

        public static Sprite ResolveM07CargoCrateSprite(bool open,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            return open ? m.M07CargoCoverCrateOpen : m.M07CargoCoverCrateClosed;
        }

        public static Sprite ResolveM07PassSprite(bool stamped,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            return stamped ? m.M07InspectionStampedPass : m.M07InspectionPendingPass;
        }

        public static Sprite ResolveM07BerthTagSprite(bool boarded,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            return boarded ? m.M07BerthTagBoarded : m.M07BerthTagAvailable;
        }

        public static Sprite ResolveM07PumpSprite(bool working,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            return working ? m.M07BackupHandPumpWorking : m.M07BackupHandPumpIdle;
        }

        public static Sprite ResolveM07SluiceSprite(bool open,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            return open ? m.M07SluiceReleasedOpen : m.M07SluiceLockedShut;
        }

        public static Sprite ResolveM07GangwaySprite(bool lowered,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            return lowered ? m.M07BoardingGangwayLowered : m.M07BoardingGangwayRaised;
        }

        public static Sprite ResolveM07BarrierSprite(bool open,
            RpgPixelArtManifest manifest = null)
        {
            RpgPixelArtManifest m = ResolveManifest(manifest);
            if (m == null) return null;
            return open ? m.M07InspectionBarrierOpen : m.M07InspectionBarrierClosed;
        }

        // --- UGUI Pixel Icon Mounting (Idempotent, Non-Blocking, Null-Safe) ---

        public static Image CreateOrUpdatePixelIcon(RectTransform parent, string name,
            Vector2 anchoredPosition, Vector2 size, Sprite sprite)
        {
            if (parent == null) return null;
            string safeName = string.IsNullOrWhiteSpace(name) ? "PixelIcon" : name;

            Transform existing = parent.Find(safeName);
            GameObject iconObj = existing != null
                ? existing.gameObject
                : new GameObject(safeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

            if (existing == null)
                iconObj.transform.SetParent(parent, false);

            var rect = iconObj.GetComponent<RectTransform>();
            if (rect == null)
                rect = iconObj.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(
                Mathf.Round(anchoredPosition.x),
                Mathf.Round(anchoredPosition.y));
            rect.sizeDelta = new Vector2(
                Mathf.Max(0f, Mathf.Round(size.x)),
                Mathf.Max(0f, Mathf.Round(size.y)));

            var image = iconObj.GetComponent<Image>();
            if (image == null)
                image = iconObj.AddComponent<Image>();

            ApplyPixelSprite(image, sprite);
            return image;
        }

        public static void ApplyPixelSprite(Image target, Sprite sprite)
        {
            if (target == null) return;
            target.preserveAspect = true;
            target.raycastTarget = false;
            target.sprite = sprite;
            bool visible = sprite != null;
            target.enabled = visible;
            target.gameObject.SetActive(visible);
        }
    }
}
