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
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            // RpgSpriteAutoBuilder가 슬라이스한 시트(Multiple)는 건드리지 않는다 —
            // Single로 되돌리면 슬라이스·프레임별 피벗·PPU가 통째로 날아간다.
            if (ti.spriteImportMode != SpriteImportMode.Multiple)
            {
                ti.spriteImportMode = SpriteImportMode.Single;
                // 픽셀아트 기본값 — 방향별 스프라이트가 오면 그대로 적용된다
                ti.spritePixelsPerUnit = 32;
                ti.spritePivot = new Vector2(0.5f, 0.06f);   // 발밑 피벗 — 쿼터뷰 Y정렬 기준
            }
        }
        else if (assetPath.Contains("/FX/") || assetPath.Contains("/out_effects/"))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            if (ti.spriteImportMode != SpriteImportMode.Multiple)
            {
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 32;
            }
            // 이펙트는 중앙 피벗 — 대상 위에 겹쳐 그린다
        }
        else if (assetPath.Contains("/ground/estate/") || assetPath.Contains("/Ground/estate/"))
        {
            // 2026-08-18: 영지(EstateYard) 다이아몬드 바닥 타일 — 아래 seamless 타일링
            // 규칙(alphaSource=None, wrapMode=Repeat)을 그대로 쓰면 알파가 통째로
            // 버려져 GUI.DrawTexture가 타일 바깥(투명해야 할 영역)을 불투명 검정으로
            // 그린다(실측 사고 — 알파 채널이 있는 마름모 컷아웃 텍스처인데 seamless
            // 무한 타일링 바닥과 같은 규칙을 적용받고 있었다). 다이아몬드 실루엣은
            // 알파로 만드므로 From Input + Clamp가 맞다.
            ti.filterMode = FilterMode.Bilinear;
            ti.textureType = TextureImporterType.Default;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
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
