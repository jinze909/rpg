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



        internal static Sprite Create16Colored(
            string[] rows, string name, Color32 coat, out Texture2D texture)
        {
            const int size = 16;
            texture = null;
            // Reject all malformed rows before allocating a native texture.
            if (rows == null || rows.Length != size)
                throw new System.InvalidOperationException("S00 pixel actor requires 16 rows.");
            for (int i = 0; i < size; i++)
                if (rows[i] == null || rows[i].Length != size)
                    throw new System.InvalidOperationException(
                        "S00 pixel actor row must be exactly 16 pixels.");

            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name + " texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    switch (rows[size - 1 - y][x])
                    {
                        case 'k': pixels[y * size + x] = new Color32(24, 32, 41, 255); break;
                        case 'h': pixels[y * size + x] = new Color32(82, 51, 43, 255); break;
                        case 'f': pixels[y * size + x] = new Color32(226, 180, 136, 255); break;
                        case 'c': pixels[y * size + x] = coat; break;
                        case 'g': pixels[y * size + x] = new Color32(195, 181, 151, 255); break;
                        case 'b': pixels[y * size + x] = new Color32(67, 70, 75, 255); break;
                        case 'w': pixels[y * size + x] = new Color32(181, 202, 207, 255); break;
                        default: pixels[y * size + x] = new Color32(0, 0, 0, 0); break;
                    }
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
            sprite.name = name;
            return sprite;
        }

    }
}
