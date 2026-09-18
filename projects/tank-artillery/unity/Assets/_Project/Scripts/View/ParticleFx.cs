// 파티클 이펙트 — 폭발과 탄 자취를 유니티 ParticleSystem 으로 만든다(오너 지시 2026-09-17).
//
// 왜 바꿨나: 처음엔 메시 조각을 직접 적분해 뿌렸다(ExplosionFx). 동작은 했지만
// 조각 하나하나가 GameObject 라 수가 늘면 비싸고, 크기·색·알파가 시간에 따라 변하는 연출을
// 전부 손으로 짜야 했다. ParticleSystem 은 그걸 GPU 쪽에서 처리하고 곡선으로 준다.
//
// 구성 근거(조사 2026-09-17): 게임 폭발 VFX 는 **독립된 레이어를 겹쳐** 만든다 —
//   Flash(섬광) · Shockwave(충격파) · Debris(파편) · Smoke(연기) · Bolt(스파크),
//   각 레이어가 **자기 타이밍과 색**을 갖는다. 타이밍은 섬광이 **첫 0.05~0.1초에 정점 후 소멸**,
//   파편은 더 오래 남되 다음 행동을 가리지 않을 것.
//   pcgamer.com/how-great-game-explosions-are-made · zengo.eu/en/blog/getting-know-real-time-vfx-games
//   처음엔 3레이어(섬광·연기·흙)에 섬광 수명을 0.45초까지 줬는데, 그러면 섬광이 연기와 겹쳐
//   "번쩍" 이 사라지고 뿌옇게만 보인다. 조사대로 섬광을 짧게 끊고 충격파·스파크를 더했다.
//
// 범위 확장(오너 지시 2026-09-17 "이펙트는 파티클로 다 표현하고 좀더 경쟁력 있게"):
//   폭발·자취 외에 **발사 섬광 · 피격 스파크 · 실드 · 격파(화구+연기 기둥) · 아이템 · 독/속박 상태 ·
//   설치물(지속불·독구름) 루프 · 위성 빔 · 눈 날씨 · 폭발 광원**까지 전부 이 파일의 ParticleSystem 이다.
//   예전 메시 연출(설치물 구슬 고리, 위성탄 실린더, ExplosionFx 메시 조각)은 전부 걷어냈다 — 두 방식이 섞이면 화면 톤이 갈린다.
//
// ⚠️ 에셋은 여전히 없다 — 시스템도 텍스처도 코드로 만든다(§9 와 같은 방식).
// ⚠️ 파티클 셰이더가 빌드에서 빠지면 분홍색이 된다. BuildScript.EnsureShadersIncluded 에 등록돼 있어야 한다.
// ⚠️ 연출일 뿐이다. 탄도(§5)·피해(§6)·지형(§7) 판정에 절대 끼어들지 않는다.

using System.Collections.Generic;
using UnityEngine;
using Tankfall.Sim;

namespace Tankfall.View
{
    public sealed class ParticleFx : MonoBehaviour
    {
        static Texture2D _dot;
        Material _addMat, _alphaMat;

        readonly List<ParticleSystem> _blastPool = new List<ParticleSystem>();
        readonly Dictionary<Transform, ParticleSystem> _trails = new Dictionary<Transform, ParticleSystem>();
        readonly Dictionary<string, List<ParticleSystem>> _pools = new Dictionary<string, List<ParticleSystem>>();   // 단발 연출 풀(이름별)
        readonly List<(Light light, float born, float intensity)> _lights = new List<(Light, float, float)>();     // 폭발·섬광 광원(짧게 켜고 끈다)
        readonly Dictionary<int, ParticleSystem> _fieldLoops = new Dictionary<int, ParticleSystem>();               // 설치물 루프(스냅샷 인덱스별)
        readonly Dictionary<int, ParticleSystem> _poisonLoops = new Dictionary<int, ParticleSystem>();              // 유닛 독 상태
        readonly Dictionary<int, ParticleSystem> _rootLoops = new Dictionary<int, ParticleSystem>();                // 유닛 속박 상태
        readonly List<ParticleSystem> _wreckLoops = new List<ParticleSystem>();                                     // 격파 연기 기둥(시간 지나면 멈춤)
        readonly Dictionary<Transform, float> _dacsNext = new Dictionary<Transform, float>();                       // 측추력기 펄스 간격(탄별)
        ParticleSystem _snow; Transform _snowFollow;

        /// <summary>폭발 흔들림. 카메라가 매 프레임 읽고 여기서 저절로 줄어든다.</summary>
        public float Shake { get; private set; }

        /// <summary>
        /// 폭발이 아닌 사건(지진 등)이 카메라를 흔들 때. `Shake` 의 set 을 열지 않는 이유는
        /// 감쇠·상한 규칙이 이 파일 안에만 있어야 하기 때문이다 — 밖에서 대입하면 상한이 무시된다.
        /// </summary>
        public void AddShake(float amount) => Shake = Mathf.Min(Shake + Mathf.Max(0f, amount), 1.1f);

        void Update()
        {
            Shake = Mathf.Max(0f, Shake - Time.deltaTime * 2.2f);
            // 광원: 0.35초 동안 꺼진다. 폭발이 겹치면 각자 감쇠하고, 다 꺼진 건 비활성.
            for (int i = _lights.Count - 1; i >= 0; i--)
            {
                var (l, born, inten) = _lights[i];
                if (l == null) { _lights.RemoveAt(i); continue; }
                float t = (Time.time - born) / 0.35f;
                if (t >= 1f) { l.enabled = false; _lights.RemoveAt(i); continue; }
                l.intensity = inten * (1f - t) * (1f - t);
            }
            // 눈: 카메라 머리 위 상자에서 내린다 — 맵 전체에 뿌리면 입자 대부분이 화면 밖이다.
            if (_snow != null && _snowFollow != null)
                _snow.transform.position = _snowFollow.position + Vector3.up * 28f + _snowFollow.forward * 25f;
        }

        // ── 단발 연출 풀 ────────────────────────────────────────
        ParticleSystem Rent(string name, bool additive)
        {
            if (!_pools.TryGetValue(name, out var list)) { list = new List<ParticleSystem>(); _pools[name] = list; }
            foreach (var p in list) if (p != null && !p.IsAlive(true)) return p;
            var ps = NewSystem($"{name}{list.Count}", null, additive);
            list.Add(ps);
            return ps;
        }

        /// <summary>한 번 터지는 시스템의 공통 설정. 모양은 호출부가 뒤에서 덮어쓴다.</summary>
        static ParticleSystem.MainModule Burst(ParticleSystem ps, float life0, float life1, float speed0, float speed1,
                                              float size0, float size1, Color c0, Color c1, float gravity, int count, float radius)
        {
            var m = ps.main;
            m.duration = Mathf.Max(0.1f, life1); m.loop = false; m.playOnAwake = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(life0, life1);
            m.startSpeed = new ParticleSystem.MinMaxCurve(speed0, speed1);
            m.startSize = new ParticleSystem.MinMaxCurve(size0, size1);
            m.startColor = new ParticleSystem.MinMaxGradient(c0, c1);
            m.gravityModifier = gravity;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = Mathf.Max(64, count * 2);
            var e = ps.emission; e.enabled = true; e.rateOverTime = 0f; e.rateOverDistance = 0f;
            e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius; sh.rotation = Vector3.zero; sh.angle = 25f; sh.radiusThickness = 1f;
            var vel = ps.velocityOverLifetime; vel.enabled = false;
            var rot = ps.rotationOverLifetime; rot.enabled = false;
            return m;
        }

        static void Cone(ParticleSystem ps, float angle, float radius, Vector3 euler)
        {
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = angle; sh.radius = radius; sh.rotation = euler;
        }

        void Flash(Vector3 at, Color c, float intensity, float range)
        {
            Light l = null;
            // 꺼진 광원 재사용
            foreach (Transform t in transform)
                if (t.name == "FxLight" && t.TryGetComponent(out Light cand) && !cand.enabled) { l = cand; break; }
            if (l == null)
            {
                var go = new GameObject("FxLight");
                go.transform.SetParent(transform, false);
                l = go.AddComponent<Light>();
                l.type = LightType.Point; l.shadows = LightShadows.None;
            }
            l.transform.position = at + Vector3.up * 1.5f;
            l.color = c; l.range = range; l.intensity = intensity; l.enabled = true;
            _lights.Add((l, Time.time, intensity));
        }

        // ── 코드로 만든 입자 텍스처 ─────────────────────────────
        /// <summary>가운데가 밝고 가장자리로 부드럽게 사라지는 원. 이거 하나로 불꽃·연기·먼지를 다 그린다.</summary>
        static Texture2D Dot
        {
            get
            {
                if (_dot != null) return _dot;
                const int N = 64;
                _dot = new Texture2D(N, N, TextureFormat.RGBA32, true) { hideFlags = HideFlags.HideAndDontSave };
                var px = new Color32[N * N];
                float c = (N - 1) * 0.5f;
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                        // ⚠️ 부드럽게만 깎으면 겹칠 때 **뿌연 흰 덩어리**가 된다(처음 그렇게 나왔다).
                        //    스타일라이즈드는 흐릿한 게 아니라 **또렷한 형태**다 — 가운데를 채우고 가장자리만 짧게 깎는다.
                        float a = Mathf.Clamp01((1f - d) * 2.2f);
                        a = a * a * (3f - 2f * a);     // smoothstep — 중심은 꽉 차고 테두리만 부드럽다
                        px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                _dot.SetPixels32(px);
                _dot.Apply(true);
                return _dot;
            }
        }

        Material AddMat                      // 불꽃·빔 — 겹칠수록 밝아진다
        {
            get
            {
                if (_addMat == null) _addMat = MakeParticleMat(true);
                return _addMat;
            }
        }

