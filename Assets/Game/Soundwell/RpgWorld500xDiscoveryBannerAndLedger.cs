using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public readonly struct RpgWorld500xMapProgress
    {
        public RpgWorld500xMapProgress(
            RpgWorldMapId mapId,
            string displayNameZh,
            int discoveredMarkers,
            int totalMarkers,
            int completedQuests,
            int totalQuests,
            int unlockedBeacons,
            int totalBeacons,
            int clearedDangerZones,
            int totalDangerZones)
        {
            MapId = mapId;
            DisplayNameZh = displayNameZh;
            DiscoveredMarkers = discoveredMarkers;
            TotalMarkers = totalMarkers;
            CompletedQuests = completedQuests;
            TotalQuests = totalQuests;
            UnlockedBeacons = unlockedBeacons;
            TotalBeacons = totalBeacons;
            ClearedDangerZones = clearedDangerZones;
            TotalDangerZones = totalDangerZones;
        }

        public RpgWorldMapId MapId { get; }
        public string DisplayNameZh { get; }
        public int DiscoveredMarkers { get; }
        public int TotalMarkers { get; }
        public int CompletedQuests { get; }
        public int TotalQuests { get; }
        public int UnlockedBeacons { get; }
        public int TotalBeacons { get; }
        public int ClearedDangerZones { get; }
        public int TotalDangerZones { get; }
    }

    public readonly struct RpgWorld500xGlobalProgress
    {
        public RpgWorld500xGlobalProgress(
            IReadOnlyList<RpgWorld500xMapProgress> perMap,
            int discoveredMarkers,
            int totalMarkers,
            int completedQuests,
            int totalQuests,
            int unlockedBeacons,
            int totalBeacons,
            int clearedDangerZones,
            int totalDangerZones)
        {
            PerMap = perMap;
            DiscoveredMarkers = discoveredMarkers;
            TotalMarkers = totalMarkers;
            CompletedQuests = completedQuests;
            TotalQuests = totalQuests;
            UnlockedBeacons = unlockedBeacons;
            TotalBeacons = totalBeacons;
            ClearedDangerZones = clearedDangerZones;
            TotalDangerZones = totalDangerZones;
        }

        public IReadOnlyList<RpgWorld500xMapProgress> PerMap { get; }
        public int DiscoveredMarkers { get; }
        public int TotalMarkers { get; }
        public int CompletedQuests { get; }
        public int TotalQuests { get; }
        public int UnlockedBeacons { get; }
        public int TotalBeacons { get; }
        public int ClearedDangerZones { get; }
        public int TotalDangerZones { get; }

        public float CompletionPercent
        {
            get
            {
                int denom = TotalMarkers + TotalQuests;
                if (denom <= 0) return 0f;
                return Mathf.Clamp((DiscoveredMarkers + CompletedQuests) * 100f / denom, 0f, 100f);
            }
        }
    }

    /// <summary>
    /// Aggregates 500x-scale exploration progress across all four maps directly
    /// from Makinom variable readers for ReturnStoryLedger and UI summaries.
    /// </summary>
    public static class RpgWorld500xLedgerSummary
    {
        public static RpgWorld500xGlobalProgress Capture(Func<string, int> getInt)
        {
            Func<string, int> safeRead = getInt ?? (_ => 0);
            var perMap = new List<RpgWorld500xMapProgress>(RpgExpandedWorldScale.AllSpecs.Count);

            int sumDisc = 0, sumTotMarkers = 0;
            int sumDoneQ = 0, sumTotQ = 0;
            int sumBeacons = 0, sumTotBeacons = 0;
            int sumCleared = 0, sumTotDanger = 0;

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(spec.MapId);
                IReadOnlyList<RpgLandmarkQuestDefinition> quests = RpgWorldLandmarkCatalog.GetQuests(spec.MapId);

                int disc = 0, unlockedBeacons = 0, totalBeacons = 0, clearedDanger = 0, totalDanger = 0;
                for (int i = 0; i < markers.Count; i++)
                {
                    RpgMapLandmarkMarker m = markers[i];
                    bool isDisc = safeRead(RpgMapExplorationTracker.MarkerDiscoveryKey(m.Id)) == 1;
                    if (isDisc) disc++;
                    if (m.SupportsFastTravel)
                    {
                        totalBeacons++;
                        if (isDisc) unlockedBeacons++;
                    }
                    if (m.Category == RpgMapMarkerCategory.DangerZone)
                    {
                        totalDanger++;
                        if (safeRead(RpgWorldOrkExplorationBridge.DangerClearedKey(m.Id)) == 1)
                            clearedDanger++;
                    }
                }

                int doneQ = 0;
                for (int i = 0; i < quests.Count; i++)
                {
                    RpgLandmarkQuestDefinition q = quests[i];
                    if (safeRead(RpgMapExplorationTracker.QuestProgressKey(q.QuestId)) >= q.RequiredSteps)
                        doneQ++;
                }

                perMap.Add(new RpgWorld500xMapProgress(
                    spec.MapId, spec.DisplayNameZh,
                    disc, markers.Count, doneQ, quests.Count,
                    unlockedBeacons, totalBeacons, clearedDanger, totalDanger));

                sumDisc += disc;
                sumTotMarkers += markers.Count;
                sumDoneQ += doneQ;
                sumTotQ += quests.Count;
                sumBeacons += unlockedBeacons;
                sumTotBeacons += totalBeacons;
                sumCleared += clearedDanger;
                sumTotDanger += totalDanger;
            }

            return new RpgWorld500xGlobalProgress(
                perMap, sumDisc, sumTotMarkers, sumDoneQ, sumTotQ,
                sumBeacons, sumTotBeacons, sumCleared, sumTotDanger);
        }

        public static string FormatLedgerSummaryZh(RpgWorld500xGlobalProgress progress)
        {
            return "大世界探索总进度 " + Mathf.RoundToInt(progress.CompletionPercent) +
                "% | 地标 " + progress.DiscoveredMarkers + "/" + progress.TotalMarkers +
                " | 地标任务 " + progress.CompletedQuests + "/" + progress.TotalQuests +
                " | 传送灯标 " + progress.UnlockedBeacons + "/" + progress.TotalBeacons +
                " | 高危清障 " + progress.ClearedDangerZones + "/" + progress.TotalDangerZones;
        }
    }

    /// <summary>
    /// Queued retro RPG toast banner HUD announcing landmark discoveries,
    /// unlocked fast-travel beacons, completed landmark quests, and cleared
    /// danger zones during 500x world exploration.
    /// </summary>
    public sealed class RpgWorld500xDiscoveryBannerHud : IDisposable
    {
        public const float DefaultDisplaySeconds = 3.2f;

        private readonly struct BannerEntry
        {
            public BannerEntry(string headline, string subtitle, float durationSeconds)
            {
                Headline = headline;
                Subtitle = subtitle;
                DurationSeconds = durationSeconds;
            }

            public string Headline { get; }
            public string Subtitle { get; }
            public float DurationSeconds { get; }
        }

        private readonly GameObject rootObject;
        private readonly GameObject bannerPanel;
        private readonly Text headlineText;
        private readonly Text subtitleText;
        private readonly Queue<BannerEntry> queue = new Queue<BannerEntry>();
        private float remainingSeconds;
        private bool showing;
        private bool disposed;

        public GameObject RootObject => rootObject;
        public bool IsShowing => !IsDisposed && showing;
        public int QueuedCount => queue.Count;
        public string CurrentHeadline => headlineText != null ? headlineText.text : string.Empty;
        public string CurrentSubtitle => subtitleText != null ? subtitleText.text : string.Empty;
        public bool IsDisposed => disposed || rootObject == null;

        public RpgWorld500xDiscoveryBannerHud(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            rootObject = new GameObject("RpgWorld500xDiscoveryBannerHud",
                typeof(RectTransform), typeof(Canvas));
            rootObject.transform.SetParent(parent, false);
            try
            {
                var canvas = rootObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 60;

                bannerPanel = new GameObject("BannerPanel", typeof(RectTransform), typeof(Image));
                bannerPanel.transform.SetParent(rootObject.transform, false);
                var rect = bannerPanel.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -20f);
                rect.sizeDelta = new Vector2(460f, 58f);
                bannerPanel.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.14f, 0.92f);

                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                var hObj = new GameObject("Headline", typeof(RectTransform), typeof(Text));
                hObj.transform.SetParent(bannerPanel.transform, false);
                headlineText = hObj.GetComponent<Text>();
                headlineText.font = font;
                headlineText.fontSize = 15;
                headlineText.alignment = TextAnchor.MiddleCenter;
                headlineText.color = new Color(1.0f, 0.88f, 0.54f, 1f);

                var sObj = new GameObject("Subtitle", typeof(RectTransform), typeof(Text));
                sObj.transform.SetParent(bannerPanel.transform, false);
                subtitleText = sObj.GetComponent<Text>();
                subtitleText.font = font;
                subtitleText.fontSize = 12;
                subtitleText.alignment = TextAnchor.MiddleCenter;
                subtitleText.color = new Color(0.86f, 0.90f, 0.95f, 1f);

                bannerPanel.SetActive(false);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool EnqueueLandmarkDiscovered(RpgMapLandmarkMarker marker)
        {
            string sub = marker.SupportsFastTravel
                ? "已解锁快速传送灯标 · 坐标 (" + marker.Tile.x + "," + marker.Tile.y + ")"
                : "已标记至大地图 · 坐标 (" + marker.Tile.x + "," + marker.Tile.y + ")";
            return Enqueue("发现新地标 · " + marker.TitleZh, sub, DefaultDisplaySeconds);
        }

        public bool EnqueueQuestCompleted(RpgLandmarkQuestRewardResult result)
        {
            return Enqueue("地标任务完成 · " + result.TitleZh,
                result.RewardSummaryZh, DefaultDisplaySeconds);
        }

        public bool Enqueue(string headline, string subtitle, float durationSeconds = DefaultDisplaySeconds)
        {
            if (IsDisposed || string.IsNullOrEmpty(headline) ||
                float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) ||
                durationSeconds <= 0f)
                return false;

            var entry = new BannerEntry(headline, subtitle ?? string.Empty, durationSeconds);
            if (!showing)
                Present(entry);
            else
                queue.Enqueue(entry);
            return true;
        }

        public void Step(float deltaSeconds)
        {
            if (IsDisposed || !showing ||
                float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ||
                deltaSeconds <= 0f)
                return;

            remainingSeconds -= deltaSeconds;
            if (remainingSeconds > 0f)
                return;

            if (queue.Count > 0)
            {
                Present(queue.Dequeue());
            }
            else
            {
                showing = false;
                remainingSeconds = 0f;
                bannerPanel.SetActive(false);
            }
        }

        private void Present(BannerEntry entry)
        {
            showing = true;
            remainingSeconds = entry.DurationSeconds;
            headlineText.text = entry.Headline;
            subtitleText.text = entry.Subtitle;
            bannerPanel.SetActive(true);
        }

        private static void DestroySafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            showing = false;
            queue.Clear();
            DestroySafe(rootObject);
        }
    }
}
