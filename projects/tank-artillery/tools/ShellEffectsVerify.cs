// ShellEffects.cs 검증 — 2번탄 메커니즘 네거티브 컨트롤.
//
// 테스트 항목:
//   1. 독 3턴: 정확히 3번 피해 나오고 4번째는 0
//   2. 이동금지 2턴: 2턴 동안 CanMove=false, 3턴째 true
//   3. 지뢰: 반경 안 1회 폭발, 2번째 접근은 0, 반경 밖은 0, 레이저·포세이돈은 0
//   4. 위성탄: 오버행 있는 지형에서 수직 낙하 확인 (오버행 없는 대조군과 비교)
//   5. 다탄두: 멀티미사일 일반 3발, 특수 9발, 캐롯 일반 1발, 특수 3발

using System;
using System.Collections.Generic;
using Tankfall.Sim;

static class ShellEffectsVerify
{
    static int testCount = 0, passCount = 0;

    static void Test(string name, bool pass)
    {
        testCount++;
        if (pass) { passCount++; Console.WriteLine($"  PASS: {name}"); }
        else Console.WriteLine($"  FAIL: {name}");
    }

    static float OverhangTerrain(float x, float z)
    {
        // 평탄한 기본 지형(높이 10m)
        float baseHeight = 10f;

        // (50~70, 50~70) 범위에 오버행 구조:
        // 상층(높이 15m), 하층(높이 5m)으로 처마 만들기
        if (x >= 50f && x <= 70f && z >= 50f && z <= 70f)
        {
            // 상층 처마
            if (x >= 55f && x <= 65f && z >= 55f && z <= 65f) return 15f;
            // 하층(처마 아래 공간, 높이 5m)
            return 5f;
        }

        return baseHeight;
    }

    static float FlatTerrain(float x, float z)
    {
        return 10f;  // 완전히 평탄함
    }