        Material AlphaMat                    // 연기·먼지 — 겹쳐도 밝아지지 않는다
        {
            get
            {
                if (_alphaMat == null) _alphaMat = MakeParticleMat(false);
                return _alphaMat;
            }
        }

        /// <summary>
        /// ⚠️ `Particles/Standard Unlit` 을 먼저 썼다가 **입자가 작은 흰 사각형으로** 나왔다 —
        ///    그 셰이더는 `_Mode` 를 코드로 바꿔도 키워드(`_ALPHABLEND_ON` 등)를 같이 켜야 블렌딩이 바뀌고,
        ///    텍스처 슬롯 이름도 버전에 따라 다르다. Legacy 파티클 셰이더는 `_MainTex` 하나로 끝난다.
        ///    연출용이라 기능 차이가 없으므로 **확실히 도는 쪽**을 쓴다.
        /// </summary>
        static Material MakeParticleMat(bool additive)
        {
            var sh = Shader.Find(additive ? "Legacy Shaders/Particles/Additive"
                                          : "Legacy Shaders/Particles/Alpha Blended")
                     ?? Shader.Find("Sprites/Default");
            var m = new Material(sh);
            if (m.HasProperty("_MainTex")) m.mainTexture = Dot;
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            m.enableInstancing = true;
            return m;
        }

        // ── 시스템 만들기 ───────────────────────────────────────
        ParticleSystem NewSystem(string name, Transform parent, bool additive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = additive ? AddMat : AlphaMat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = -5f;
            return ps;
        }

        // ══════════════════════════════════════════════════════
        //  폭발
        // ══════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════
        //  폭발 스타일 — 기종 × 탄종마다 다르다 (오너 지시 2026-09-17 "터지는 이펙트도 다 달라야 됨")
        //
        //  출처(조사 2026-09-17, 나무위키 각 기종 문서 · GameFAQs 43426):
        //   캐논     검콩(포도알, "가장 폭발 범위가 넓으며") / 빨콩(앵두알, "단발 화력이 가장 강해")   — 캐논 탱크
        //   캐터펄트 대형 바위("지형을 없애") / 불덩어리 바위("지속적인 대미지를 주는 불을 생성… 바람을 따라 퍼져") — 캐터펄트(포트리스 시리즈)
        //   크로스보우 작살(폭발범위 31 최하위) / 독화살("지속적으로 체력이 떨어지는 독")                — 크로스보우(포트리스 시리즈)
        //   캐롯     일반 탱크포 / 3발 소형포("녹색 소형 포탄", "해골 모양으로 폭발")                      — 캐롯 탱크
        //   듀크     일반 탱크포 / 독가스 포탄("독가스 구름을 생성… 바람을 타고 이동", 원작 색 연두)      — 듀크 탱크
        //   마인랜더 수류탄 / 지뢰("그 자리에 지뢰가 설치")                                              — 마인랜더
        //   미사일·슈퍼탱크 "3단 폭발 3연발 미사일(핫도그)" / "9연발 유도탄", "폭발 이펙트는 지뢰탱과 비슷" — 슈퍼탱크
        //   레이저   단발 레이저탄("검콩에 버금가는 폭발 범위") / 3발 회전 레이저탄                        — 레이저 탱크
        //   이온     이온샷 / 이온 위성탄("ION ATTACK from above")                                        — GameFAQs
        //   포세이돈 "파란 물덩어리" / "보라색 물덩어리"(2턴 이동 봉쇄), 눈이면 +25%                       — 포세이돈(포트리스 시리즈)
        //   세크윈드 에너지탄("도넛 모양", "연기가 나면서 레이저") / 2발 회전 에너지탄                     — 세크윈드
        //  원작은 2D 스프라이트라 3D 파티클 수치는 전부 [추정]이다 — 색·성격(불/독/물/전기/먼지)만 원작을 따른다.
        //  ⚠️ 연출일 뿐이다. 굴착 반경(radius)은 Sim 이 주는 값을 그대로 쓴다 — 여기서 크기를 속이면 §7 이 거짓말이 된다.
        // ══════════════════════════════════════════════════════
        public struct BlastStyle
        {
            public Color Flash, Flash2;      // 섬광 두 색
            public Color Smoke;              // 연기 색(알파 포함)
            public Color Wave;               // 충격파 링
            public Color Bolt;               // 스파크
            public Color Debris;             // 파편 색. UseDirt 면 지형 흙색으로 대체
            public bool UseDirt;
            public float FlashMul, SmokeMul, SmokeLife, SmokeRise, DebrisMul, BoltMul, WaveMul;
            public int Stages;               // 다단 폭발(미사일 3단) — 섬광을 시차로 여러 번
            public bool Column;              // 스파크가 위로 기둥처럼 솟는다(전기·빔)
            public bool Splash;              // 파편이 물방울처럼 위로 솟아 떨어진다(물)

            static BlastStyle Fire(Color a, Color b) => new BlastStyle
            {
                Flash = a, Flash2 = b, Smoke = new Color(0.58f, 0.56f, 0.54f, 0.42f),
                Wave = new Color(1f, 0.95f, 0.8f), Bolt = new Color(1f, 0.9f, 0.6f), UseDirt = true,
                FlashMul = 1f, SmokeMul = 1f, SmokeLife = 1f, SmokeRise = 0.04f, DebrisMul = 1f, BoltMul = 1f, WaveMul = 1f, Stages = 1,
            };

