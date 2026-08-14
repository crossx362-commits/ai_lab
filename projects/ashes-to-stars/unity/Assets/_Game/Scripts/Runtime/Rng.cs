namespace AshesToStars
{
    // ─────────────────────────────────────────────────────────────
    // 결정적 난수 (던전 생성 명세 §3-2)
    //
    // 왜 UnityEngine.Random을 쓰지 않나:
    //   그것은 **전역 정적 상태**다. 이펙트든 에디터 도구든 아무나 그 사이에 Random.value를
    //   부르면 던전이 조용히 달라진다. 영구사망 게임에서 "이 판을 시드로 재현할 수 있는가"는
    //   기능 요구사항이지 편의가 아니다 — 캐릭터가 삭제됐다는 신고가 오면 그 판을 다시 만들어야 한다.
    //
    // 왜 System.Random도 아닌가:
    //   .NET 버전이 바뀌면 **같은 시드가 다른 수열**을 낸다(MS 공식 문서). 시드 호환이 깨진다.
    //
    // 왜 com.unity.mathematics 패키지를 안 넣나:
    //   의존성 추가는 승인 사항이고(CLAUDE.md), 이 프로젝트는 패키지를 최소로 유지해 왔다.
    //   Unity.Mathematics.Random이 "32비트 최소 상태의 임베드 가능한 구조체"인 이유가
    //   정확히 이 용도이므로, 그 설계만 베끼고 의존성은 지지 않는다.
    // ─────────────────────────────────────────────────────────────

    /// <summary>난수 스트림 채널. 서브시스템마다 독립 스트림을 쓴다(§3-2 규칙 2).</summary>
    public enum SeedChannel
    {
        Layout = 1,     // 노드 수·그래프 형태·사이클 위치
        Template = 2,   // 노드별 아레나 템플릿·청크
        Wave = 3,       // 웨이브 편성
        Boon = 4,       // 강화 3택 후보
        Drop = 5,       // 드랍 판정
        Terrain = 6,    // NoiseTerrain
        Decor = 7,      // FieldDecor 프랍 배치
        Boss = 8,       // 보스 종류·마릿수
    }

    /// <summary>
    /// xorshift32. 구조체라 값으로 복사되고, 복사본은 원본과 무관하게 굴러간다 —
    /// 그래서 "이 노드의 난수"를 떼어내 넘겨도 다른 곳의 수열을 오염시키지 않는다.
    /// </summary>
    public struct Rng
    {
        uint _s;

        /// <summary>0은 xorshift의 고정점이라 1로 보정한다(§3-6 R6).</summary>
        public Rng(uint seed) { _s = seed == 0u ? 1u : seed; _consumed = 0; }

        int _consumed;

        /// <summary>이 스트림이 지금까지 몇 번 소비됐는지 — 네거티브 컨트롤 N4가 이걸 본다.</summary>
        public int Consumed => _consumed;

        public uint NextUInt()
        {
            _consumed++;
            uint x = _s;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _s = x;
            return x;
        }

        /// <summary>[0, max) 정수. max가 0 이하면 0.</summary>
        public int Next(int max) => max <= 0 ? 0 : (int)(NextUInt() % (uint)max);

        /// <summary>[min, max) 정수.</summary>
        public int Range(int min, int max) => max <= min ? min : min + Next(max - min);

        /// <summary>[0, 1) 실수.</summary>
        public float Value01() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>확률 p로 true.</summary>
        public bool Chance(float p) => Value01() < p;

        /// <summary>제자리 셔플(Fisher–Yates). 순회 순서에 의존하지 않게 하려고 쓴다(§3-2 규칙 3).</summary>
        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// 런 시드 + 위치 + 채널 → 그 스트림의 시작 상태.
        ///
        /// 세 값을 섞어야 하는 이유: 채널만 다르면 노드마다 같은 수열이 나오고,
        /// 노드만 다르면 채널끼리 같은 수열이 나온다. 둘 다 실제로 눈에 띄는 패턴을 만든다.
        /// 섞기는 SplitMix32의 finalizer를 쓴다 — 짧고, 비트가 고르게 퍼지는 것이 검증된 상수다.
        /// </summary>
        public static Rng Stream(uint runSeed, int index, SeedChannel channel)
        {
            uint h = runSeed;
            h = Mix(h ^ (uint)(index * 0x9E3779B9u));
            h = Mix(h ^ ((uint)channel * 0x85EBCA6Bu));
            return new Rng(h);
        }

        static uint Mix(uint x)
        {
            x ^= x >> 16; x *= 0x7FEB352Du;
            x ^= x >> 15; x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }
    }
}
