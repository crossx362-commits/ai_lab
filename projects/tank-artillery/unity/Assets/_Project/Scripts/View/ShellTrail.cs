// 탄 비행 흔적 — 기종·탄종마다 다른 자취를 남긴다.
//
// 왜 필요한가(오너 지시 2026-09-17): 탄이 아무것도 안 남기고 날아가면 **돌덩이가 지나가는 것**과 같다.
// 원작에서는 날아오는 모습만 보고 무엇이 오는지 알 수 있었다 — 그 정보가 대응(이동할지, 버틸지)을 만든다.
// 모양(ProceduralTank.Shell)과 색(ShellColor)에 이어 **자취**가 세 번째 식별 채널이다.
//
// 근거는 §2-9 원작 조사의 기종 성격이다: 캐논=포탄(연기), 미사일=추진(화염 배기),
// 레이저=빔(빛줄기), 이온=위성 에너지(전기), 듀크=독(녹색 가스), 포세이돈=물, 마인랜더=지뢰(거의 무연).
//
// ⚠️ 연출일 뿐이다 — 탄도(§5)·판정에 아무 영향이 없다. 여기서 속도나 위치를 만지지 마라.

using UnityEngine;
using Tankfall.Sim;

namespace Tankfall.View
{
    public static class ShellTrail
    {
        public struct Style
        {
            public Color Color;
            public float Size;       // 조각 크기(m)
            public float Life;       // 수명(초)
            public float Interval;   // 남기는 간격(초) — 짧을수록 촘촘한 선이 된다
            public Vector3 Drift;    // 남은 뒤 흐르는 방향(연기는 위로, 빔은 정지)
            public bool Glow;        // true=조명 안 받는 발광체(빔·전기), false=조명 받는 연기·먼지
            public bool Cube;        // true=각진 조각(파편·얼음), false=둥근 조각(연기)
            // ── 다층 자취(오너 지시 2026-09-17 "날아가는 이펙트도 더 디테일하게") — 기본 점선 위에 얹는 레이어 ──
            public bool Flame;       // 추진 화염: 탄 뒤로 짧게 뿜는 주황 불꽃(미사일 계열)
            public bool Smoke;       // 연기: 커지며 흐려지는 회색/유색 퍼프(포탄·투석·독)
            public bool Sparks;      // 스파크: 작은 점이 옆으로 튀어 흩어진다(전기·빔)
            public bool Ring;        // 에너지 링: 탄 주위를 도는 입자(레이저·세크윈드)
            public Color Flame2;     // 화염/연기 보조색
        }