            public static BlastStyle Of(TankKind kind, ShellKind shell)
            {
                bool sp = shell == ShellKind.Special;
                BlastStyle s;
                switch (kind)
                {
                    case TankKind.Cannon:
                        // 검콩: 검은 연기가 넓게. 빨콩: 붉고 촘촘한 화구
                        s = sp ? Fire(new Color(1f, 0.55f, 0.35f), new Color(0.95f, 0.15f, 0.08f)) : Fire(new Color(1f, 0.85f, 0.5f), new Color(0.9f, 0.45f, 0.15f));
                        if (sp) { s.Wave = new Color(1f, 0.5f, 0.35f); s.Bolt = new Color(1f, 0.45f, 0.25f); s.FlashMul = 1.3f; s.BoltMul = 1.5f; s.SmokeMul = 0.6f; }
                        else { s.Smoke = new Color(0.22f, 0.21f, 0.22f, 0.5f); s.SmokeMul = 1.5f; s.SmokeLife = 1.3f; }
                        break;
                    case TankKind.Catapult:
                        if (sp)
                        {   // 불덩어리 바위 — 불이 남아 타오른다: 주황 연기가 길게, 위로 오른다
                            s = Fire(new Color(1f, 0.75f, 0.3f), new Color(1f, 0.4f, 0.1f));
                            s.Smoke = new Color(1f, 0.45f, 0.12f, 0.5f); s.SmokeLife = 2.4f; s.SmokeRise = 0.35f; s.SmokeMul = 1.3f;
                            s.Bolt = new Color(1f, 0.6f, 0.2f); s.BoltMul = 1.4f; s.Wave = new Color(1f, 0.6f, 0.3f);
                        }
                        else
                        {   // 대형 바위 — 불 없이 흙먼지와 돌조각만
                            s = Fire(new Color(0.75f, 0.68f, 0.55f), new Color(0.6f, 0.52f, 0.4f));
                            s.FlashMul = 0.35f; s.Smoke = new Color(0.62f, 0.55f, 0.42f, 0.5f); s.SmokeMul = 1.6f; s.SmokeLife = 1.2f;
                            s.DebrisMul = 1.8f; s.BoltMul = 0f; s.Wave = new Color(0.75f, 0.68f, 0.55f); s.WaveMul = 0.6f;
                        }
                        break;
                    case TankKind.CrossBow:
                        // 작살: 작고 날카로운 파편. 독화살: 작은 녹색 퍼프
                        s = Fire(new Color(0.95f, 0.9f, 0.8f), new Color(0.8f, 0.7f, 0.5f));
                        s.FlashMul = 0.5f; s.SmokeMul = 0.5f; s.WaveMul = 0.4f; s.BoltMul = 1.6f; s.Bolt = new Color(0.95f, 0.92f, 0.85f);
                        if (sp) { s.Flash = new Color(0.6f, 0.95f, 0.4f); s.Flash2 = new Color(0.3f, 0.7f, 0.2f); s.Smoke = new Color(0.45f, 0.8f, 0.3f, 0.45f); s.SmokeMul = 1f; s.SmokeLife = 1.6f; s.Bolt = new Color(0.6f, 1f, 0.5f); s.Wave = new Color(0.6f, 0.95f, 0.5f); }
                        break;
                    case TankKind.Carrot:
                        // 일반: 표준 화구. 소형포: 녹색 소형 폭발(원작 "녹색 소형 포탄")
                        s = Fire(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.55f, 0.16f));
                        if (sp) { s.Flash = new Color(0.75f, 1f, 0.5f); s.Flash2 = new Color(0.3f, 0.8f, 0.3f); s.Smoke = new Color(0.55f, 0.75f, 0.45f, 0.42f); s.Wave = new Color(0.7f, 1f, 0.6f); s.Bolt = new Color(0.7f, 1f, 0.5f); s.SmokeMul = 0.7f; }
                        break;
                    case TankKind.Duke:
                        s = Fire(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.55f, 0.16f));
                        if (sp)
                        {   // 독가스 구름 — 섬광 거의 없이 연두색 구름이 오래 남는다
                            s.FlashMul = 0.3f; s.Flash = new Color(0.7f, 0.95f, 0.4f); s.Flash2 = new Color(0.5f, 0.8f, 0.3f);
                            s.Smoke = new Color(0.55f, 0.85f, 0.30f, 0.55f); s.SmokeMul = 2.2f; s.SmokeLife = 3.0f; s.SmokeRise = 0.12f;
                            s.Bolt = new Color(0.7f, 1f, 0.4f); s.BoltMul = 0.4f; s.Wave = new Color(0.7f, 0.95f, 0.5f); s.WaveMul = 0.6f; s.DebrisMul = 0.4f;
                        }
                        break;
                    case TankKind.MineLander:
                        // 수류탄·지뢰: 짧고 매운 검은 폭발, 스파크 많이 (원작 "지뢰탱과 비슷"한 이펙트가 미사일 기본)
                        s = Fire(new Color(1f, 0.8f, 0.45f), new Color(0.9f, 0.4f, 0.1f));
                        s.Smoke = new Color(0.18f, 0.17f, 0.18f, 0.55f); s.SmokeLife = 0.8f; s.BoltMul = 1.8f; s.FlashMul = sp ? 1.2f : 0.9f; s.WaveMul = 1.2f;
                        break;
                    case TankKind.Missile:
                        // 3단 폭발 — 섬광이 세 번 시차로 터지고 검은 연기 기둥
                        s = Fire(new Color(1f, 0.8f, 0.4f), new Color(1f, 0.45f, 0.1f));
                        s.Stages = 3; s.Smoke = new Color(0.25f, 0.23f, 0.22f, 0.5f); s.SmokeRise = 0.25f; s.SmokeLife = 1.4f; s.SmokeMul = 1.3f;
                        break;
                    case TankKind.MultiMissile:
                        // 9연발 — 한 발은 작고 빠르다(9개가 겹쳐 덩어리를 만든다)
                        s = Fire(new Color(1f, 0.9f, 0.6f), new Color(1f, 0.6f, 0.2f));
                        s.FlashMul = 0.8f; s.SmokeMul = 0.6f; s.SmokeLife = 0.8f; s.BoltMul = 1.3f; s.DebrisMul = 0.7f;
                        break;
                    case TankKind.SuperTank:
                        // 핫도그(3단) / 유도탄 — 푸른빛 도는 화구로 다른 미사일과 갈린다
                        s = Fire(new Color(0.8f, 0.9f, 1f), new Color(0.35f, 0.55f, 1f));
                        s.Stages = sp ? 1 : 3; s.Wave = new Color(0.6f, 0.75f, 1f); s.Bolt = new Color(0.7f, 0.85f, 1f); s.Smoke = new Color(0.35f, 0.38f, 0.48f, 0.5f); s.SmokeRise = 0.2f;
                        break;
                    case TankKind.Laser:
                        // 빔 — 붉은 빛무리, 연기 없음, 빛 기둥
                        s = Fire(new Color(1f, 0.55f, 0.6f), new Color(1f, 0.2f, 0.3f));
                        s.SmokeMul = 0f; s.Wave = new Color(1f, 0.4f, 0.5f); s.WaveMul = 1.3f; s.Bolt = new Color(1f, 0.5f, 0.6f); s.BoltMul = 1.6f; s.Column = true; s.DebrisMul = 0.6f; s.FlashMul = 1.2f;
                        break;
                    case TankKind.IonAttacker:
                        // 이온 — 청백 전기. 위성탄은 하늘에서 내려오는 빔(ShowSatelliteBeam)이 따로 있다
                        s = Fire(new Color(0.85f, 0.97f, 1f), new Color(0.4f, 0.8f, 1f));
                        s.SmokeMul = 0.3f; s.Smoke = new Color(0.6f, 0.8f, 0.95f, 0.35f); s.Wave = new Color(0.6f, 0.9f, 1f); s.WaveMul = 1.4f;
                        s.Bolt = new Color(0.7f, 0.95f, 1f); s.BoltMul = 2.2f; s.Column = true; s.FlashMul = sp ? 1.5f : 1.1f;
                        break;
                    case TankKind.Poseidon:
                        // 물덩어리 — 파랑(1번)/보라(2번) 물이 솟구쳐 떨어지고 흰 물보라
                        s = Fire(Color.white, sp ? new Color(0.75f, 0.55f, 0.95f) : new Color(0.55f, 0.8f, 1f));
                        s.FlashMul = 0.6f; s.UseDirt = false; s.Debris = sp ? new Color(0.62f, 0.42f, 0.9f) : new Color(0.35f, 0.62f, 0.95f);
                        s.DebrisMul = 2.0f; s.Splash = true; s.Smoke = new Color(0.9f, 0.95f, 1f, 0.45f); s.SmokeLife = 0.7f; s.SmokeMul = 1.2f;
                        s.Wave = sp ? new Color(0.75f, 0.55f, 0.95f) : new Color(0.55f, 0.8f, 1f); s.Bolt = new Color(0.8f, 0.92f, 1f); s.BoltMul = 0.8f;
                        break;
                    default:   // SecWind
                        // 도넛 모양 에너지탄 — 큰 청록 링, 옅은 연기
                        s = Fire(new Color(0.8f, 1f, 0.95f), new Color(0.4f, 0.9f, 0.8f));
                        s.Wave = new Color(0.55f, 0.95f, 0.85f); s.WaveMul = 1.9f; s.Smoke = new Color(0.6f, 0.85f, 0.8f, 0.35f); s.SmokeMul = 0.6f;
                        s.Bolt = new Color(0.7f, 1f, 0.9f); s.FlashMul = 0.8f; s.DebrisMul = 0.6f;
                        break;
                }
                return s;
            }
        }

        /// <param name="radius">굴착 반경. 연출 크기가 실제 파괴 크기를 따라가야 "얼마나 팠는지"가 눈에 읽힌다.</param>
        /// <param name="dirt">그 지형의 흙색(MapTheme). 사막에서 초록 파편이 튀면 안 된다.</param>
        /// <param name="ultimate">궁극기(§49) = 핵. true 면 버섯구름이 얹힌다(오너 확정 "궁극기가 핵 쏘는 거라니까").</param>
        public void Blast(Vector3 at, float radius, Color dirt, TankKind kind, ShellKind shell, bool ultimate = false)
        {
            var ps = RentBlast();
            ps.transform.position = at;
            ConfigureBlast(ps, radius, dirt, BlastStyle.Of(kind, shell));
            ps.Clear(true);
            ps.Play(true);

            // 흔들림은 반경에 비례하되 상한을 둔다 — 굴착탄(16m)에서 화면이 뒤집히면 조준을 못 한다.
            // ⚠️ 핵만 상한을 올린다. 상한 자체를 올리면 평범한 착탄에서도 조준이 불가능해진다.
            Shake = Mathf.Min(Shake + radius * (ultimate ? 0.12f : 0.055f), ultimate ? 1.8f : 1.1f);

            // 광원: 섬광 색으로 주변 지형·탱크를 한 번 비춘다 — 파티클만으로는 "빛"이 안 난다.
            var st = BlastStyle.Of(kind, shell);
            // ⚠️ 세기를 반경에 정비례로 뒀더니 굴착 14m 급에서 크레이터 바닥이 흰색으로 타 버렸다(스크린샷). 제곱근으로 누른다.
            Flash(at, Color.Lerp(st.Flash, st.Flash2, 0.5f), 2.2f * Mathf.Sqrt(Mathf.Clamp(radius / 7f, 0.5f, 2.4f)) * Mathf.Max(0.3f, st.FlashMul), radius * 2.5f + 8f);

            // 불씨: 오래 남아 천천히 떨어지는 작은 점. 폭발 뒤 "여운"을 만든다(불·전기 계열만)
            if (st.BoltMul > 0.5f)
            {
                var em = Rent("Ember", true);
                em.transform.position = at;
                float s = Mathf.Clamp(radius / 7f, 0.5f, 2.4f);
                Burst(em, 1.2f, 2.4f, 4f * s, 10f * s, 0.18f * s, 0.4f * s, Color.Lerp(st.Bolt, Color.white, 0.2f), st.Bolt, 0.55f, Mathf.RoundToInt(18 * s), radius * 0.2f);
                Cone(em, 50f, radius * 0.2f, new Vector3(-90f, 0f, 0f));
                FadeOut(em, 1f); ShrinkOverLife(em, 1f, 0.1f);
                em.Play(true);
            }

            if (ultimate) NukeCloud(at, radius, dirt, st);
        }

        // ══════════════════════════════════════════════════════
        //  핵 — 궁극기(§49) 전용 버섯구름
        // ══════════════════════════════════════════════════════
        //
        // 왜 따로 만드는가(오너 지시 2026-09-17 "궁극기가 핵 쏘는 거라니까"):
        // 궁극기는 나이스샷을 여러 번 성공해야 한 번 열리는 **판을 뒤집는 한 방**이다(NiceShot.UltimateCost).
        // 그런데 연출이 일반 폭발을 키운 것뿐이면 **쓴 사람도 맞은 사람도 그게 궁극기였는지 모른다** —
        // 수치만 커지고 화면은 그대로면 그 자원을 모을 이유가 사라진다. 형태가 달라야 한다.
        //
        // 버섯구름의 구조는 실제 핵폭발의 순서를 그대로 따른다:
        //   ① 화구(fireball)   — 지면에서 부풀어 오르는 흰-주황 구
        //   ② 기둥(stem)       — 화구가 빨아올린 먼지가 좁은 기둥으로 솟는다
        //   ③ 갓(cap)          — 꼭대기에서 퍼지며 **바깥쪽이 아래로 말린다**(이게 있어야 버섯이다)
        //   ④ 바닥 파도(base surge) — 지면을 따라 사방으로 깔리는 먼지 고리
        //   ⑤ 응결 고리(condensation ring) — 충격파가 지나간 자리에 생기는 흰 링
        //
        // ⚠️ 연출일 뿐이다. 피해·굴착은 NiceShot.ApplyUltimate 가 정한 수치 그대로다 — 여기서 만지지 마라.
        // ⚠️ 크기를 굴착 반경에 그대로 비례시키지 마라. 굴착 14m 급 궁극기에서 구름이 맵을 덮는다.
        //    제곱근으로 눌러서 "핵은 늘 크지만 화면은 안 가린다"를 지킨다.
        void NukeCloud(Vector3 at, float radius, Color dirt, in BlastStyle st)
        {
            float s = Mathf.Clamp(Mathf.Sqrt(radius / 7f), 0.7f, 1.9f);
            // ⚠️ 높이를 26m 로 뒀더니 **구름이 화면 밖으로 나갔다**(스크린샷으로 확인).
            //    카메라는 착탄점에 붙어 내려다보므로 세로로 긴 연출은 프레임을 벗어난다.
            //    "크게" 가 아니라 "굵고 낮게" 가 이 시점에서 핵으로 읽힌다.
            // ⚠️ 두 번째 실패: 높이는 맞췄는데 **갓이 너무 커서 하늘에 뜬 흙덩어리**로 보였다(스크린샷).
            //    입자 크기 10.5m 에 성장 1.8배 = 폭 19m 짜리 조각이 화면 왼쪽 위를 통째로 덮었다.
            //    버섯으로 읽히려면 갓이 아니라 **기둥과 갓의 비율**이 맞아야 한다 — 갓을 줄이고 기둥을 살렸다.
            // ⚠️ 세 번째: 갓을 줄였더니 이번엔 **기둥이 갓 속에 묻혀** 가로로 퍼진 구름이 됐다.
            //    버섯의 정체는 "가는 기둥 + 그보다 넓은 갓" 이라는 **비율**이다. 갓을 올려 띄우고 기둥을 가늘게 뽑는다.
            //    갓은 기둥이 다 올라간 뒤에 핀다(지연 0.8s) — 동시에 나오면 한 덩어리로 뭉친다.
            float H = 15f * s;                    // 갓이 앉는 높이
            float capR = 5.0f * s;                // 갓 반경
            var hot = Color.Lerp(st.Flash, Color.white, 0.55f);
            // 그을음은 진해야 한다 — 옅으면 지형색에 묻혀 아무것도 안 보인다(첫 시도가 그랬다).
            var soot = new Color(dirt.r * 0.55f + 0.08f, dirt.g * 0.5f + 0.07f, dirt.b * 0.48f + 0.07f, 0.97f);

            // ① 화구 — 짧고 아주 밝게. 첫 0.2초를 이게 지배해야 "핵" 으로 읽힌다.
            {
                var f = Rent("NukeBall", true);
                f.transform.position = at + Vector3.up * (1.2f * s);
                var m = Burst(f, 0.30f, 0.55f, 0.5f * s, 2.2f * s, 3.4f * s, 6.0f * s,
                              Color.white, hot, -0.05f, 26, 1.0f * s);
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                FadeOut(f, 1f); GrowOverLife(f, 0.55f, 1.9f);
                f.Play(true);
            }

            // ② 기둥 — 좁은 원뿔로 위로. 수명이 길어 갓이 뜰 때까지 이어진다.
            {
                var c = Rent("NukeStem", false);
                c.transform.position = at + Vector3.up * (0.5f * s);
                var m = Burst(c, 1.8f, 3.0f, 8f * s, 12f * s, 2.0f * s, 3.2f * s,
                              Color.Lerp(soot, hot, 0.35f), soot, -0.02f, 70, 0.9f * s);
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                Cone(c, 4f, 0.9f * s, new Vector3(-90f, 0f, 0f));      // -90° = +Y 로 뿜는다
                FadeOut(c, 0.95f); GrowOverLife(c, 0.6f, 1.35f);
                var rot = c.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);
                c.Play(true);
            }

            // ③ 갓 — 꼭대기 원에서 바깥으로 퍼지고 **아래로 말린다**. 말림(중력 +)이 없으면 그냥 연기 구름이다.
            {
                var cap = Rent("NukeCap", false);
                cap.transform.position = at + Vector3.up * H;
                var m = Burst(cap, 2.4f, 3.8f, 0f, 0f, 3.4f * s, 5.4f * s,
                              Color.Lerp(soot, hot, 0.22f), soot, 0.05f, 66, 0f);
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                m.startDelay = new ParticleSystem.MinMaxCurve(0.80f, 1.05f);   // 기둥이 먼저 올라간 뒤에 핀다
                var sh = cap.shape;
                sh.shapeType = ParticleSystemShapeType.Donut;
                sh.radius = capR; sh.donutRadius = capR * 0.45f; sh.rotation = new Vector3(-90f, 0f, 0f);
                var vel = cap.velocityOverLifetime;
                vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.radial = new ParticleSystem.MinMaxCurve(2.2f * s, 4.2f * s);   // 바깥으로
                vel.y = new ParticleSystem.MinMaxCurve(0.8f * s, 1.8f * s);        // 살짝 더 솟았다가 중력에 말린다
                FadeOut(cap, 0.95f); GrowOverLife(cap, 0.8f, 1.35f);
                var rot = cap.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
                cap.Play(true);
            }

            // ④ 바닥 파도 — 지면을 따라 낮게 깔린다. 이게 있어야 "지면에서 터졌다"가 읽힌다.
            {
                var b = Rent("NukeSurge", false);
                b.transform.position = at + Vector3.up * (0.4f * s);
                var m = Burst(b, 1.5f, 2.6f, 10f * s, 17f * s, 3.2f * s, 5.4f * s,
                              Color.Lerp(dirt, Color.white, 0.30f), soot, 0.02f, 56, radius * 0.4f);
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                var sh = b.shape;
                sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = radius * 0.5f; sh.radiusThickness = 1f; sh.rotation = new Vector3(-90f, 0f, 0f);
                FadeOut(b, 0.8f); GrowOverLife(b, 0.7f, 2.4f);
                b.Play(true);
            }

            // ⑤ 응결 고리 — 충격파가 지나간 자리. 얇고 빠르게 퍼졌다 사라진다.
            {
                var w = Rent("NukeRing", true);
                w.transform.position = at + Vector3.up * (2.5f * s);
                var m = Burst(w, 0.40f, 0.65f, 26f * s, 34f * s, 1.8f * s, 3.2f * s,
                              Color.white, new Color(1f, 1f, 1f, 0.25f), 0f, 34, 0.5f);
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                var sh = w.shape;
                sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = 1.0f; sh.radiusThickness = 0f; sh.rotation = new Vector3(-90f, 0f, 0f);
                FadeOut(w, 0.9f); GrowOverLife(w, 0.5f, 1.6f);
                w.Play(true);
            }

            // 광원 — 일반 폭발보다 훨씬 밝고 멀리. 핵은 주변을 통째로 한 번 태워 보여야 한다.
            Flash(at + Vector3.up * (2f * s), Color.Lerp(hot, Color.white, 0.4f), 7.5f * s, radius * 5f + 30f);
        }

        ParticleSystem RentBlast()
        {
            foreach (var p in _blastPool)
                if (p != null && !p.IsAlive(true)) return p;

            // 폭발 하나 = 레이어 5개(조사 근거는 머리말). 루트가 Flash 이고 나머지는 자식이다.
            var root = NewSystem($"Blast{_blastPool.Count}", null, true);
            NewSystem("Smoke", root.transform, false);      // child 0
            NewSystem("Debris", root.transform, false);     // child 1
            NewSystem("Shockwave", root.transform, true);   // child 2
            NewSystem("Bolt", root.transform, true);        // child 3
            _blastPool.Add(root);
            return root;
        }

        void ConfigureBlast(ParticleSystem root, float radius, Color dirt, BlastStyle st)
        {
            float s = Mathf.Clamp(radius / 7f, 0.5f, 2.4f);      // 일반탄 7m 을 1 로 본다

            // ── 불꽃: 크고 짧게. 어디서 터졌는지를 첫 프레임에 알린다 ──
            {
                var m = root.main;
                m.duration = 0.5f; m.loop = false; m.playOnAwake = false;
                // 조사 기준: 섬광은 첫 0.05~0.1초에 정점 후 소멸. 길게 끌면 "번쩍"이 사라진다.
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(4f * s, 13f * s);
                // ⚠️ 섬광 크기는 반경에 그대로 비례시키지 않는다 — 굴착 14m 급에서 10m 짜리 흰 입자 25개가 화면을 통째로 덮었다(스크린샷).
                float sf = Mathf.Min(s, 1.5f) * st.FlashMul;
                m.startSize = new ParticleSystem.MinMaxCurve(2.2f * sf, 4.2f * sf);
                m.startColor = new ParticleSystem.MinMaxGradient(st.Flash, st.Flash2);
                m.gravityModifier = 0f;
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                m.maxParticles = 200;

                var e = root.emission; e.enabled = true;
                e.rateOverTime = 0f;
                // 다단 폭발(미사일 3단): 섬광을 시차로 나눠 터뜨린다. 단이 갈수록 작아진다
                var bursts = new ParticleSystem.Burst[Mathf.Max(1, st.Stages)];
                for (int i = 0; i < bursts.Length; i++)
                    bursts[i] = new ParticleSystem.Burst(i * 0.13f, (short)Mathf.Max(1, Mathf.RoundToInt(10 * s * st.FlashMul / (1 + i))));
                e.SetBursts(bursts);
                if (st.Stages > 1) m.duration = 0.5f + st.Stages * 0.13f;

                var sh = root.shape; sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = radius * 0.22f;

                FadeOut(root, 1f);
                ShrinkOverLife(root, 1f, 0.35f);
            }

            var kids = root.transform;
            // ── 연기: 느리게 퍼지며 오래 남는다 ──
            {
                var smoke = kids.GetChild(0).GetComponent<ParticleSystem>();
                var m = smoke.main;
                m.duration = 1.2f; m.loop = false; m.playOnAwake = false;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f * s * st.SmokeLife, 1.5f * s * st.SmokeLife);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * s, 5f * s);
                m.startSize = new ParticleSystem.MinMaxCurve(3.2f * s * Mathf.Sqrt(st.SmokeMul), 6.4f * s * Mathf.Sqrt(st.SmokeMul));
                m.startColor = new ParticleSystem.MinMaxGradient(st.Smoke, Color.Lerp(st.Smoke, new Color(1f, 1f, 1f, st.Smoke.a), 0.3f));
                m.gravityModifier = -st.SmokeRise;             // 살짝 떠오른다(독가스·불은 더)
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                m.maxParticles = 300;

                var e = smoke.emission; e.enabled = true;
                e.rateOverTime = 0f;
                e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(16 * s * st.SmokeMul)) });

                var sh = smoke.shape; sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = radius * 0.3f;

                FadeOut(smoke, 0.5f);
                GrowOverLife(smoke, 1f, 2.1f);
            }

            // ── 흙: 위로 튀어 중력으로 떨어진다 ──
            {
                var d = kids.GetChild(1).GetComponent<ParticleSystem>();
                var m = d.main;
                m.duration = 0.8f; m.loop = false; m.playOnAwake = false;
                var dc = st.UseDirt ? dirt : st.Debris;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f * s, 1.7f * s);
                m.startSpeed = new ParticleSystem.MinMaxCurve(7f * s * (st.Splash ? 1.3f : 1f), 16f * s * (st.Splash ? 1.3f : 1f));
                m.startSize = new ParticleSystem.MinMaxCurve(1.0f * s * (st.Splash ? 0.6f : 1f), 2.4f * s * (st.Splash ? 0.6f : 1f));
                m.startColor = new ParticleSystem.MinMaxGradient(dc, st.Splash ? Color.Lerp(dc, Color.white, 0.4f) : Color.Lerp(dc, Color.black, 0.35f));
                m.gravityModifier = 2.4f;                      // §5 의 g 와 같은 방향 — 눈에 익은 낙하
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                m.maxParticles = 320;

                var e = d.emission; e.enabled = true;
                e.rateOverTime = 0f;
                e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(30 * s * st.DebrisMul)) });

                var sh = d.shape; sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = st.Splash ? 22f : 42f;              // 물은 좁게 높이 솟는다
                sh.radius = radius * 0.25f;
                sh.rotation = new Vector3(-90f, 0f, 0f);       // 위로 뿜는다

                FadeOut(d, 1f);
                ShrinkOverLife(d, 1f, 0.55f);
            }

            // ── 충격파: 지면을 따라 **수평으로** 빠르게 퍼지고 곧 사라진다 ──
            //    폭발의 크기를 한눈에 알리는 레이어다(조사: Shockwave 는 독립 레이어).
            {
                var w = kids.GetChild(2).GetComponent<ParticleSystem>();
                var m = w.main;
                m.duration = 0.4f; m.loop = false; m.playOnAwake = false;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.30f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(18f * s, 30f * s);
                m.startSize = new ParticleSystem.MinMaxCurve(1.6f * s * st.WaveMul, 3.0f * s * st.WaveMul);
                m.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(st.Wave, Color.white, 0.3f), st.Wave);
                m.gravityModifier = 0f;
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                m.maxParticles = 120;

                var e = w.emission; e.enabled = true;
                e.rateOverTime = 0f;
                e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(12 * s * st.WaveMul)) });

                var sh = w.shape; sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Circle;    // 원 둘레로 — 링처럼 퍼진다
                sh.radius = radius * 0.18f;
                sh.radiusThickness = 0f;
                sh.rotation = new Vector3(90f, 0f, 0f);           // 지면에 눕힌다

                FadeOut(w, 0.9f);
                GrowOverLife(w, 0.7f, 1.8f);
            }

            // ── 스파크(Bolt): 작고 밝은 점이 멀리 튄다. 파편보다 빠르고 짧다 ──
            {
                var bolt = kids.GetChild(3).GetComponent<ParticleSystem>();
                var m = bolt.main;
                m.duration = 0.5f; m.loop = false; m.playOnAwake = false;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(16f * s, 34f * s);
                m.startSize = new ParticleSystem.MinMaxCurve(0.35f * s, 0.8f * s);
                m.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(st.Bolt, Color.white, 0.4f), st.Bolt);
                m.gravityModifier = st.Column ? 0.2f : 1.1f;
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                m.maxParticles = 260;

                var e = bolt.emission; e.enabled = true;
                e.rateOverTime = 0f;
                e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(22 * s * st.BoltMul)) });

                var sh = bolt.shape; sh.enabled = true;
                if (st.Column)
                {   // 전기·빔: 좁은 원뿔로 위로 솟는 빛 기둥
                    sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 9f; sh.radius = radius * 0.08f; sh.rotation = new Vector3(-90f, 0f, 0f);
                    m.startSpeed = new ParticleSystem.MinMaxCurve(22f * s, 44f * s);
                    m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                }
                else
                {
                    sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius * 0.12f; sh.rotation = Vector3.zero;
                }

                FadeOut(bolt, 1f);
                ShrinkOverLife(bolt, 1f, 0.2f);
            }
        }

        // ══════════════════════════════════════════════════════
        //  탄 자취 — 탄에 붙어 따라다니는 시스템
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 탄 Transform 에 자취 시스템을 붙인다. 같은 Transform 에 두 번 부르면 **기존 것을 다시 설정**한다 —
        /// 매 발 새로 만들면 시스템이 계속 쌓인다.
        /// </summary>
        public void AttachTrail(Transform shell, in ShellTrail.Style st)
        {
            if (shell == null) return;
            if (!_trails.TryGetValue(shell, out var ps) || ps == null)
            {
                ps = NewSystem("Trail", shell, st.Glow);
                ps.transform.localPosition = Vector3.zero;
                _trails[shell] = ps;
            }
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = st.Glow ? AddMat : AlphaMat;

            var m = ps.main;
            m.loop = true; m.playOnAwake = false;
            m.startLifetime = st.Life;
            m.startSpeed = 0f;                                  // 제자리에 남아야 "자취"가 된다
            m.startSize = new ParticleSystem.MinMaxCurve(st.Size * 1.6f, st.Size * 2.6f);
            m.startColor = st.Color;
            m.gravityModifier = 0f;
            m.simulationSpace = ParticleSystemSimulationSpace.World;   // ⚠️ World 가 아니면 탄을 따라다녀 자취가 안 생긴다
            m.maxParticles = 400;

            var e = ps.emission;
            e.enabled = true;
            // ⚠️ 시간 기준 방출(Interval)이면 80m/s 탄에서 조각이 2m 씩 벌어져 **점선**이 된다(스크린샷).
            //    거리 기준으로 뿜어 속도와 무관하게 이어진 선을 만든다. Interval 은 밀도 계수로만 남긴다.
            e.rateOverTime = 0f;
            e.rateOverDistance = 1f / Mathf.Max(0.12f, st.Size * 0.6f * Mathf.Sqrt(st.Interval / 0.03f));

            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = st.Size * 0.4f;

            // 흐름(연기는 위로, 물방울은 아래로, 빔은 정지)
            var vel = ps.velocityOverLifetime;
            vel.enabled = st.Drift.sqrMagnitude > 1e-4f;
            if (vel.enabled)
            {
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(st.Drift.x);
                vel.y = new ParticleSystem.MinMaxCurve(st.Drift.y);
                vel.z = new ParticleSystem.MinMaxCurve(st.Drift.z);
            }

            FadeOut(ps, st.Glow ? 1f : 0.9f);
            ShrinkOverLife(ps, 1f, st.Glow ? 0.15f : 0.6f);
            ps.Play(true);

            // ── 다층 레이어(ShellTrail.Style 플래그). 자식 시스템은 처음 한 번 만들고 켜고 끈다 ──
            var st2 = st;   // in 매개변수는 람다에서 못 쓴다 — 복사본
            TrailLayer(ps, "Flame", st2.Flame, true, l =>
            {
                // 추진 화염: 탄 **로컬 -z** 로 짧게 뿜는다(탄이 도는 방향을 따라간다). 수명이 짧아 꼬리가 아니라 "불꽃"으로 보인다.
                var m = l.main; m.simulationSpace = ParticleSystemSimulationSpace.Local;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f); m.startSpeed = new ParticleSystem.MinMaxCurve(6f, 10f);
                m.startSize = new ParticleSystem.MinMaxCurve(st2.Size * 1.4f, st2.Size * 2.4f);
                m.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(st2.Flame2, Color.white, 0.4f), st2.Color);
                var e = l.emission; e.rateOverTime = 90f;
                var sh = l.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8f; sh.radius = st2.Size * 0.25f; sh.rotation = new Vector3(180f, 0f, 0f);
                l.transform.localPosition = new Vector3(0f, 0f, -0.5f);
                FadeOut(l, 1f); ShrinkOverLife(l, 1f, 0.2f);
            });
            TrailLayer(ps, "Smoke", st2.Smoke, false, l =>
            {
                var m = l.main; m.simulationSpace = ParticleSystemSimulationSpace.World;
                m.startLifetime = new ParticleSystem.MinMaxCurve(st2.Life * 1.8f, st2.Life * 3.2f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
                m.startSize = new ParticleSystem.MinMaxCurve(st2.Size * 1.6f, st2.Size * 2.6f);
                var c = st2.Flame2; if (c.a > 0.9f) c.a = 0.35f;
                m.startColor = new ParticleSystem.MinMaxGradient(c, new Color(c.r, c.g, c.b, c.a * 0.7f));
                m.gravityModifier = -0.03f;
                var e = l.emission; e.rateOverTime = 0f; e.rateOverDistance = 1.2f / Mathf.Max(0.2f, st2.Size);
                var sh = l.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = st2.Size * 0.5f;
                FadeOut(l, 0.5f); GrowOverLife(l, 0.5f, 3.2f);
                var rot = l.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
            });
            TrailLayer(ps, "Sparks", st2.Sparks, true, l =>
            {
                var m = l.main; m.simulationSpace = ParticleSystemSimulationSpace.World;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f); m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
                m.startSize = new ParticleSystem.MinMaxCurve(st2.Size * 0.25f, st2.Size * 0.5f);
                m.startColor = new ParticleSystem.MinMaxGradient(Color.white, st2.Color);
                m.gravityModifier = 0.3f;
                var e = l.emission; e.rateOverTime = 60f;
                var sh = l.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = st2.Size * 0.6f;
                FadeOut(l, 1f); ShrinkOverLife(l, 1f, 0.1f);
            });
            TrailLayer(ps, "Ring", st2.Ring, true, l =>
            {
                // 에너지 링: 탄 주위를 도는 입자(로컬 공간, 궤도 속도). 빔·바람칼이 "에너지"로 읽힌다.
                var m = l.main; m.simulationSpace = ParticleSystemSimulationSpace.Local;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f); m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(st2.Size * 0.5f, st2.Size * 0.8f);
                m.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(st2.Color, Color.white, 0.5f), st2.Color);
                var e = l.emission; e.rateOverTime = 70f;
                var sh = l.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = st2.Size * 2.2f; sh.radiusThickness = 0f; sh.rotation = Vector3.zero;
                var v = l.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local; v.orbitalZ = new ParticleSystem.MinMaxCurve(12f);
                FadeOut(l, 1f);
            });
        }

        /// <summary>자취의 자식 레이어를 켜거나 끈다. 없으면 만들고 설정한다 — 같은 탄에 매 발 새로 만들지 않는다.</summary>
        void TrailLayer(ParticleSystem parent, string name, bool on, bool additive, System.Action<ParticleSystem> cfg)
        {
            ParticleSystem l = null;
            foreach (Transform t in parent.transform) if (t.name == name) { l = t.GetComponent<ParticleSystem>(); break; }
            if (!on) { if (l != null && l.isPlaying) l.Stop(true, ParticleSystemStopBehavior.StopEmitting); return; }
            if (l == null)
            {
                l = NewSystem(name, parent.transform, additive);
                var m = l.main; m.loop = true; m.playOnAwake = false; m.maxParticles = 300; m.gravityModifier = 0f;
                var e = l.emission; e.enabled = true; e.rateOverDistance = 0f;
                var sh = l.shape; sh.enabled = true;
            }
            l.GetComponent<ParticleSystemRenderer>().sharedMaterial = additive ? AddMat : AlphaMat;
            cfg(l);
            if (!l.isPlaying) l.Play(true);
        }

        public void StopTrail(Transform shell)
        {
            if (shell == null) return;
            if (_trails.TryGetValue(shell, out var ps) && ps != null)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);   // 남은 입자(자식 포함)는 수명대로 사라지게 둔다
        }

        // ── 수명 곡선 헬퍼 ──────────────────────────────────────
        static void FadeOut(ParticleSystem ps, float startAlpha)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(startAlpha * 0.85f, 0.35f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        static void GrowOverLife(ParticleSystem ps, float from, float to)
        {
            var s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
        }

        static void ShrinkOverLife(ParticleSystem ps, float from, float to)
            => GrowOverLife(ps, from, to);


        // ══════════════════════════════════════════════════════
        //  발사 · 피격 · 격파 · 아이템 · 상태 · 설치물 · 위성 빔 · 눈
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 미사일 **초기 유도·자세 제어**의 연출(오너 지시 2026-09-17). 자세 값은 전부 Sim/Guidance 가 준다 —
        /// 여기서는 그리기만 한다(그래서 유니티 없이 verify.sh guide 로 검증된다).
        ///
        ///   · TVC  — 자취의 "Flame" 레이어(노즐 화염)를 **짐벌처럼 꺾는다**. 배기가 옆으로 나가는 게 눈에 보여야
        ///            "저게 힘으로 대가리를 돌리는 중" 으로 읽힌다. 화염을 탄 축에 고정해두면 아무 일도 안 일어나 보인다.
        ///   · DACS — 몸통 옆 소형 로켓. 짧고 센 퍼프를 **옆으로** 뿜는다. 펄스라 끊겨야 한다(연속이면 그냥 연기다).
        ///   · INS/GPS 보정 — 중간 유도 진입 순간의 청백색 링 한 번.
        ///
        /// ⚠️ 매 프레임 불린다. 새 오브젝트를 만들지 말고 자취의 자식·풀만 건드려야 한다.
        /// </summary>
        public void Guidance(Transform shell, in Attitude att, in ShellTrail.Style st)
        {
            if (shell == null) return;

            // ── TVC: 노즐 화염을 배기 방향으로 돌린다 ──
            if (_trails.TryGetValue(shell, out var trail) && trail != null)
                foreach (Transform t in trail.transform)
                    if (t.name == "Flame")
                    {
                        var local = shell.InverseTransformDirection(new Vector3(att.Exhaust.X, att.Exhaust.Y, att.Exhaust.Z));
                        // Flame 은 로컬 -z 로 뿜도록 설정돼 있다(AttachTrail) — 그 축을 배기 방향에 맞춘다.
                        if (local.sqrMagnitude > 1e-6f)
                            t.localRotation = Quaternion.FromToRotation(Vector3.back, local.normalized);
                        break;
                    }

            // ── DACS: 측추력기 펄스 ──
            if (att.Dacs > 0.35f && att.DacsDir.LengthSq > 0.5f)
            {
                _dacsNext.TryGetValue(shell, out float next);
                if (Time.time >= next)
                {
                    _dacsNext[shell] = Time.time + 0.045f;
                    var dir = new Vector3(att.DacsDir.X, att.DacsDir.Y, att.DacsDir.Z).normalized;
                    var nose = new Vector3(att.Nose.X, att.Nose.Y, att.Nose.Z).normalized;
                    var p = Rent("Dacs", true);
                    // 분사구는 탄두 쪽(무게중심 앞) — 거기서 옆으로 밀어야 대가리가 돌아간다.
                    p.transform.position = shell.position + nose * (ShellNoseOffset * shell.localScale.x);
                    var m = Burst(p, 0.06f, 0.14f, 7f, 13f, st.Size * 0.30f, st.Size * 0.55f,
                                  Color.white, new Color(0.85f, 0.92f, 1f, 0.9f), 0f,
                                  Mathf.RoundToInt(4f + att.Dacs * 5f), 0.04f);
                    m.simulationSpace = ParticleSystemSimulationSpace.World;
                    Cone(p, 16f, 0.05f, Quaternion.LookRotation(dir).eulerAngles);
                    FadeOut(p, 1f); ShrinkOverLife(p, 1f, 0.1f);
                    p.Play(true);
                }
            }

            // ── INS/GPS 보정: 중간 유도 진입 ──
            if (att.InsFix)
            {
                var r = Rent("InsFix", true);
                r.transform.position = shell.position;
                var m = Burst(r, 0.16f, 0.26f, 5f, 7f, st.Size * 0.4f, st.Size * 0.7f,
                              new Color(0.7f, 0.95f, 1f), new Color(0.35f, 0.7f, 1f, 0.8f), 0f, 16, 0.05f);
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                var sh = r.shape;
                sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.12f; sh.radiusThickness = 0f;
                sh.rotation = Quaternion.LookRotation(new Vector3(att.Nose.X, att.Nose.Y, att.Nose.Z)).eulerAngles;
                FadeOut(r, 1f); GrowOverLife(r, 0.6f, 1.6f);
                r.Play(true);
            }
        }

        /// <summary>탄 중심에서 탄두(측추력기 분사구)까지의 거리(m). 탄 메시 길이에 맞춘 값이다.</summary>
        const float ShellNoseOffset = 0.45f;

        /// <summary>자세 연출 상태를 버린다 — 착탄 때 불러 다음 발이 남은 짐벌 각을 물려받지 않게 한다.</summary>
        public void StopGuidance(Transform shell)
        {
            if (shell == null) return;
            _dacsNext.Remove(shell);
            if (_trails.TryGetValue(shell, out var trail) && trail != null)
                foreach (Transform t in trail.transform)
                    if (t.name == "Flame") { t.localRotation = Quaternion.identity; break; }
        }

        /// <summary>발사 섬광 — 포구에서 앞으로 뿜는 짧은 불꽃 + 연기 퍼프 + 광원. 색은 그 기종 폭발 섬광색.</summary>
        public void MuzzleFlash(Vector3 at, Vector3 dir, TankKind kind, ShellKind shell)
        {
            var st = BlastStyle.Of(kind, shell);
            var euler = Quaternion.LookRotation(dir).eulerAngles;
            var f = Rent("Muzzle", true);
            f.transform.position = at;
            Burst(f, 0.06f, 0.14f, 6f, 14f, 0.9f, 1.8f, Color.Lerp(st.Flash, Color.white, 0.3f), st.Flash2, 0f, 10, 0.15f);
            Cone(f, 18f, 0.15f, euler);
            FadeOut(f, 1f); ShrinkOverLife(f, 1f, 0.3f); f.Play(true);

            var sm = Rent("MuzzleSmoke", false);
            sm.transform.position = at;
            Burst(sm, 0.5f, 1.1f, 2f, 5f, 0.6f, 1.2f, new Color(0.75f, 0.73f, 0.7f, 0.35f), new Color(0.9f, 0.9f, 0.9f, 0.3f), -0.05f, 8, 0.12f);
            Cone(sm, 22f, 0.12f, euler);
            FadeOut(sm, 0.5f); GrowOverLife(sm, 0.6f, 2.2f); sm.Play(true);

            Flash(at, st.Flash, 2.5f, 9f);
        }

        /// <summary>피격 — 맞은 자리에서 스파크가 튀고 작은 연기. 직격이면 더 크고 금속 조각이 더 튄다.</summary>
        public void Hit(Vector3 at, bool direct)
        {
            float k = direct ? 1.6f : 1f;
            var sp = Rent("HitSpark", true);
            sp.transform.position = at;
            Burst(sp, 0.25f, 0.5f, 6f * k, 14f * k, 0.15f, 0.35f, new Color(1f, 0.95f, 0.75f), new Color(1f, 0.6f, 0.25f), 1.4f, Mathf.RoundToInt(16 * k), 0.4f);
            FadeOut(sp, 1f); ShrinkOverLife(sp, 1f, 0.2f); sp.Play(true);

            var sm = Rent("HitSmoke", false);
            sm.transform.position = at;
            Burst(sm, 0.5f, 0.9f, 1f, 3f, 0.7f * k, 1.4f * k, new Color(0.3f, 0.29f, 0.3f, 0.5f), new Color(0.55f, 0.53f, 0.52f, 0.4f), -0.08f, Mathf.RoundToInt(6 * k), 0.5f);
            FadeOut(sm, 0.5f); GrowOverLife(sm, 0.7f, 1.8f); sm.Play(true);
            if (direct) Flash(at, new Color(1f, 0.75f, 0.4f), 3f, 7f);
        }

        /// <summary>
        /// 주행 먼지 — 바퀴 밑에서 낮게 피어오른다. 탱크가 "미끄러지지" 않게 하는 두 축 중 하나
        /// (다른 하나는 실제로 도는 바퀴 = TankDrive).
        ///
        /// ⚠️ 매 프레임 부르는 경로다. 그래서 **호출부가 간격을 재지 않는다** — 여기서 스스로 솎는다
        ///    (프레임레이트가 달라지면 먼지 양이 달라지는 걸 호출부마다 막게 하면 반드시 한 곳이 빠진다).
        /// </summary>
        public void DriveDust(Vector3 at, Color dirt, float strength01)
        {
            if (Time.time < _dustNext) return;
            _dustNext = Time.time + 0.055f;                 // 초당 ~18회 — 이보다 잦으면 먼지가 벽이 된다

            float k = Mathf.Clamp01(strength01);
            var d = Rent("DriveDust", false);
            d.transform.position = at;
            var c0 = new Color(dirt.r, dirt.g, dirt.b, 0.36f);
            var c1 = new Color(dirt.r * 1.15f, dirt.g * 1.12f, dirt.b * 1.08f, 0.18f);
            // 위로 조금, 옆으로 조금. 오래 남지 않는다(0.5초) — 지나간 자리에 먼지가 쌓여 보이면 안 된다.
            Burst(d, 0.30f, 0.55f, 0.6f, 1.7f, 0.45f + k * 0.35f, 0.9f + k * 0.5f, c0, c1, -0.05f, 2, 0.35f);
            FadeOut(d, 0.45f); GrowOverLife(d, 0.8f, 1.9f); d.Play(true);
        }
        float _dustNext;

        /// <summary>실드가 공격을 막았다 — 푸른 구 껍질이 한 번 번쩍이고 링이 퍼진다.</summary>
        public void ShieldBlock(Vector3 at)
        {
            var sh = Rent("Shield", true);
            sh.transform.position = at;
            Burst(sh, 0.3f, 0.5f, 0f, 0.5f, 0.5f, 0.8f, new Color(0.55f, 0.8f, 1f), new Color(0.3f, 0.55f, 1f), 0f, 60, 2.2f);
            var shape = sh.shape; shape.radiusThickness = 0f;      // 껍질만
            FadeOut(sh, 1f); GrowOverLife(sh, 0.6f, 1.4f); sh.Play(true);

            var ring = Rent("ShieldRing", true);
            ring.transform.position = at;
            Burst(ring, 0.25f, 0.4f, 10f, 14f, 0.8f, 1.2f, Color.white, new Color(0.5f, 0.75f, 1f), 0f, 24, 0.3f);
            var rs = ring.shape; rs.shapeType = ParticleSystemShapeType.Circle; rs.radiusThickness = 0f; rs.rotation = new Vector3(90f, 0f, 0f);
            FadeOut(ring, 1f); ring.Play(true);
            Flash(at, new Color(0.5f, 0.75f, 1f), 3.5f, 10f);
        }

        /// <summary>격파 — 큰 화구·파편·검은 연기 기둥(8초)·불씨. 탱크가 사라진 자리가 "여기서 죽었다"로 남는다.</summary>
        public void Death(Vector3 at, Color bodyColor)
        {
            var fb = Rent("DeathFire", true);
            fb.transform.position = at + Vector3.up * 0.8f;
            Burst(fb, 0.25f, 0.6f, 3f, 9f, 2.5f, 4.5f, new Color(1f, 0.9f, 0.5f), new Color(1f, 0.4f, 0.1f), -0.3f, 22, 1.2f);
            FadeOut(fb, 1f); ShrinkOverLife(fb, 1f, 0.4f); fb.Play(true);

            var deb = Rent("DeathDebris", false);
            deb.transform.position = at + Vector3.up * 0.8f;
            Burst(deb, 1f, 2f, 8f, 18f, 0.3f, 0.7f, Color.Lerp(bodyColor, Color.black, 0.3f), bodyColor, 2.2f, 34, 0.8f);
            Cone(deb, 55f, 0.8f, new Vector3(-90f, 0f, 0f));
            FadeOut(deb, 1f); deb.Play(true);

            var em = Rent("Ember", true);
            em.transform.position = at + Vector3.up * 0.5f;
            Burst(em, 1.5f, 3f, 4f, 11f, 0.2f, 0.45f, new Color(1f, 0.85f, 0.5f), new Color(1f, 0.5f, 0.2f), 0.5f, 40, 0.6f);
            Cone(em, 60f, 0.6f, new Vector3(-90f, 0f, 0f));
            FadeOut(em, 1f); ShrinkOverLife(em, 1f, 0.1f); em.Play(true);

            // 연기 기둥: 8초 동안 계속 나오다 멈춘다(루프 시스템 + duration)
            var col = NewSystem("Wreck", null, false);
            col.transform.position = at + Vector3.up * 0.6f;
            var m = col.main;
            m.duration = 8f; m.loop = false; m.playOnAwake = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.5f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            m.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.15f, 0.16f, 0.6f), new Color(0.4f, 0.38f, 0.37f, 0.45f));
            m.gravityModifier = -0.12f;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 200;
            var e = col.emission; e.enabled = true; e.rateOverTime = 14f;
            var sh = col.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 0.6f; sh.rotation = new Vector3(-90f, 0f, 0f);
            FadeOut(col, 0.6f); GrowOverLife(col, 0.6f, 2.6f);
            var rot = col.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            col.Play(true);
            _wreckLoops.Add(col);
            for (int i = _wreckLoops.Count - 1; i >= 0; i--)          // 다 꺼진 기둥은 지운다
                if (_wreckLoops[i] == null || (!_wreckLoops[i].IsAlive(true) && _wreckLoops[i] != col)) { if (_wreckLoops[i] != null) Destroy(_wreckLoops[i].gameObject); _wreckLoops.RemoveAt(i); }

            Flash(at, new Color(1f, 0.6f, 0.3f), 8f, 22f);
            Shake = Mathf.Min(Shake + 0.5f, 1.1f);
        }

        /// <summary>아이템 사용 — 종류별 모양. 0 상승 반짝(회복) · 1 구 껍질(실드) · 2 지면 링(파워/이동) · 3 소용돌이(텔레포트/눈) · 4 이중 섬광(더블파이어)</summary>
        public void ItemBurst(Vector3 at, Color color, int style)
        {
            var ps = Rent("Item", true);
            ps.transform.position = at + Vector3.up * 0.6f;
            switch (style)
            {
                case 0:
                    Burst(ps, 0.8f, 1.4f, 1.5f, 3.5f, 0.25f, 0.5f, Color.Lerp(color, Color.white, 0.4f), color, -0.35f, 40, 1.4f);
                    Cone(ps, 30f, 1.4f, new Vector3(-90f, 0f, 0f));
                    FadeOut(ps, 1f); ShrinkOverLife(ps, 1f, 0.2f);
                    break;
                case 1:
                    Burst(ps, 0.6f, 0.9f, 0f, 0.3f, 0.35f, 0.6f, Color.Lerp(color, Color.white, 0.3f), color, 0f, 70, 2.0f);
                    { var sh = ps.shape; sh.radiusThickness = 0f; }
                    FadeOut(ps, 1f); GrowOverLife(ps, 0.5f, 1.3f);
                    break;
                case 2:
                    Burst(ps, 0.4f, 0.7f, 6f, 10f, 0.5f, 0.9f, Color.white, color, 0f, 36, 0.4f);
                    { var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radiusThickness = 0f; sh.rotation = new Vector3(90f, 0f, 0f); }
                    FadeOut(ps, 1f); ShrinkOverLife(ps, 1f, 0.3f);
                    break;
                case 3:
                    Burst(ps, 0.7f, 1.2f, 0.5f, 1.5f, 0.3f, 0.6f, Color.Lerp(color, Color.white, 0.4f), color, -0.5f, 60, 1.6f);
                    { var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local; v.orbitalY = new ParticleSystem.MinMaxCurve(4f); }
                    FadeOut(ps, 1f); ShrinkOverLife(ps, 1f, 0.2f);
                    break;
                default:
                    Burst(ps, 0.15f, 0.3f, 2f, 6f, 1.2f, 2.2f, Color.white, color, 0f, 14, 0.8f);
                    FadeOut(ps, 1f); ShrinkOverLife(ps, 1f, 0.3f);
                    break;
            }
            ps.Play(true);
            Flash(at, color, 3f, 9f);
        }

        /// <summary>
        /// 유닛 상태 루프 — 독(녹색 방울이 위로 스멀스멀) · 속박(보라 링이 발밑에서 돈다).
        /// 매 턴 호출해 켜고 끈다. 같은 유닛에 두 번 켜도 시스템은 하나다.
        /// </summary>
        public void SetUnitStatus(int id, Transform root, bool poison, bool rooted)
        {
            Loop(_poisonLoops, id, root, poison, ps =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f); m.gravityModifier = -0.25f;
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.95f, 0.35f, 0.8f), new Color(0.3f, 0.75f, 0.25f, 0.7f));
                var e = ps.emission; e.rateOverTime = 14f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 1.4f; sh.rotation = Vector3.zero;
                ps.transform.localPosition = Vector3.up * 1.2f;
                FadeOut(ps, 1f); ShrinkOverLife(ps, 1f, 0.2f);
            }, true);
            Loop(_rootLoops, id, root, rooted, ps =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f); m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.55f); m.gravityModifier = 0f;
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.5f, 1f), new Color(0.45f, 0.3f, 0.9f));
                var e = ps.emission; e.rateOverTime = 30f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 2.4f; sh.radiusThickness = 0f; sh.rotation = new Vector3(90f, 0f, 0f);
                var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local; v.orbitalY = new ParticleSystem.MinMaxCurve(3f);
                ps.transform.localPosition = Vector3.up * 0.3f;
                FadeOut(ps, 1f);
            }, true);
        }

        void Loop(Dictionary<int, ParticleSystem> map, int key, Transform parent, bool on, System.Action<ParticleSystem> cfg, bool additive)
        {
            map.TryGetValue(key, out var ps);
            if (!on)
            {
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }
            if (ps == null)
            {
                ps = NewSystem("Loop", parent, additive);
                var m = ps.main; m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 200;
                var e = ps.emission; e.enabled = true; e.rateOverDistance = 0f;
                var sh = ps.shape; sh.enabled = true;
                cfg(ps);
                map[key] = ps;
            }
            else if (ps.transform.parent != parent) { ps.transform.SetParent(parent, false); }
            if (!ps.isPlaying) ps.Play(true);
        }

        /// <summary>
        /// 설치물 스냅샷을 루프 파티클로 맞춘다. 지속불 = 불꽃이 위로 일렁이고 불씨가 튄다 · 독구름 = 녹색 안개가 낮게 깔려 돈다.
        /// 지뢰는 여기서 안 다룬다(작은 공 메시 + 깜빡이는 점은 게임 쪽). 크레이터가 사발이라도 불꽃은 **위로 오르니** 어디서나 보인다.
        /// </summary>
        public void SetFields(List<HazardField.HazardView> views)
        {
            int n = 0;
            foreach (var h in views)
            {
                if (h.Kind == 0) continue;
                int key = n++;
                bool cloud = h.Kind == 2;
                _fieldLoops.TryGetValue(key, out var ps);
                if (ps == null)
                {
                    ps = NewSystem("Field", null, !cloud);
                    var m = ps.main; m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 600;
                    var e = ps.emission; e.enabled = true; e.rateOverDistance = 0f;
                    var sh = ps.shape; sh.enabled = true;
                    _fieldLoops[key] = ps;
                }
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = cloud ? AlphaMat : AddMat;
                // ⚠️ 크레이터는 사발이라 중앙 지면 높이에서 가장자리로 뿜으면 입자가 사발 벽 **안**에서 태어나 안 보인다(스크린샷).
                //    반경의 1/3 만큼 띄우고 안쪽 70% 에서만 뿜는다 — 어디서 봐도 불꽃·안개가 사발 위로 올라온다.
                ps.transform.position = new Vector3(h.X, h.Y + 0.3f + h.Radius * 0.33f, h.Z);
                {
                    var m = ps.main;
                    var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = h.Radius * 0.7f; sh.radiusThickness = 1f; sh.rotation = new Vector3(90f, 0f, 0f);
                    var e = ps.emission;
                    var v = ps.velocityOverLifetime;
                    if (cloud)
                    {
                        m.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
                        m.startSize = new ParticleSystem.MinMaxCurve(2.2f, 3.6f); m.gravityModifier = -0.02f;
                        m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.5f, 0.9f, 0.3f, 0.45f), new Color(0.35f, 0.7f, 0.25f, 0.4f));
                        e.rateOverTime = h.Radius * 3.5f;
                        v.enabled = true; v.space = ParticleSystemSimulationSpace.Local; v.orbitalY = new ParticleSystem.MinMaxCurve(0.35f); v.x = 0f; v.y = new ParticleSystem.MinMaxCurve(0.15f); v.z = 0f;
                        FadeOut(ps, 0.5f); GrowOverLife(ps, 0.6f, 1.5f);
                        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
                    }
                    else
                    {
                        m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f); m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
                        m.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.6f); m.gravityModifier = -0.35f;
                        m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.35f), new Color(1f, 0.4f, 0.1f));
                        e.rateOverTime = h.Radius * 9f;
                        v.enabled = true; v.space = ParticleSystemSimulationSpace.World; v.x = 0f; v.z = 0f; v.y = new ParticleSystem.MinMaxCurve(3.5f, 6.5f); v.orbitalY = 0f;
                        FadeOut(ps, 1f); ShrinkOverLife(ps, 1f, 0.15f);
                        var rot = ps.rotationOverLifetime; rot.enabled = false;
                    }
                }
                if (!ps.isPlaying) ps.Play(true);
            }
            foreach (var kv in _fieldLoops)
                if (kv.Key >= n && kv.Value != null && kv.Value.isPlaying) kv.Value.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>지뢰 표식 — 깜빡이는 붉은 점(설치 직후 한 번, 이후 루프는 게임이 안 돈다). 설치 순간을 알린다.</summary>
        public void MinePlaced(Vector3 at)
        {
            var ps = Rent("Mine", true);
            ps.transform.position = at + Vector3.up * 0.5f;
            Burst(ps, 0.6f, 1.0f, 0.2f, 1.2f, 0.25f, 0.45f, new Color(1f, 0.35f, 0.3f), new Color(1f, 0.15f, 0.1f), -0.1f, 16, 0.6f);
            FadeOut(ps, 1f); ps.Play(true);
        }

        /// <summary>위성 빔 — 하늘에서 착탄점까지 청백 기둥이 꽂히고, 바닥에서 링과 스파크가 튄다(원작 "위성 레이저").</summary>
        public void SatelliteBeam(Vector3 at, float height)
        {
            var beam = Rent("SatBeam", true);
            beam.transform.position = at + Vector3.up * (height * 0.5f);
            Burst(beam, 0.55f, 0.9f, 0f, 0f, 1.6f, 2.6f, new Color(0.75f, 0.97f, 1f), new Color(0.35f, 0.8f, 1f), 0f, 90, 0.5f);
            { var sh = beam.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(0.7f, height, 0.7f); }
            FadeOut(beam, 1f); ShrinkOverLife(beam, 1f, 0.2f); beam.Play(true);

            var fall = Rent("SatFall", true);
            fall.transform.position = at + Vector3.up * height;
            Burst(fall, 0.5f, 0.8f, height * 1.3f, height * 1.8f, 0.4f, 0.8f, Color.white, new Color(0.5f, 0.85f, 1f), 0f, 40, 0.4f);
            Cone(fall, 3f, 0.3f, new Vector3(90f, 0f, 0f));        // 아래로
            FadeOut(fall, 1f); fall.Play(true);

            var ring = Rent("SatRing", true);
            ring.transform.position = at;
            Burst(ring, 0.3f, 0.5f, 14f, 22f, 1.2f, 2f, Color.white, new Color(0.45f, 0.85f, 1f), 0f, 40, 0.5f);
            { var sh = ring.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radiusThickness = 0f; sh.rotation = new Vector3(90f, 0f, 0f); }
            FadeOut(ring, 1f); GrowOverLife(ring, 0.6f, 1.6f); ring.Play(true);
            Flash(at, new Color(0.5f, 0.85f, 1f), 7f, 26f);
        }

        /// <summary>눈 날씨 — 카메라를 따라다니는 상자에서 눈송이가 바람 방향으로 흩날린다.</summary>
        public void SetSnow(bool on, Transform follow, Vector2 wind)
        {
            _snowFollow = follow;
            if (!on) { if (_snow != null && _snow.isPlaying) _snow.Stop(true, ParticleSystemStopBehavior.StopEmitting); return; }
            if (_snow == null)
            {
                _snow = NewSystem("Snow", null, false);
                var m = _snow.main; m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 2500;
                m.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f); m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.3f); m.gravityModifier = 0.09f;
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.9f), new Color(0.9f, 0.95f, 1f, 0.7f));
                var e = _snow.emission; e.enabled = true; e.rateOverTime = 320f;
                var sh = _snow.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(110f, 2f, 110f);
                var n = _snow.noise; n.enabled = true; n.strength = 0.8f; n.frequency = 0.25f; n.scrollSpeed = 0.3f;
                FadeOut(_snow, 0.9f);
            }
            var v = _snow.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
            v.x = new ParticleSystem.MinMaxCurve(wind.x * 0.25f); v.y = new ParticleSystem.MinMaxCurve(-1.6f); v.z = new ParticleSystem.MinMaxCurve(wind.y * 0.25f);
            if (follow != null) _snow.transform.position = follow.position + Vector3.up * 28f;
            if (!_snow.isPlaying) _snow.Play(true);
        }

        public void Clear()
        {
            foreach (var p in _blastPool) if (p != null) p.Clear(true);
            foreach (var kv in _trails) if (kv.Value != null) kv.Value.Clear(true);
            foreach (var kv in _pools) foreach (var p in kv.Value) if (p != null) p.Clear(true);
            foreach (var kv in _fieldLoops) if (kv.Value != null) kv.Value.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var kv in _poisonLoops) if (kv.Value != null) kv.Value.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var kv in _rootLoops) if (kv.Value != null) kv.Value.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var w in _wreckLoops) if (w != null) Destroy(w.gameObject);
            _wreckLoops.Clear();
            foreach (var (l, _, _) in _lights) if (l != null) l.enabled = false;
            _lights.Clear();
            Shake = 0f;
        }
    }
}
