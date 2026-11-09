using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    // Scene-local presentation for the explicitly labelled placeholder trial.
    internal sealed class SoundwellView
    {
        private static readonly Color Ink = new Color(0.79f, 0.87f, 0.90f);
        private static readonly Color Gold = new Color(1f, 0.72f, 0.33f);
        private static readonly Color Cloth = new Color(0.45f, 0.73f, 0.83f);
        private readonly Sprite square;
        private readonly Material ringMaterial;
        private readonly SpriteRenderer bellMarker;
        private readonly LineRenderer pulse;
        private readonly AudioSource audio;
        private readonly AudioClip clearBell;
        private readonly AudioClip mutedBell;
        private readonly Text hint;
        private readonly Text beastStatus;
        private float pulseStarted = float.NegativeInfinity;
        private bool mutedPulse;

        public SoundwellView(Transform root, Camera camera, Transform player, Transform bell,
            Transform beast, Transform safe)
        {
            square = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f), Texture2D.whiteTexture.width);
            ringMaterial = new Material(Shader.Find("Sprites/Default"));
            camera.backgroundColor = new Color(0.055f, 0.086f, 0.115f);
            camera.orthographicSize = 5.5f;

            Plate(root, "Trial floor", Vector2.zero, new Vector2(14f, 6.4f), new Color(0.11f, 0.19f, 0.23f), -4);
            Plate(root, "Beast lane", new Vector2(3.7f, 0f), new Vector2(6.55f, 6.4f), new Color(0.22f, 0.16f, 0.18f), -3);
            Plate(safe, "Retreat mark", Vector2.zero, new Vector2(1.75f, 1.75f), new Color(0.16f, 0.38f, 0.36f), -1);
            Wall(root, "Safety grating", new Vector2(0.25f, 0f), new Vector2(0.32f, 6.4f));
            Wall(root, "West wall", new Vector2(-7f, 0f), new Vector2(0.32f, 6.4f));
            Wall(root, "North wall", new Vector2(-3.4f, 3.2f), new Vector2(7.3f, 0.28f));
            Wall(root, "South wall", new Vector2(-3.4f, -3.2f), new Vector2(7.3f, 0.28f));

            Plate(player, "PLACEHOLDER player silhouette", Vector2.zero, new Vector2(0.64f, 0.64f), new Color(0.43f, 0.91f, 0.72f), 3);
            Plate(player, "Player face", new Vector2(0f, 0.12f), new Vector2(0.26f, 0.16f), Color.white, 4);
            bellMarker = Plate(bell, "PLACEHOLDER copper bell", Vector2.zero, new Vector2(0.57f, 0.57f), Gold, 2);
            bellMarker.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Plate(beast, "PLACEHOLDER beast", Vector2.zero, new Vector2(1.03f, 0.78f), new Color(0.95f, 0.41f, 0.42f), 3);
            Plate(beast, "Beast muzzle", new Vector2(0.56f, 0f), new Vector2(0.35f, 0.3f), new Color(1f, 0.67f, 0.49f), 4);

            var pulseObject = new GameObject("Bell pressure ripple");
            pulseObject.transform.SetParent(bell, false);
            pulse = pulseObject.AddComponent<LineRenderer>();
            pulse.sharedMaterial = ringMaterial;
            pulse.useWorldSpace = false;
            pulse.loop = true;
            pulse.positionCount = 40;
            pulse.sortingOrder = 1;
            pulse.widthMultiplier = 0.06f;

            audio = bell.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            clearBell = BellClip("Copper bell", 640f, 0.75f, 0.22f);
            mutedBell = BellClip("Wrapped bell", 230f, 0.20f, 0.10f);

            var canvas = new GameObject("PLACEHOLDER trial UI", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(root, false);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.sortingOrder = 12;
            var rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(840, 480);
            rect.localScale = Vector3.one * 0.02f;
            AddLabel(rect, "SOUNDWELL  /  SIGNAL TRIAL", 25, Ink, new Vector2(0, 224), new Vector2(700, 40));
            AddLabel(rect, "PLACEHOLDER   /   Entrance and observation lane", 14, Cloth,
                new Vector2(0, 194), new Vector2(650, 22));
            AddLabel(rect, "RETREAT", 13, Cloth, new Vector2(-278, -68), new Vector2(120, 24));
            AddLabel(rect, "COPPER BELL", 13, Gold, new Vector2(-88, -43), new Vector2(120, 22));
            beastStatus = AddLabel(rect, "", 17, Ink, new Vector2(170, -100), new Vector2(290, 30));
            hint = AddLabel(rect, "", 18, Ink, new Vector2(0, -201), new Vector2(760, 36));
        }

        public void Refresh(bool ringing, bool searching, bool wrapped, bool atBell, bool atSafePoint)
        {
            bellMarker.color = wrapped ? Cloth : Gold;
            string status = ringing ? "BEAST  /  TRACKING" : searching ? "BEAST  /  SEARCHING" : "BEAST  /  LISTENING";
            if (beastStatus.text != status)
                beastStatus.text = status;
            string controls = atBell ? (wrapped ? "[E] Muffled bell     [Q] Unwrap" : "[E] Ring     [Q] Wrap with wet cloth")
                : atSafePoint ? "[WASD / Arrows] Move     [R] Reset trial" : "[WASD / Arrows] Move";
            if (hint.text != controls)
                hint.text = controls;
            float age = Time.time - pulseStarted;
            pulse.enabled = ringing || age < (mutedPulse ? 0.33f : 0.75f);
            if (!pulse.enabled)
                return;
            float progress = ringing ? Mathf.Repeat(age * 1.65f, 1f) : Mathf.Clamp01(age / 0.33f);
            float radius = Mathf.Lerp(0.32f, mutedPulse ? 0.78f : 2.15f, progress);
            Color color = mutedPulse ? Cloth : Gold;
            color.a = 0.95f - progress * 0.68f;
            pulse.startColor = pulse.endColor = color;
            for (int i = 0; i < pulse.positionCount; i++)
            {
                float angle = i * (2f * Mathf.PI / pulse.positionCount);
                pulse.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
            }
        }

        public void PlayBell(bool muffled)
        {
            mutedPulse = muffled;
            pulseStarted = Time.time;
            audio.Stop();
            audio.PlayOneShot(muffled ? mutedBell : clearBell);
        }

        public void ResetPulse()
        {
            pulseStarted = float.NegativeInfinity;
            pulse.enabled = false;
            audio.Stop();
        }

        public void Dispose()
        {
            Object.Destroy(square);
            Object.Destroy(ringMaterial);
            Object.Destroy(clearBell);
            Object.Destroy(mutedBell);
        }

        internal static Text AddLabel(Transform parent, string content, int fontSize, Color color,
            Vector2 position, Vector2 size)
        {
            var label = new GameObject("Trial label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text))
                .GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = size;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.raycastTarget = false;
            label.text = content;
            return label;
        }

        private SpriteRenderer Plate(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.sortingOrder = order;
            renderer.color = color;
            return renderer;
        }

        private void Wall(Transform root, string name, Vector2 position, Vector2 size)
        {
            var wall = Plate(root, name, position, size, new Color(0.32f, 0.43f, 0.48f), 0);
            wall.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
        }

        private static AudioClip BellClip(string name, float frequency, float duration, float volume)
        {
            const int rate = 22050;
            var samples = new float[Mathf.RoundToInt(rate * duration)];
            for (int i = 0; i < samples.Length; i++)
            {
                float time = (float)i / rate;
                float phase = time * frequency * Mathf.PI * 2f;
                samples[i] = (Mathf.Sin(phase) + 0.38f * Mathf.Sin(phase * 2.73f))
                    * Mathf.Min(time * 1600f, 1f) * Mathf.Exp(-5f * time / duration) * volume;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
