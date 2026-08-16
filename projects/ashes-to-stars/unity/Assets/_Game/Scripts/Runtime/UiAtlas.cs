using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 한 장으로 생성한 UI 원본에서 공통 UI 조각을 잘라 쓰는 레지스트리.
    /// 좌표는 원본 PNG의 좌상단을 (0, 0)으로 한 픽셀 기준이다.
    /// </summary>
    public static class UiAtlas
    {
        public const int Width = 1448;
        public const int Height = 1086;
        const string ResourceKey = "ui/ashes_to_stars_ui_atlas";

        static Texture2D _texture;
        static bool _tried;

        static readonly Dictionary<string, Rect> Pieces = new Dictionary<string, Rect>
        {
            // 상단: 하단 고정바 5종
            ["territory"] = new Rect(12, 0, 120, 122),
            ["field"] = new Rect(140, 0, 135, 126),
            ["tower"] = new Rect(275, 0, 120, 128),
            ["worldmap"] = new Rect(407, 0, 135, 128),
            ["characters"] = new Rect(550, 0, 130, 128),

            // 두 번째 줄: 역할 아이콘
            ["tank"] = new Rect(8, 122, 125, 126),
            ["damage"] = new Rect(137, 122, 138, 126),
            ["healer"] = new Rect(278, 122, 142, 135),
            ["buffer"] = new Rect(426, 122, 145, 130),

            // 역할 아래: 목숨 파이프. 유니코드 하트는 기본 폰트에서 □로 나온다.
            ["heart"] = new Rect(21, 262, 61, 62),
            ["heart_broken"] = new Rect(108, 262, 65, 62),

            // 건물 줄: 영지 허브 목적지. 좌표는 아틀라스 실측.
            ["building_smith"] = new Rect(638, 570, 122, 124),
            ["building_auction"] = new Rect(329, 572, 130, 121),
            ["building_mausoleum"] = new Rect(793, 570, 121, 124),
            ["building_barracks"] = new Rect(483, 571, 123, 119),

            // 아틀라스 하단: 슬롯, 게이지, 패널, 버튼
            ["rarity_common"] = new Rect(8, 800, 80, 92),
            ["rarity_uncommon"] = new Rect(93, 800, 88, 92),
            ["rarity_rare"] = new Rect(185, 800, 91, 92),
            ["rarity_heroic"] = new Rect(280, 800, 89, 92),
            ["rarity_legendary"] = new Rect(373, 800, 96, 92),
            ["hp_frame"] = new Rect(303, 910, 228, 61),
            ["xp_frame"] = new Rect(538, 910, 222, 61),
            ["boss_hp_frame"] = new Rect(762, 905, 355, 71),
            ["panel"] = new Rect(746, 972, 181, 88),
            ["portrait_frame"] = new Rect(1124, 893, 79, 91),
            ["button_normal"] = new Rect(12, 996, 84, 63),
            ["button_hover"] = new Rect(103, 996, 83, 63),
            ["button_pressed"] = new Rect(193, 996, 82, 63),
        };

        public static readonly string[] RequiredKeys =
        {
            "territory", "field", "tower", "worldmap", "characters",
            "tank", "damage", "healer", "buffer",
            "heart", "heart_broken",
            "building_smith", "building_auction", "building_mausoleum", "building_barracks",
            "button_normal", "button_hover", "button_pressed", "panel", "hp_frame",
            "xp_frame", "portrait_frame", "boss_hp_frame",
            "rarity_common", "rarity_uncommon", "rarity_rare", "rarity_heroic", "rarity_legendary",
        };

        static Texture2D Texture
        {
            get
            {
                if (!_tried)
                {
                    _tried = true;
                    _texture = Resources.Load<Texture2D>(ResourceKey);
                }
                return _texture;
            }
        }

        public static bool IsReady => Texture != null;

        public static Rect RectFor(string key)
        {
            return Pieces.TryGetValue(key, out var rect) ? rect : Rect.zero;
        }

        public static bool Draw(Rect target, string key, Color? tint = null)
        {
            var texture = Texture;
            var source = RectFor(key);
            if (texture == null || source.width <= 0 || source.height <= 0) return false;

            var saved = GUI.color;
            GUI.color = tint ?? Color.white;
            GUI.DrawTextureWithTexCoords(target, texture, TextureCoords(source), true);
            GUI.color = saved;
            return true;
        }

        /// <summary>
        /// 상자 안에서 원본 비율을 지킨 가장 큰 칸. 가로로 넓은 칸에 초상 프레임을
        /// 그대로 넣으면 늘어나 보인다(오너 21:50).
        /// </summary>
        public static Rect FitInside(Rect box, float srcW, float srcH)
        {
            if (box.width <= 0f || box.height <= 0f) return box;
            if (srcW <= 0f || srcH <= 0f) return box;
            float scale = Mathf.Min(box.width / srcW, box.height / srcH);
            float w = srcW * scale;
            float h = srcH * scale;
            return new Rect(
                box.x + (box.width - w) * 0.5f,
                box.y + (box.height - h) * 0.5f,
                w, h);
        }

        /// <summary>아이콘·초상. 칸이 길어도 조각을 늘리지 않는다.</summary>
        public static bool DrawFit(Rect box, string key, Color? tint = null)
        {
            var source = RectFor(key);
            if (source.width <= 0f || source.height <= 0f) return false;
            return Draw(FitInside(box, source.width, source.height), key, tint);
        }

        /// <summary>호버·눌림을 아틀라스 3상태에 대응한다. 눌림이 호버보다 앞선다.</summary>
        public static string ButtonKey(bool hover, bool pressed)
        {
            if (pressed) return "button_pressed";
            if (hover) return "button_hover";
            return "button_normal";
        }

        /// <summary>
        /// qa_shot에는 마우스가 없어서 호버·눌림이 안 보인다.
        /// 견본 3칸을 나란히 그리면 조각이 서로 다른지 화면으로 판정할 수 있다.
        /// </summary>
        public static readonly (bool hover, bool pressed, string label)[] ButtonStateSamples =
        {
            (false, false, "보통"),
            (true, false, "호버"),
            (false, true, "눌림"),
        };

        public static bool QaShowButtonStates =>
            Environment.GetEnvironmentVariable("QA_UI_STATES") == "1";

        /// <summary>
        /// 등급 프레임 5종. RequiredKeys에만 있고 화면 소비처가 0곳이었다.
        /// 제작품은 일반. 견본은 QA_UI_RARITY=1일 때만 나란히 그린다.
        /// </summary>
        public static string RarityKey(GearGrade grade) => grade switch
        {
            GearGrade.Uncommon => "rarity_uncommon",
            GearGrade.Rare => "rarity_rare",
            GearGrade.Heroic => "rarity_heroic",
            GearGrade.Legendary => "rarity_legendary",
            _ => "rarity_common",
        };

        public static readonly (GearGrade grade, string label)[] RaritySamples =
        {
            (GearGrade.Common, "일반"),
            (GearGrade.Uncommon, "고급"),
            (GearGrade.Rare, "희귀"),
            (GearGrade.Heroic, "영웅"),
            (GearGrade.Legendary, "전설"),
        };

        public static bool QaShowRarity =>
            Environment.GetEnvironmentVariable("QA_UI_RARITY") == "1";

        public static bool DrawRarity(Rect target, GearGrade grade, Color? tint = null) =>
            Draw(target, RarityKey(grade), tint);

        /// <summary>
        /// 보스 HP 프레임. RequiredKeys·Pieces에만 있고 화면 소비처가 0곳이었다.
        /// 파티 hp_frame과 다른 조각 — 상단 중앙에서 위험으로 읽혀야 한다(§16-1·§16-5).
        /// </summary>
        public const string BossHpFrameKey = "boss_hp_frame";

        public static bool QaShowBossHp =>
            Environment.GetEnvironmentVariable("QA_BOSS_HP") == "1";

        /// <summary>§10-5 페이즈 수. BossBattle.CreateBosses와 같은 층 구간.</summary>
        public static int PhaseCountForFloor(int floor)
        {
            if (floor <= 5) return 2;
            if (floor <= 10) return 3;
            return 4;
        }

        public static readonly (float current, float max, int phases, string label)[] BossHpSamples =
        {
            (9000f, 9000f, 2, "만피·2페이즈"),
            (4500f, 9000f, 2, "1/2·경계"),
            (1200f, 9000f, 3, "낮음·3페이즈"),
        };

        /// <summary>프레임 + 채움 + 페이즈 경계선. 경계선이 없으면 §16-5가 화면에 없다.</summary>
        public static bool DrawBossHp(Rect target, float current, float max, int phaseCount)
        {
            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            var fill = ratio > 0.5f ? new Color(0.78f, 0.16f, 0.18f)
                     : ratio > 0.25f ? new Color(0.92f, 0.48f, 0.14f)
                                    : new Color(0.95f, 0.22f, 0.16f);
            bool framed = DrawMeter(target, BossHpFrameKey, ratio, fill);
            int phases = Mathf.Max(1, phaseCount);
            if (phases <= 1) return framed;

            var saved = GUI.color;
            GUI.color = new Color(1f, 0.92f, 0.55f, 0.95f);
            float padX = framed ? 10f : 0f;
            float padY = framed ? 6f : 4f;
            float inner = Mathf.Max(0f, target.width - padX * 2f);
            float h = Mathf.Max(4f, target.height - padY * 2f);
            for (int i = 1; i < phases; i++)
            {
                float x = target.x + padX + inner * (i / (float)phases);
                GUI.DrawTexture(new Rect(x - 1f, target.y + padY, 2f, h), Pixel);
            }
            GUI.color = saved;
            return framed;
        }

        /// <summary>
        /// 화면 이름 → 제목 옆 아이콘. 기본값을 worldmap(나침반)으로 두면
        /// 필드·탑이 전부 월드맵처럼 읽힌다 — 아틀라스에 조각이 있는데 소비처가 없던 함정.
        /// 매핑 없는 화면은 null. 호출부가 폴백을 정한다.
        /// </summary>
        public static string HeaderKey(string screen)
        {
            switch (screen)
            {
                case GameFlow.Field:
                case "필드":
                    return "field";
                case GameFlow.Tower:
                case "탑":
                    return "tower";
                case GameFlow.Estate:
                case "영지":
                    return "territory";
                case GameFlow.Character:
                case "캐릭터":
                    return "characters";
                case GameFlow.WorldMap:
                case "월드맵":
                    return "worldmap";
                default:
                    return null;
            }
        }

        /// <summary>기본·1차 직업명을 역할 아이콘 키로 접는다. 모르는 이름은 딜.</summary>
        public static string RoleKey(string job)
        {
            switch (job)
            {
                case "탱":
                case "수호기사":
                case "광전사":
                    return "tank";
                case "힐":
                case "사제":
                case "드루이드":
                    return "healer";
                case "버퍼":
                case "음유시인":
                case "주술사":
                case "정령사":
                    return "buffer";
                default:
                    return "damage";
            }
        }

        /// <summary>영지 건물 라벨 → 아틀라스 건물 실루엣. 없으면 null.</summary>
        public static string BuildingKey(string building)
        {
            switch (building)
            {
                case "대장간": return "building_smith";
                case "경매장": return "building_auction";
                case "영묘": return "building_mausoleum";
                case "수비대": return "building_barracks";
                default: return null;
            }
        }

        /// <summary>slot 0..2. 남은 목숨만 온전한 하트, 삭제·소모분은 깨진 하트.</summary>
        public static string HeartKey(int slot, int deathCount, bool deleted, int maxHearts = 3)
        {
            if (maxHearts < 1) maxHearts = 3;
            int lives = deleted ? 0 : Mathf.Max(0, maxHearts - deathCount);
            return slot < lives ? "heart" : "heart_broken";
        }

        /// <summary>목숨 칸을 아이콘으로 그린다. 특수 직업은 1칸(§3). 사용한 가로 폭을 돌려준다.</summary>
        public static float DrawHearts(Rect origin, int deathCount, bool deleted, int maxHearts = 3)
        {
            if (maxHearts < 1) maxHearts = 3;
            const float size = 22f, gap = 2f;
            for (int i = 0; i < maxHearts; i++)
            {
                var cell = new Rect(origin.x + i * (size + gap), origin.y, size, size);
                Draw(cell, HeartKey(i, deathCount, deleted, maxHearts));
            }
            return maxHearts * (size + gap);
        }

        /// <summary>
        /// 명부 한 칸이 쓰는 조각. 캐릭터 화면만 연결하고 편성이 글자면
        /// 같은 목숨이 두 화면에서 다르게 읽힌다.
        /// </summary>
        public static (string frame, string role, string heart0, string heart1, string heart2)
            SlotChrome(string job, int deathCount, bool deleted)
        {
            return (
                "portrait_frame",
                RoleKey(job),
                HeartKey(0, deathCount, deleted),
                HeartKey(1, deathCount, deleted),
                HeartKey(2, deathCount, deleted));
        }

        /// <summary>초상 뒤에 프레임만. 초상은 호출부가 이어서 그린다. 9-slice라 넓은 칸에서 안 늘어난다.</summary>
        public static bool DrawRosterFrame(Rect face)
        {
            return DrawSliced(new Rect(face.x - 2, face.y - 2, face.width + 4, face.height + 4),
                "portrait_frame", 16f);
        }

        /// <summary>초상 위 역할 뱃지 + 오른쪽 목숨. 초상을 그린 뒤에 부른다.</summary>
        public static float DrawRosterMarks(Rect face, Rect desc, string job, int deathCount, bool deleted)
        {
            Draw(new Rect(face.xMax - 8, face.yMax - 8, 20, 20), RoleKey(job));
            return DrawHearts(new Rect(desc.x, desc.y + 4, 80, 22), deathCount, deleted);
        }

        /// <summary>패널처럼 늘어나는 조각은 가장자리만 남기고 가운데를 늘린다.</summary>
        public static bool DrawSliced(Rect target, string key, float border = 12f, Color? tint = null)
        {
            var texture = Texture;
            var source = RectFor(key);
            if (texture == null || source.width <= 0 || source.height <= 0) return false;

            float b = Mathf.Min(border, source.width * 0.45f, source.height * 0.45f,
                                target.width * 0.45f, target.height * 0.45f);
            if (b < 1f) return Draw(target, key, tint);

            var saved = GUI.color;
            GUI.color = tint ?? Color.white;
            float sx = source.x, sy = source.y, sw = source.width, sh = source.height;
            float x0 = target.x, x1 = target.x + b, x2 = target.xMax - b, x3 = target.xMax;
            float y0 = target.y, y1 = target.y + b, y2 = target.yMax - b, y3 = target.yMax;
            DrawSrc(new Rect(x0, y0, b, b), new Rect(sx, sy, b, b), texture);
            DrawSrc(new Rect(x1, y0, x2 - x1, b), new Rect(sx + b, sy, sw - 2f * b, b), texture);
            DrawSrc(new Rect(x2, y0, b, b), new Rect(sx + sw - b, sy, b, b), texture);
            DrawSrc(new Rect(x0, y1, b, y2 - y1), new Rect(sx, sy + b, b, sh - 2f * b), texture);
            DrawSrc(new Rect(x1, y1, x2 - x1, y2 - y1), new Rect(sx + b, sy + b, sw - 2f * b, sh - 2f * b), texture);
            DrawSrc(new Rect(x2, y1, b, y2 - y1), new Rect(sx + sw - b, sy + b, b, sh - 2f * b), texture);
            DrawSrc(new Rect(x0, y2, b, b), new Rect(sx, sy + sh - b, b, b), texture);
            DrawSrc(new Rect(x1, y2, x2 - x1, b), new Rect(sx + b, sy + sh - b, sw - 2f * b, b), texture);
            DrawSrc(new Rect(x2, y2, b, b), new Rect(sx + sw - b, sy + sh - b, b, b), texture);
            GUI.color = saved;
            return true;
        }

        /// <summary>프레임 조각 안에 채움 막대를 그린다. 프레임이 없어도 막대는 그린다.</summary>
        public static bool DrawMeter(Rect target, string frameKey, float fill01, Color fill)
        {
            bool framed = Draw(target, frameKey);
            float padX = framed ? 10f : 0f;
            float padY = framed ? 6f : 0f;
            float w = Mathf.Max(0f, (target.width - padX * 2f) * Mathf.Clamp01(fill01));
            if (w > 0f)
            {
                var saved = GUI.color;
                GUI.color = fill;
                GUI.DrawTexture(new Rect(target.x + padX, target.y + padY, w, target.height - padY * 2f), Pixel);
                GUI.color = saved;
            }
            return framed;
        }

        static Texture2D _pixel;
        static Texture2D Pixel
        {
            get
            {
                if (_pixel == null)
                {
                    _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    _pixel.SetPixel(0, 0, Color.white);
                    _pixel.Apply();
                    _pixel.hideFlags = HideFlags.HideAndDontSave;
                }
                return _pixel;
            }
        }

        static void DrawSrc(Rect dest, Rect source, Texture2D texture)
        {
            if (dest.width <= 0f || dest.height <= 0f || source.width <= 0f || source.height <= 0f) return;
            GUI.DrawTextureWithTexCoords(dest, texture, TextureCoords(source), true);
        }

        static Rect TextureCoords(Rect source)
        {
            return new Rect(
                source.x / Width,
                (Height - source.y - source.height) / (float)Height,
                source.width / Width,
                source.height / Height);
        }
    }
}
