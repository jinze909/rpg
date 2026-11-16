using UnityEngine;

namespace Rpg.Soundwell
{
    // Shared source for the trial's temporary 16 PPU pixel sprite poses.
    // This has no combat, localization or input authority.
    internal static class SoundwellPixelSpriteBuilder
    {
        internal static Sprite Create16(
            string[] rows, string name, out Texture2D texture)
        {
            const int size = 16;
            if (rows.Length != size)
                throw new System.InvalidOperationException("Pixel rescuer requires 16 rows.");

            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                if (rows[size - 1 - y].Length != size)
                    throw new System.InvalidOperationException("Pixel rescuer row must be 16 pixels.");
                for (int x = 0; x < size; x++)
                {
                    char part = rows[size - 1 - y][x];
                    pixels[y * size + x] = part == '#'
                        ? new Color32(255, 255, 255, 255)
                        : part == '+'
                            ? new Color32(176, 176, 176, 255)
                            : new Color32(0, 0, 0, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture,
                new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                16f, 0, SpriteMeshType.FullRect);
            // Texture.name does not propagate to Sprite.Create. Keep each
            // visible pose identifiable for the guard-action regression and
            // for artifact inspection without changing ORK battle state.
            sprite.name = name;
            return sprite;
        }



    }
}
