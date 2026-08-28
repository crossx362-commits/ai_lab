using UnityEngine;

namespace AshesToStars
{
    /// <summary>전투 이펙트가 캐릭터·몹·보스보다 뒤로 숨지 않게 전면 정렬값을 고정한다.</summary>
    public static class FxPoolSelfCheck
    {
        public static void Run()
        {
            Debug.Assert(FxPool.FrontSortingOrder > 1100,
                "[FxPool] 전면 이펙트 정렬값은 몬스터·보스 깊이 정렬(최대 1100)보다 커야 한다");
            var hit = Resources.Load<Texture2D>("fx/fx_hit");
            Debug.Assert(hit != null, "[FxPool] fx_hit 런타임 리소스가 있어야 한다");
            Debug.Assert(hit != null && hit.width == 512 && hit.height == 512,
                "[FxPool] fx_hit는 투명 512×512 런타임 심볼이어야 한다");
            if (hit != null && hit.isReadable)
            {
                var pixels = hit.GetPixels32();
                int clear = 0;
                int visible = 0;
                for (int i = 0; i < pixels.Length; i += 31)
                {
                    if (pixels[i].a < 16) clear++;
                    if (pixels[i].a > 64) visible++;
                }
                Debug.Assert(clear > 1000, "[FxPool] fx_hit 배경은 투명해야 한다");
                Debug.Assert(visible > 500, "[FxPool] fx_hit 충격 실루엣이 비어 있지 않아야 한다");
            }
            var fire = Resources.Load<Texture2D>("fx/fx_fire");
            Debug.Assert(fire != null, "[FxPool] fx_fire 런타임 리소스가 있어야 한다");
            Debug.Assert(fire != null && fire.width == 512 && fire.height == 512,
                "[FxPool] fx_fire는 투명 512×512 런타임 심볼이어야 한다");
            if (fire != null && fire.isReadable)
            {
                var pixels = fire.GetPixels32();
                int clear = 0;
                int visible = 0;
                for (int i = 0; i < pixels.Length; i += 31)
                {
                    if (pixels[i].a < 16) clear++;
                    if (pixels[i].a > 64) visible++;
                }
                Debug.Assert(clear > 1000, "[FxPool] fx_fire 배경은 투명해야 한다");
                Debug.Assert(visible > 500, "[FxPool] fx_fire 불꽃 실루엣이 비어 있지 않아야 한다");
            }
            var slash = Resources.Load<Texture2D>("fx/fx_slash");
            Debug.Assert(slash != null, "[FxPool] fx_slash 런타임 리소스가 있어야 한다");
            Debug.Assert(slash != null && slash.width == 512 && slash.height == 512,
                "[FxPool] fx_slash는 투명 512×512 런타임 심볼이어야 한다");
            if (slash != null && slash.isReadable)
            {
                var pixels = slash.GetPixels32();
                int clear = 0;
                int visible = 0;
                for (int i = 0; i < pixels.Length; i += 31)
                {
                    if (pixels[i].a < 16) clear++;
                    if (pixels[i].a > 64) visible++;
                }
                Debug.Assert(clear > 1000, "[FxPool] fx_slash 배경은 투명해야 한다");
                Debug.Assert(visible > 500, "[FxPool] fx_slash 베기 실루엣이 비어 있지 않아야 한다");
            }
            var heal = Resources.Load<Texture2D>("fx/fx_heal");
            Debug.Assert(heal != null, "[FxPool] fx_heal 런타임 리소스가 있어야 한다");
            Debug.Assert(heal != null && heal.width == 512 && heal.height == 512,
                "[FxPool] fx_heal은 투명 512×512 런타임 심볼이어야 한다");
            if (heal != null && heal.isReadable)
            {
                var pixels = heal.GetPixels32();
                int clear = 0;
                int visible = 0;
                for (int i = 0; i < pixels.Length; i += 31)
                {
                    if (pixels[i].a < 16) clear++;
                    if (pixels[i].a > 64) visible++;
                }
                Debug.Assert(clear > 1000, "[FxPool] fx_heal 배경은 투명해야 한다");
                Debug.Assert(visible > 500, "[FxPool] fx_heal 치유 고리 실루엣이 비어 있지 않아야 한다");
            }
            var shield = Resources.Load<Texture2D>("fx/fx_shield");
            Debug.Assert(shield != null, "[FxPool] fx_shield 런타임 리소스가 있어야 한다");
            Debug.Assert(shield != null && shield.width == 512 && shield.height == 512,
                "[FxPool] fx_shield는 투명 512×512 런타임 심볼이어야 한다");
            if (shield != null && shield.isReadable)
            {
                var pixels = shield.GetPixels32();
                int clear = 0;
                int visible = 0;
                for (int i = 0; i < pixels.Length; i += 31)
                {
                    if (pixels[i].a < 16) clear++;
                    if (pixels[i].a > 64) visible++;
                }
                Debug.Assert(clear > 1000, "[FxPool] fx_shield 배경은 투명해야 한다");
                Debug.Assert(visible > 500, "[FxPool] fx_shield 보호 고리 실루엣이 비어 있지 않아야 한다");
            }
            var taunt = Resources.Load<Texture2D>("fx/fx_taunt");
            Debug.Assert(taunt != null, "[FxPool] fx_taunt 런타임 리소스가 있어야 한다");
            Debug.Assert(taunt != null && taunt.width == 512 && taunt.height == 512,
                "[FxPool] fx_taunt는 투명 512×512 런타임 심볼이어야 한다");
            if (taunt != null && taunt.isReadable)
            {
                var pixels = taunt.GetPixels32();
                int clear = 0;
                int visible = 0;
                for (int i = 0; i < pixels.Length; i += 31)
                {
                    if (pixels[i].a < 16) clear++;
                    if (pixels[i].a > 64) visible++;
                }
                Debug.Assert(clear > 1000, "[FxPool] fx_taunt 배경은 투명해야 한다");
                Debug.Assert(visible > 500, "[FxPool] fx_taunt 동심 충격파 실루엣이 비어 있지 않아야 한다");
            }
            var summon = Resources.Load<Texture2D>("fx/fx_summon");
            Debug.Assert(summon != null, "[FxPool] fx_summon 런타임 리소스가 있어야 한다");
            Debug.Assert(summon != null && summon.width == 512 && summon.height == 512,
                "[FxPool] fx_summon은 투명 512×512 런타임 심볼이어야 한다");
            if (summon != null && summon.isReadable)
            {
                var pixels = summon.GetPixels32();
                int clear = 0;
                int visible = 0;
                for (int i = 0; i < pixels.Length; i += 31)
                {
                    if (pixels[i].a < 16) clear++;
                    if (pixels[i].a > 64) visible++;
                }
                Debug.Assert(clear > 1000, "[FxPool] fx_summon 배경은 투명해야 한다");
                Debug.Assert(visible > 500, "[FxPool] fx_summon 룬 마법진 실루엣이 비어 있지 않아야 한다");
            }
            var death = Resources.Load<Texture2D>("fx/fx_death");
            Debug.Assert(death != null, "[FxPool] fx_death 런타임 리소스가 있어야 한다");
            Debug.Assert(death != null && death.width == 512 && death.height == 512,
                "[FxPool] fx_death는 투명 512×512 런타임 심볼이어야 한다");
            if (death != null && death.isReadable)
            {
                var pixels = death.GetPixels32();
                int clear = 0;
                int visible = 0;
                for (int i = 0; i < pixels.Length; i += 31)
                {
                    if (pixels[i].a < 16) clear++;
                    if (pixels[i].a > 64) visible++;
                }
                Debug.Assert(clear > 1000, "[FxPool] fx_death 배경은 투명해야 한다");
                Debug.Assert(visible > 500, "[FxPool] fx_death 연기 확산 실루엣이 비어 있지 않아야 한다");
            }
            Debug.Log("[FxPoolSelfCheck] PASS");
        }
    }
}
