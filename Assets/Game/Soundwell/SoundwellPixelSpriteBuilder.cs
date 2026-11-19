using UnityEngine;

namespace Rpg.Soundwell
{
    // Shared source for the trial's temporary 16 PPU pixel sprite poses.
    // This has no combat, localization or input authority.
    internal static class SoundwellPixelSpriteBuilder
    {
        private const int Size = 16;

        internal static Sprite Create16(
            string[] rows, string name, out Texture2D texture)
        {
            return Create16Internal(
                rows,
                name,
                name,
                "Pixel rescuer requires 16 rows.",
                "Pixel rescuer row must be 16 pixels.",
                setAnisoZero: true,
                useFullRectMesh: true,
                part => part == '#'
                    ? new Color32(255, 255, 255, 255)
                    : part == '+'
                        ? new Color32(176, 176, 176, 255)
                        : new Color32(0, 0, 0, 0),
                out texture);
        }

        internal static Sprite Create16Colored(
            string[] rows, string name, Color32 coat, out Texture2D texture)
        {
            return Create16Internal(
                rows,
                name,
                name + " texture",
                "S00 pixel actor requires 16 rows.",
                "S00 pixel actor row must be 16 pixels.",
                setAnisoZero: false,
                useFullRectMesh: false,
                part => MapColoredPixel(part, coat),
                out texture);
        }

        private static Sprite Create16Internal(
            string[] rows,
            string spriteName,
            string textureName,
            string invalidRowsMessage,
            string invalidRowLengthMessage,
            bool setAnisoZero,
            bool useFullRectMesh,
            System.Func<char, Color32> colorMapper,
            out Texture2D texture)
        {
            texture = null;
            if (rows == null || rows.Length != Size)
                throw new System.InvalidOperationException(invalidRowsMessage);
            // Validate the entire grid before allocating a native Texture2D.
            // Rejecting a malformed later row must never leak an unowned
            // texture, especially during repeated failed scene initialization.
            for (int i = 0; i < Size; i++)
                if (rows[i] == null || rows[i].Length != Size)
                    throw new System.InvalidOperationException(invalidRowLengthMessage);

            texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = textureName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            if (setAnisoZero)
                texture.anisoLevel = 0;

            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    pixels[y * Size + x] = colorMapper(rows[Size - 1 - y][x]);

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            Sprite sprite = useFullRectMesh
                ? Sprite.Create(
                    texture,
                    new Rect(0f, 0f, Size, Size),
                    new Vector2(0.5f, 0.5f),
                    16f,
                    0,
                    SpriteMeshType.FullRect)
                : Sprite.Create(
                    texture,
                    new Rect(0, 0, Size, Size),
                    new Vector2(0.5f, 0.5f),
                    Size);
            // Texture.name does not propagate to Sprite.Create. Keep each
            // visible pose identifiable for the guard-action regression and
            // for artifact inspection without changing ORK battle state.
            sprite.name = spriteName;
            return sprite;
        }

        private static Color32 MapColoredPixel(char part, Color32 coat)
        {
            switch (part)
            {
                case 'k': return new Color32(24, 32, 41, 255);
                case 'h': return new Color32(82, 51, 43, 255);
                case 'f': return new Color32(226, 180, 136, 255);
                case 'c': return coat;
                case 'g': return new Color32(195, 181, 151, 255);
                case 'b': return new Color32(67, 70, 75, 255);
                case 'w': return new Color32(181, 202, 207, 255);
                default: return new Color32(0, 0, 0, 0);
            }
        }
    }
}