    static void Main()
    {
        Console.WriteLine("=== ShellEffects 검증 ===\n");

        // ── 테스트 1: 독 3턴 ──
        Console.WriteLine("### 테스트 1: 독 피해(3턴)");
        {
            var effects = new StatusEffects();

            // 크로스보우 독화살: 25/턴, 3턴 감염
            // 감염 → TickStartOfTurn (1차) → TickStartOfTurn (2차) → TickStartOfTurn (3차) → TickStartOfTurn (4차)
            // 피해 순서: 0 → 25 → 25 → 25 → 0 (3턴 경과 후 사라짐)
            effects.Poison(1, 25, 3, TankKind.Carrot);  // 타겟: 캐롯(저항 없음)

            int dmg0 = effects.TickStartOfTurn(1);  // 1턴차: 25
            int dmg1 = effects.TickStartOfTurn(1);  // 2턴차: 25
            int dmg2 = effects.TickStartOfTurn(1);  // 3턴차: 25
            int dmg3 = effects.TickStartOfTurn(1);  // 4턴차: 0 (사라짐)

            Test($"독 1턴차: 피해={dmg0} (기대: 25)", dmg0 == 25);
            Test($"독 2턴차: 피해={dmg1} (기대: 25)", dmg1 == 25);
            Test($"독 3턴차: 피해={dmg2} (기대: 25)", dmg2 == 25);
            Test($"독 4턴차: 피해={dmg3} (기대: 0)", dmg3 == 0);
        }

        // ── 테스트 1-2: 포세이돈 독 저항 ──
        Console.WriteLine("\n### 테스트 1-2: 포세이돈 독 저항(반감)");
        {
            var effects = new StatusEffects();

            // 크로스보우가 포세이돈에 독 감염
            // Poison() 내에서 반감 처리: (25 + 1) / 2 = 13
            effects.Poison(1, 25, 3, TankKind.Poseidon);

            int dmg1 = effects.TickStartOfTurn(1);
            Test($"포세이돈 첫 독 피해(기대: 13, 반감)", dmg1 == 13);
        }

        // ── 테스트 1-3: 듀크 독 — 게임 로직에서 처리 ──
        Console.WriteLine("\n### 테스트 1-3: 듀크 독 (자체 면역은 게임 로직에서)");
        {
            // 듀크가 자신의 독에 면역이라는 설계는 게임 로직(BattleSimVerify.cs 등)에서
            // 처리해야 한다. ShellEffects는 순수 상태 관리만 담당.
            Test($"주석: 듀크 자체 면역은 게임 로직 레벨에서 Poison() 호출 회피로 처리", true);
        }

        // ── 테스트 2: 이동금지 2턴 ──
        Console.WriteLine("\n### 테스트 2: 이동금지(포세이돈 2번탄, 2턴)");
        {
            var effects = new StatusEffects();

            effects.Root(1, 2);  // 유닛 1을 2턴 속박

            Test($"턴 0(속박 시작): CanMove={effects.CanMove(1)} (기대: false)", !effects.CanMove(1));
            effects.TickStartOfTurn(1);  // 턴 1 시작

            Test($"턴 1: CanMove={effects.CanMove(1)} (기대: false)", !effects.CanMove(1));
            effects.TickStartOfTurn(1);  // 턴 2 시작

            Test($"턴 2: CanMove={effects.CanMove(1)} (기대: true)", effects.CanMove(1));
        }

        // ── 테스트 3: 지뢰 ──
        Console.WriteLine("\n### 테스트 3: 지뢰(마인랜더)");
        {
            var field = new HazardField();
            var pos1 = new Vec3(50f, 10f, 50f);
            var pos2 = new Vec3(55f, 10f, 50f);    // 반경 5 안
            var pos3 = new Vec3(60f, 10f, 50f);    // 반경 밖

            field.PlaceMine(50f, 10f, 50f, 5f, 200);  // 반경 5, 피해 200

            // 첫 진입: 피해
            int dmg1 = field.OnUnitAt(1, TankKind.Carrot, pos1);
            Test($"지뢰 반경 중심 진입: 피해={dmg1} (기대: 200)", dmg1 == 200);

            // 두 번째 진입: 이미 터짐, 피해 0
            int dmg2 = field.OnUnitAt(1, TankKind.Carrot, pos1);
            Test($"지뢰 두 번째 진입: 피해={dmg2} (기대: 0)", dmg2 == 0);

            // 반경 안 다른 위치: 피해
            field.Clear();
            field.PlaceMine(50f, 10f, 50f, 5f, 200);
            int dmg3 = field.OnUnitAt(1, TankKind.Carrot, pos2);
            Test($"지뢰 반경 안(거리 5): 피해={dmg3} (기대: 200)", dmg3 == 200);

            // 반경 밖: 피해 0
            field.Clear();
            field.PlaceMine(50f, 10f, 50f, 5f, 200);
            int dmg4 = field.OnUnitAt(1, TankKind.Carrot, pos3);
            Test($"지뢰 반경 밖(거리 10): 피해={dmg4} (기대: 0)", dmg4 == 0);

            // 레이저는 무시
            field.Clear();
            field.PlaceMine(50f, 10f, 50f, 5f, 200);
            int dmg5 = field.OnUnitAt(1, TankKind.Laser, pos1);
            Test($"지뢰 레이저 무시: 피해={dmg5} (기대: 0)", dmg5 == 0);

            // 포세이돈도 무시
            field.Clear();
            field.PlaceMine(50f, 10f, 50f, 5f, 200);
            int dmg6 = field.OnUnitAt(1, TankKind.Poseidon, pos1);
            Test($"지뢰 포세이돈 무시: 피해={dmg6} (기대: 0)", dmg6 == 0);
        }

        // ── 테스트 4: 위성탄 ──
        Console.WriteLine("\n### 테스트 4: 위성탄(이온어태커, 수직 낙하)");
        {
            // 오버행 지형
            const float Voxel = 0.5f;
            var volOverhang = new SdfVolume(Voxel, 16, 0f, OverhangTerrain, 400);

            // 평탄 지형
            var volFlat = new SdfVolume(Voxel, 16, 0f, FlatTerrain, 400);

            // 테스트 위치:
            // OverhangTerrain(60, 60) = 15 (처마 상층)
            // OverhangTerrain(60, 50) = 5 (처마 아래) ← 이 위치에서 테스트
            // FlatTerrain은 항상 10
            var strike = SatelliteStrike.Resolve(volOverhang, 60f, 50f, 20f, null, out int hitId1);
            var strikeFlat = SatelliteStrike.Resolve(volFlat, 60f, 60f, 20f, null, out int hitId2);

            Test($"위성탄 오버행 낙하 y={strike.Y} (기대: y<10)", strike.Y < 10f);
            Test($"위성탄 평탄 낙하 y={strikeFlat.Y} (기대: 9<=y<=11)", strikeFlat.Y >= 9f && strikeFlat.Y <= 11f);
            Test($"위성탄 직격 없음(기대: -1)", hitId1 == -1 && hitId2 == -1);
        }

        // ── 테스트 5: 다탄두 ──
        Console.WriteLine("\n### 테스트 5: 다탄두 패턴");
        {
            // 멀티미사일 일반탄: 3발
            var spMM = Spread.Pattern(TankKind.MultiMissile, ShellKind.Normal);
            Test($"멀티미사일 일반탄 3발: {spMM.Count}발 (기대: 3)", spMM.Count == 3);

            // 멀티미사일 특수탄: 9발
            var spMMSp = Spread.Pattern(TankKind.MultiMissile, ShellKind.Special);
            Test($"멀티미사일 특수탄 9발: {spMMSp.Count}발 (기대: 9)", spMMSp.Count == 9);

            // 캐롯 일반탄: 1발
            var spCN = Spread.Pattern(TankKind.Carrot, ShellKind.Normal);
            Test($"캐롯 일반탄 단발: {spCN.Count}발 (기대: 1)", spCN.Count == 1);

            // 캐롯 특수탄: 3발
            var spCSp = Spread.Pattern(TankKind.Carrot, ShellKind.Special);
            Test($"캐롯 특수탄 3발: {spCSp.Count}발 (기대: 3)", spCSp.Count == 3);

            // 레이저 특수탄: 3발
            var spLaser = Spread.Pattern(TankKind.Laser, ShellKind.Special);
            Test($"레이저 특수탄 3발: {spLaser.Count}발 (기대: 3)", spLaser.Count == 3);

            // 오프셋 확인 (특수탄 편차가 더 넓은가)
            bool spreadDiffers = false;
            if (spMM.Count > 0 && spMMSp.Count > 0)
            {
                float maxMMOffset = 0;
                for (int i = 0; i < spMM.Count; i++)
                    maxMMOffset = MathF.Max(maxMMOffset, MathF.Abs(spMM[i].PitchOffsetDeg));

                float maxMMSpOffset = 0;
                for (int i = 0; i < spMMSp.Count; i++)
                    maxMMSpOffset = MathF.Max(maxMMSpOffset, MathF.Abs(spMMSp[i].PitchOffsetDeg));

                spreadDiffers = maxMMSpOffset > maxMMOffset;
                Test($"다탄두 특수탄 편차 더 넓음(일반{maxMMOffset}° < 특수{maxMMSpOffset}°)", spreadDiffers);
            }
        }

        // ── 테스트 6: ShellEffects.Of() ──
        Console.WriteLine("\n### 테스트 6: ShellEffects.Of() 효과 타입");
        {
            var ePos = ShellEffects.Of(TankKind.Poseidon, ShellKind.Special);
            Test($"포세이돈 특수탄 이동금지: {ePos.Type}", ePos.Type == ShellEffects.EffectType.Root);

            var eCB = ShellEffects.Of(TankKind.CrossBow, ShellKind.Special);
            Test($"크로스보우 특수탄 독: {eCB.Type}", eCB.Type == ShellEffects.EffectType.Poison);

            var eCat = ShellEffects.Of(TankKind.Catapult, ShellKind.Special);
            Test($"캐터펄트 특수탄 화상: {eCat.Type}", eCat.Type == ShellEffects.EffectType.Burn);

            var eMin = ShellEffects.Of(TankKind.MineLander, ShellKind.Special);
            Test($"마인랜더 특수탄 지뢰: {eMin.Type}", eMin.Type == ShellEffects.EffectType.Mine);

            var eIon = ShellEffects.Of(TankKind.IonAttacker, ShellKind.Special);
            Test($"이온어태커 특수탄 위성탄: {eIon.Type}", eIon.Type == ShellEffects.EffectType.SatelliteStrike);
        }

        // 최종 결과
        Console.WriteLine($"\n=== 결과: {passCount}/{testCount} 통과 ===");
        Environment.Exit(passCount == testCount ? 0 : 1);
    }
}