        public static Style Of(TankKind kind, ShellKind shell, bool ultimate)
        {
            Style s;
            switch (kind)
            {
                case TankKind.Cannon:        // 포탄 — 회색 연기가 뒤로 퍼진다
                    s = Mk(new Color(0.62f, 0.60f, 0.58f), 0.42f, 0.55f, 0.030f, Vector3.up * 1.2f, false, false); break;

                case TankKind.Missile:
                case TankKind.MultiMissile:  // 추진 — 주황 화염 + 흰 연기(번갈아 보이도록 밝게)
                    s = Mk(new Color(1.00f, 0.62f, 0.22f), 0.34f, 0.40f, 0.022f, Vector3.up * 0.8f, true, false); break;

                case TankKind.Laser:         // 빔 — 가늘고 촘촘한 빛줄기, 흐르지 않는다
                    s = Mk(new Color(1.00f, 0.32f, 0.36f), 0.20f, 0.28f, 0.012f, Vector3.zero, true, true); break;

                case TankKind.IonAttacker:   // 위성 에너지 — 청백색 전기 조각
                    s = Mk(new Color(0.55f, 0.85f, 1.00f), 0.28f, 0.34f, 0.018f, Vector3.up * 0.4f, true, true); break;

                case TankKind.Duke:          // 독 — 녹색 가스가 퍼진다
                    s = Mk(new Color(0.42f, 0.78f, 0.34f), 0.46f, 0.80f, 0.030f, Vector3.up * 1.6f, false, false); break;

                case TankKind.Poseidon:      // 물 — 푸른 물방울이 아래로 떨어진다
                    s = Mk(new Color(0.45f, 0.72f, 0.95f), 0.26f, 0.45f, 0.022f, Vector3.down * 2.2f, false, true); break;

                case TankKind.MineLander:    // 지뢰 — 거의 무연. 어두운 점만 드문드문
                    s = Mk(new Color(0.28f, 0.27f, 0.30f), 0.22f, 0.30f, 0.060f, Vector3.zero, false, true); break;

                case TankKind.Catapult:      // 투석 — 흙먼지
                    s = Mk(new Color(0.66f, 0.56f, 0.40f), 0.38f, 0.60f, 0.040f, Vector3.up * 0.6f, false, false); break;

                case TankKind.CrossBow:      // 화살 — 아주 얇은 흰 자취
                    s = Mk(new Color(0.88f, 0.88f, 0.84f), 0.16f, 0.26f, 0.016f, Vector3.zero, false, true); break;

                case TankKind.Carrot:        // 삼연포탄 — 주황 퍼프
                    s = Mk(new Color(0.95f, 0.58f, 0.25f), 0.34f, 0.45f, 0.028f, Vector3.up * 0.9f, false, false); break;

                case TankKind.SecWind:       // 바람 — 옅은 청록 소용돌이가 옆으로 흐른다
                    s = Mk(new Color(0.66f, 0.92f, 0.86f), 0.32f, 0.50f, 0.024f, Vector3.up * 0.3f, true, false); break;

                case TankKind.SuperTank:     // 유도탄 — 푸른 배기
                    s = Mk(new Color(0.50f, 0.62f, 0.95f), 0.30f, 0.42f, 0.022f, Vector3.up * 0.7f, true, false); break;

                default:                     // 기본 — 옅은 연기
                    s = Mk(new Color(0.70f, 0.70f, 0.70f), 0.30f, 0.40f, 0.035f, Vector3.up * 0.8f, false, false); break;
            }

            // 레이어 배정 — 기종 성격(§2-9 조사)대로: 추진체=화염+연기, 포탄류=연기, 빔/전기=스파크+링, 독=진한 연기, 물=안개
            switch (kind)
            {
                case TankKind.Missile:
                case TankKind.MultiMissile:
                case TankKind.SuperTank:    s.Flame = true; s.Smoke = true; s.Flame2 = kind == TankKind.SuperTank ? new Color(0.5f, 0.7f, 1f) : new Color(1f, 0.85f, 0.4f); break;
                case TankKind.Laser:
                case TankKind.IonAttacker:  s.Sparks = true; s.Ring = true; s.Flame2 = s.Color; break;
                case TankKind.SecWind:      s.Ring = true; s.Smoke = true; s.Flame2 = new Color(0.8f, 1f, 0.95f, 0.3f); break;
                case TankKind.Duke:         s.Smoke = true; s.Flame2 = new Color(0.45f, 0.8f, 0.3f, 0.45f); break;
                case TankKind.Poseidon:     s.Smoke = true; s.Flame2 = new Color(0.85f, 0.93f, 1f, 0.3f); break;
                case TankKind.CrossBow:
                case TankKind.MineLander:   break;                                               // 가볍게 — 원작대로 거의 무연
                default:                    s.Smoke = true; s.Flame2 = new Color(0.7f, 0.68f, 0.66f, 0.35f); break;   // 캐논·캐터펄트·캐롯
            }

            // 2번탄은 조금 더 진하게, 궁극기는 확실히 굵고 길게 — 무엇이 오는지 거리에서도 읽혀야 한다.
            if (shell == ShellKind.Special) { s.Size *= 1.25f; s.Life *= 1.15f; }
            if (ultimate) { s.Size *= 1.7f; s.Life *= 1.35f; s.Interval *= 0.7f; }
            return s;
        }

        static Style Mk(Color c, float size, float life, float interval, Vector3 drift, bool glow, bool cube)
            => new Style { Color = c, Size = size, Life = life, Interval = interval, Drift = drift, Glow = glow, Cube = cube };
    }
}
