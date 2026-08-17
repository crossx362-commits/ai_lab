using UnityEditor;
using UnityEngine;

/// <summary>
/// Resources 아래 텍스처의 임포트 설정을 강제한다.
///
/// 왜 필요한가 (2026-08-13 실측 사고):
///   Texture2D.PackTextures()는 원본 텍스처가 **Read/Write 가능**해야 동작한다.
///   기본 임포트 설정은 isReadable=false라 아틀라스 생성이 조용히 실패했고,
///   그 결과 스프라이트가 하나도 안 그려진 채로 성능 측정이 끝났다.
///   FPS는 좋게 나왔지만 **렌더링을 안 한 수치**였다 — 통과가 아니라 무효.
/// </summary>
public class TextureImportRules : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/Resources/")) return;

        var ti = (TextureImporter)assetImporter;
        ti.isReadable = true;                 // ← 이게 핵심
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        // ✅ 아트 방향이 픽셀아트로 확정(2026-08-13) — Bilinear면 도트가 뭉개진다
        ti.filterMode = FilterMode.Bilinear   /* 화풍 전환 2026-08-18: HK 손그림, Point면 계단 */;
        // 오너 픽셀아트 도입 — 캐릭터 416×297급 + 런타임 아틀라스(캐릭터 52장 + 몹 22장 + 플레이스홀더) 수용
        ti.maxTextureSize = 4096;

        if (assetPath.Contains("/sprites/") || assetPath.Contains("/Sprites/"))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            // 픽셀아트 기본값 — 방향별 스프라이트가 오면 그대로 적용된다
            ti.spritePixelsPerUnit = 32;
            ti.spritePivot = new Vector2(0.5f, 0.06f);   // 발밑 피벗 — 쿼터뷰 Y정렬 기준
        }
        else if (assetPath.Contains("/FX/") || assetPath.Contains("/out_effects/"))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.spritePixelsPerUnit = 32;
            // 이펙트는 중앙 피벗 — 대상 위에 겹쳐 그린다
        }
        else if (assetPath.Contains("/ground/") || assetPath.Contains("/Ground/"))
        {
            ti.filterMode = FilterMode.Bilinear   /* 화풍 전환 2026-08-18: HK 손그림, Point면 계단 */;      // 바닥도 픽셀아트 타일 — Bilinear면 도트가 뭉개진다
            ti.textureType = TextureImporterType.Default;
            ti.wrapMode = TextureWrapMode.Repeat;   // 타일링 필수
            ti.alphaSource = TextureImporterAlphaSource.None;
        }
    }
}
