using Ulon.Shared;
using UnityEditor;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {
        static readonly (string Name, string Fbx, Color Tint, string Why)[] ExtraLooks =
        {
            ("SkelMage", SkeletonMageFbx, new Color(0.55f, 0.75f, 0.45f), "해골주술사 — 주술사보다 크고 병든 녹뼈"),
            ("Squire", KnightFbx, new Color(0.22f, 0.28f, 0.38f), "종자 — 기사보다 작고 어두운 강철"),
            ("Cutthroat", RogueFbx, new Color(0.18f, 0.42f, 0.28f), "칼잡이 — 도적·자객과 다른 숲녹"),
            ("Seer", MageFbx, new Color(0.72f, 0.48f, 0.82f), "은둔사제 — 헥사크·훈련사와 다른 보라 로브"),
            ("Bonekin", SkeletonFbx, new Color(0.70f, 0.58f, 0.32f), "갱도해골 — 모래빛 뼈, 스켈레톤보다 작다"),
            ("Runt", SkeletonMinionFbx, new Color(0.38f, 0.22f, 0.48f), "잔당 — 졸병보다 작고 멍든 보라"),
        };

        public static Vector3 ExtraSpotWorld(int i)
        {
            var p = ExtraMobRoster.World(i);
            return new Vector3(p.x, 0f, p.y);
        }

        public static void EnsureExtraMobs()
        {
            var spots = ExtraMobRoster.Spots;
            for (int i = 0; i < spots.Length; i++)
            {
                string fbx = ExtraFbxOf(spots[i].Name);
                if (fbx == null)
                    throw new System.InvalidOperationException("추가 몹 외형 원장에 " + spots[i].Name + "이(가) 없습니다.");
                EnsureHuntMob(spots[i].Name, spots[i].MobId, fbx, ExtraSpotWorld(i));
            }
            EnsureExtraMobLooks();
        }

        public static void EnsureExtraMobPlacement()
        {
            var spots = ExtraMobRoster.Spots;
            int moved = 0;
            for (int i = 0; i < spots.Length; i++)
            {
                var go = GameObject.Find(spots[i].Name);
                if (go == null)
                    continue;
                Vector3 world = ExtraSpotWorld(i);
                float y = GroundHeightAt(world.x, world.z);
                var want = new Vector3(world.x, y, world.z);
                if ((go.transform.position - want).sqrMagnitude > 0.0001f)
                    moved++;
                go.transform.position = want;
                go.transform.rotation = Quaternion.Euler(0f, spots[i].Yaw, 0f);
            }
            Debug.Log("[Ulon] 추가 잡몹 배치 — " + spots.Length + "체 지표 세우기(옮긴 것 " + moved + "체)");
        }

        static string ExtraFbxOf(string name)
        {
            for (int i = 0; i < ExtraLooks.Length; i++)
                if (ExtraLooks[i].Name == name)
                    return ExtraLooks[i].Fbx;
            return null;
        }

        static void EnsureExtraMobLooks()
        {
            int done = 0;
            for (int i = 0; i < ExtraLooks.Length; i++)
            {
                var go = GameObject.Find(ExtraLooks[i].Name);
                if (go == null)
                    continue;
                TintCharacter(go, "Mob" + ExtraLooks[i].Name, ExtraLooks[i].Tint);
                done++;
            }
            Debug.Log("[Ulon] 추가 잡몹 외형 — " + done + "체 몸 색(" + ExtraLooks.Length + "종 원장)");
        }
    }
}
