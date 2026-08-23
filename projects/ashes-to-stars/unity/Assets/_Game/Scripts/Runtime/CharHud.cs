using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 캐릭터창 명부·장비 둘레 글씨. 3열이 좁으면 이름이 잘리고,
    /// 장비 칸 폭(48)으로 라벨을 그리면 「장신구」가 잘린다.
    /// QA_NO면 옛 좁은 칸·48폭 라벨.
    /// CharacterScreen이 읽는다.
    /// </summary>
    public static class CharHud
    {
        public const string EnvShow = "QA_CHAR_HUD";
        public const string EnvNo = "QA_NO_CHAR_HUD";

        public const float OldListRatio = 0.36f;
        public const float ListRatio = 0.44f;
        public const float OldMinListW = 240f;
        public const float MinListW = 520f;
        public const float OldCellH = 132f;
        // 118이면 패널 보더(높이 비례 0.24)가 내용을 다 먹어 이름 끝글자가 하트에 덮였다
        // (실측 2026-08-24). 164이면 초상 62+이름 18+직업 16+하트 20이 전부 성립한다.
        public const float CellH = 164f;
        public const int Cols = 3;
        public const float MinCellW = 168f;
        public const float OldLabelW = 48f;
        public const float LabelW = 80f;
        public const float LabelH = 20f;
        public const float LabelGap = 2f;

        static bool _qaSeeded;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowQa
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static float ListW(Rect body)
        {
            float ratio = Blocked ? OldListRatio : ListRatio;
            float min = Blocked ? OldMinListW : MinListW;
            float w = Mathf.Max(min, body.width * ratio);
            if (w > body.width - 140f)
                w = Mathf.Max(180f, body.width * 0.34f);
            return w;
        }

        public static void RosterSplit(Rect r, out Rect list, out Rect stage)
        {
            const float gap = 10f;
            float listW = ListW(r);
            list = new Rect(r.x, r.y, listW, r.height);
            stage = new Rect(r.x + listW + gap, r.y, Mathf.Max(80f, r.width - listW - gap), r.height);
        }

        public static Rect RosterCell(Rect list, int index, int cols = Cols)
        {
            if (cols < 1) cols = 1;
            float gap = UiPages.RosterRowGap;
            float cw = (list.width - gap * (cols - 1)) / cols;
            float h = Blocked ? OldCellH : CellH;
            int x = index % cols;
            int y = index / cols;
            return new Rect(list.x + x * (cw + gap), list.y + y * (h + gap), cw, h);
        }

        public static string Line() => Blocked
            ? "명부가 좁고 장비 이름이 잘린다"
            : "명부 3열과 장비 이름이 잘리지 않는다(§16)";

        public static string SlotLabel(EquipSlot slot, GearItem worn)
        {
            string name = Equipment.SlotName(slot);
            if (worn != null && worn.Enhance > 0)
                return $"{name} +{worn.Enhance}";
            return name;
        }

        /// <summary>옛 길은 칸 폭 48이라 「장신구」가 잘린다. 새 길은 80·칸 아래.</summary>
        public static Rect EquipLabel(Rect stage, Rect slot) => EquipLabel(stage, slot, stage);

        /// <summary>
        /// 라벨은 패널의 **안쪽 금테 안 평평한 영역(flat)** 에만 둔다. stage로 클램프하면
        /// 좌측 라벨(장갑·갑옷)이 장식 여백에 걸려 가장자리에서 잘리고, 신발은 하단 금색
        /// 장식 위에 묻혀 판독 불가였다(실측 2026-08-24, polish_r65 샷). flat은
        /// stage↔chrome 중간선 — CharacterScreen.DrawEquipStudio가 정보 칸을 짜는
        /// 같은 보간 선이다. 아래 배치가 flat 바닥에 안 들어오면(좌하단 신발) 칸 위로 올린다.
        /// </summary>
        public static Rect EquipLabel(Rect stage, Rect slot, Rect flat)
        {
            if (Blocked)
                return UiPages.ClampIn(stage,
                    new Rect(slot.x, slot.yMax - 2f, slot.width, LabelH));
            var slotRect = new Rect(slot.x, slot.y, slot.width, slot.height);
            var below = UiPages.ClampIn(flat, new Rect(
                slot.center.x - LabelW * 0.5f, slot.yMax + LabelGap, LabelW, LabelH));
            if (!below.Overlaps(slotRect)) return below;
            var above = UiPages.ClampIn(flat, new Rect(
                slot.center.x - LabelW * 0.5f, slot.y - LabelGap - LabelH, LabelW, LabelH));
            if (!above.Overlaps(slotRect)) return above;
            return UiPages.ClampIn(stage, below);   // 최후: 평평한 영역이 너무 작으면 옛 배치
        }

        public static void EquipRingFit(Rect stage, Rect face, out float ringX, out float ringY)
        {
            float half = UiPages.EquipSlotSize * 0.5f;
            float side = Blocked ? half : LabelW * 0.5f;
            float below = Blocked ? LabelH : LabelH + LabelGap + 2f;
            const float pad = 8f;
            float cx = face.center.x;
            float cy = face.center.y;
            ringX = Mathf.Min(face.width * 0.50f + 16f,
                cx - stage.x - side - pad,
                stage.xMax - cx - side - pad);
            ringY = Mathf.Min(face.height * 0.42f + 16f,
                cy - stage.y - half - pad - 52f,
                stage.yMax - cy - half - pad - below);
            ringX = Mathf.Max(24f, ringX);
            ringY = Mathf.Max(24f, ringY);
        }

        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            Equipment.SeedCraftedLoadoutForQa(roster[0]);
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
