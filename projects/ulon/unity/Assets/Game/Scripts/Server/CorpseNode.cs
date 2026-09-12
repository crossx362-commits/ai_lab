using UnityEngine;

// reload-nudge-2
namespace Ulon.Server
{
    public sealed class CorpseNode : MonoBehaviour
    {
        public string CorpseId;
        public string OwnerId;

        /// <summary>
        /// **주인 몸 그 자체**(직렬화 안 함). 약탈 권리는 주인의 파티로 갈리는데, 주인을 계정 문자열로
        /// 되찾으려면 시체에 이름을 적는 자와 되찾는 자의 규칙이 같아야 한다 — 오프라인에서는 그 둘이
        /// 어긋나 **주인을 아무도 못 찾고 파티 잠금이 통째로 풀렸다**(2026-09-08 실측). 몸을 직접 붙든다.
        /// </summary>
        [System.NonSerialized] public WorldBody OwnerBody;
        public string LastKind = "";
        public float LastX;
        public float LastY;
        public float LastZ;
        public float InteractRange = 2.4f;
        public float DecaySeconds = 900f;
        /// <summary>
        /// 소유자/파티 우선권 창(초). GAME_DESIGN §18.4는 「우선권 시간」만 있고 수치는 없음 —
        /// Classic UO loot rights ≈ 2분(uo.com wiki · death and dying / loot rights)을 따른다.
        /// DecaySeconds(900)의 일부. 0 이하면 창 없이 공개(ExclusiveDisabled).
        /// </summary>
        public const float DefaultExclusiveSeconds = 120f;
        public float ExclusiveSeconds = DefaultExclusiveSeconds;
        public float SpawnedAt;
        public readonly System.Collections.Generic.List<Ulon.Shared.ItemRecord> Items = new System.Collections.Generic.List<Ulon.Shared.ItemRecord>();

        public float SecondsLeft
        {
            get
            {
                float left = DecaySeconds - (Time.time - SpawnedAt);
                return left > 0f ? left : 0f;
            }
        }
    }
}
