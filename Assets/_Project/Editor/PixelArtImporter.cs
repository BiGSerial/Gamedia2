// Assets/_Project/Editor/PixelArtImporter.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class PixelArtImporter : AssetPostprocessor
{
    const int PPU = 16;

    void OnPreprocessTexture()
    {
        // ajuste os caminhos conforme suas pastas de arte
        if (!assetPath.Contains("/PixelAdventure/") && !assetPath.Contains("/Art/"))
            return;

        var ti = (TextureImporter)assetImporter;

        // Básico de pixel art
        ti.textureType         = TextureImporterType.Sprite;
        ti.spritePixelsPerUnit = PPU;
        ti.filterMode          = FilterMode.Point;
        ti.mipmapEnabled       = false;
        ti.textureCompression  = TextureImporterCompression.Uncompressed;
        ti.wrapMode            = TextureWrapMode.Clamp;

        // >>> AQUI está a diferença: usar TextureImporterSettings <<<
        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; // (em vez de ti.spriteMeshType)
        ti.SetTextureSettings(settings);

        // Se quiser garantir sprite mode e alpha:
        // settings.spriteMode = (int)SpriteImportMode.Single ou Multiple (não obrigue se tem tilesheet)
        // settings.alphaIsTransparency = true;
    }
}
#endif
