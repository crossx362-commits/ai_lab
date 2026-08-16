using UnityEditor;
using UnityEngine;
namespace AshesToStars { public static class CombatVfxAtlasSelfCheck { public static void Run() { Debug.Assert(CombatVfxAtlas.IsReady, "[CombatVfx] 아틀라스 로드 실패"); foreach (var key in CombatVfxAtlas.RequiredKeys) Debug.Assert(CombatVfxAtlas.SpriteFor(key) != null, $"[CombatVfx] {key} 누락"); Debug.Log("[CombatVfxAtlasSelfCheck] PASS"); } } }
