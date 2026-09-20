using UnityEditor;
using UnityEngine;
namespace Tankfall.EditorTools
{
    public sealed class CasualGuiImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            bool panorama=assetPath.Contains("/_Project/Resources/Art/world-");
            bool ground=assetPath.Contains("/_Project/Resources/Art/ground-");
            if(!ground && !panorama && !assetPath.Contains("/_Project/Resources/Gui/")) return;
            var t=(TextureImporter)assetImporter;
            t.textureType=TextureImporterType.Default;
            t.npotScale=TextureImporterNPOTScale.None;
            t.alphaIsTransparency=true; t.mipmapEnabled=false;
            t.wrapMode=TextureWrapMode.Clamp; t.filterMode=FilterMode.Bilinear;
            t.textureCompression=TextureImporterCompression.Uncompressed;
            t.maxTextureSize=panorama?4096:2048;
            if(ground) { t.mipmapEnabled=true; t.wrapMode=TextureWrapMode.Repeat; t.anisoLevel=8; }
            if(panorama) { t.wrapModeU=TextureWrapMode.Mirror; t.wrapModeV=TextureWrapMode.Clamp; }
        }
    }
}
