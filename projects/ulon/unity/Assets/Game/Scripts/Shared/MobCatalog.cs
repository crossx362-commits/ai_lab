namespace Ulon.Shared
{
    public struct MobDefinition
    {
        public string Id;
        public string DisplayName;
        public float MaxHp;
        public float Height;
    }

    public static class MobCatalog
    {
        public const string Skeleton = "skeleton";
        public const string Bandit = "bandit";
        public const string Raider = "raider";
        public const string Rogue = "rogue";
        public const string Knight = "knight";
        public const string Acolyte = "acolyte";
        public const string Minion = "minion";
        public const string SkelRogue = "skelrogue";
        public const string SkelMage = "skelmage";
        public const string Squire = "squire";
        public const string Cutthroat = "cutthroat";
        public const string Seer = "seer";
        public const string Bonekin = "bonekin";
        public const string Runt = "runt";
        public const string BoneWarden = "bonewarden";
        public const string ShadowCaptain = "shadowcaptain";
        public const string Hexarch = "hexarch";
        public const string IronTyrant = "irontyrant";
        public const string Hart = "hart";
        public const string Boar = "boar";
        /// <summary>사냥터에 도는 **적대 잡몹** 종수(`HuntRoster`). 원장(mobs.json) 레코드 수(보스·조련·추가 잡몹 포함 20종)와 다르다.</summary>
        public const int HostileKindCount = 8;

        public static bool TryGet(string id, out MobDefinition definition)
        {
            // 12.2 — 수치 원장은 StreamingAssets/Data/mobs.json. 아래 if-체인은 파일이 없을 때의 폴백이다.
            if (MobData.TryGet(id, out var data))
            {
                definition = new MobDefinition
                {
                    Id = data.id,
                    DisplayName = data.name,
                    MaxHp = data.hp,
                    Height = data.height
                };
                return true;
            }
            if (id == Skeleton)
            {
                definition = new MobDefinition
                {
                    Id = Skeleton,
                    DisplayName = "스켈레톤",
                    MaxHp = 30f,
                    Height = 1.55f
                };
                return true;
            }
            if (id == Bandit)
            {
                definition = new MobDefinition
                {
                    Id = Bandit,
                    DisplayName = "도적",
                    MaxHp = 45f,
                    Height = 1.75f
                };
                return true;
            }
            if (id == Raider)
            {
                definition = new MobDefinition
                {
                    Id = Raider,
                    DisplayName = "야만인",
                    MaxHp = 60f,
                    Height = 1.85f
                };
                return true;
            }
            if (id == Rogue)
            {
                definition = new MobDefinition
                {
                    Id = Rogue,
                    DisplayName = "자객",
                    MaxHp = 40f,
                    Height = 1.70f
                };
                return true;
            }
            if (id == Knight)
            {
                definition = new MobDefinition
                {
                    Id = Knight,
                    DisplayName = "기사",
                    MaxHp = 70f,
                    Height = 1.80f
                };
                return true;
            }
            if (id == Acolyte)
            {
                definition = new MobDefinition
                {
                    Id = Acolyte,
                    DisplayName = "주술사",
                    MaxHp = 50f,
                    Height = 1.65f
                };
                return true;
            }
            if (id == Minion)
            {
                definition = new MobDefinition
                {
                    Id = Minion,
                    DisplayName = "졸병",
                    MaxHp = 22f,
                    Height = 1.35f
                };
                return true;
            }
            if (id == SkelRogue)
            {
                definition = new MobDefinition
                {
                    Id = SkelRogue,
                    DisplayName = "해골도적",
                    MaxHp = 28f,
                    Height = 1.50f
                };
                return true;
            }
            if (id == SkelMage)
            {
                definition = new MobDefinition
                {
                    Id = SkelMage,
                    DisplayName = "해골주술사",
                    MaxHp = 38f,
                    Height = 1.85f
                };
                return true;
            }
            if (id == Squire)
            {
                definition = new MobDefinition
                {
                    Id = Squire,
                    DisplayName = "종자",
                    MaxHp = 48f,
                    Height = 1.58f
                };
                return true;
            }
            if (id == Cutthroat)
            {
                definition = new MobDefinition
                {
                    Id = Cutthroat,
                    DisplayName = "칼잡이",
                    MaxHp = 36f,
                    Height = 1.64f
                };
                return true;
            }
            if (id == Seer)
            {
                definition = new MobDefinition
                {
                    Id = Seer,
                    DisplayName = "은둔사제",
                    MaxHp = 42f,
                    Height = 1.78f
                };
                return true;
            }
            if (id == Bonekin)
            {
                definition = new MobDefinition
                {
                    Id = Bonekin,
                    DisplayName = "갱도해골",
                    MaxHp = 26f,
                    Height = 1.42f
                };
                return true;
            }
            if (id == Runt)
            {
                definition = new MobDefinition
                {
                    Id = Runt,
                    DisplayName = "잔당",
                    MaxHp = 16f,
                    Height = 1.12f
                };
                return true;
            }
            if (id == BoneWarden)
            {
                definition = new MobDefinition
                {
                    Id = BoneWarden,
                    DisplayName = "본워든",
                    MaxHp = 120f,
                    Height = 2.15f
                };
                return true;
            }
            if (id == ShadowCaptain)
            {
                definition = new MobDefinition
                {
                    Id = ShadowCaptain,
                    DisplayName = "섀도우캡틴",
                    MaxHp = 150f,
                    Height = 2.45f
                };
                return true;
            }
            if (id == IronTyrant)
            {
                definition = new MobDefinition
                {
                    Id = IronTyrant,
                    DisplayName = "강철폭군",
                    MaxHp = 210f,
                    Height = 2.65f
                };
                return true;
            }
            if (id == Hexarch)
            {
                definition = new MobDefinition
                {
                    Id = Hexarch,
                    DisplayName = "헥사크",
                    MaxHp = 180f,
                    Height = 2.60f
                };
                return true;
            }
            if (id == Hart)
            {
                definition = new MobDefinition
                {
                    Id = Hart,
                    DisplayName = "야생하트",
                    MaxHp = 20f,
                    Height = 0.9f
                };
                return true;
            }
            if (id == Boar)
            {
                definition = new MobDefinition
                {
                    Id = Boar,
                    DisplayName = "야생멧돼지",
                    MaxHp = 24f,
                    Height = 0.95f
                };
                return true;
            }
            definition = default(MobDefinition);
            return false;
        }

        public static float MaxHpOf(string id)
        {
            return TryGet(id, out MobDefinition definition) ? definition.MaxHp : 30f;
        }

        public static string DisplayNameOf(string id)
        {
            return TryGet(id, out MobDefinition definition) ? definition.DisplayName : "스켈레톤";
        }

        public static float HeightOf(string id)
        {
            return TryGet(id, out MobDefinition definition) ? definition.Height : 1.55f;
        }

        public static bool IsKnown(string id)
        {
            return TryGet(id, out _);
        }

        public static bool IsBoss(string id)
        {
            if (MobData.TryGet(id, out var data)) return data.boss;
            return id == BoneWarden || id == ShadowCaptain || id == Hexarch || id == IronTyrant;
        }

        public static string KillDropOf(string id)
        {
            if (MobData.TryGet(id, out var data)) return data.drop ?? "";
            if (id == BoneWarden)
                return ItemCatalog.WardenCrest;
            if (id == ShadowCaptain)
                return ItemCatalog.CaptainSigil;
            if (id == Hexarch)
                return ItemCatalog.HexSeal;
            if (id == IronTyrant)
                return ItemCatalog.TyrantCore;
            return "";
        }

        public static void LoreStats(string id, out int str, out int resist, out int dmgMin, out int dmgMax)
        {
            str = 10;
            resist = 0;
            dmgMin = 2;
            dmgMax = 4;
            if (MobData.TryGet(id, out var data))
            {
                str = data.str;
                resist = data.resist;
                dmgMin = data.dmgMin;
                dmgMax = data.dmgMax;
                return;
            }
            if (id == Skeleton) { str = 20; resist = 2; dmgMin = 3; dmgMax = 6; }
            else if (id == Bandit) { str = 28; resist = 1; dmgMin = 4; dmgMax = 8; }
            else if (id == Raider) { str = 40; resist = 2; dmgMin = 6; dmgMax = 10; }
            else if (id == Rogue) { str = 22; resist = 1; dmgMin = 4; dmgMax = 7; }
            else if (id == Knight) { str = 45; resist = 5; dmgMin = 6; dmgMax = 12; }
            else if (id == Acolyte) { str = 18; resist = 8; dmgMin = 3; dmgMax = 6; }
            else if (id == Minion) { str = 12; resist = 0; dmgMin = 2; dmgMax = 4; }
            else if (id == SkelRogue) { str = 18; resist = 2; dmgMin = 3; dmgMax = 6; }
            else if (id == SkelMage) { str = 16; resist = 7; dmgMin = 3; dmgMax = 7; }
            else if (id == Squire) { str = 32; resist = 3; dmgMin = 4; dmgMax = 9; }
            else if (id == Cutthroat) { str = 24; resist = 1; dmgMin = 4; dmgMax = 8; }
            else if (id == Seer) { str = 14; resist = 9; dmgMin = 2; dmgMax = 6; }
            else if (id == Bonekin) { str = 19; resist = 3; dmgMin = 3; dmgMax = 5; }
            else if (id == Runt) { str = 10; resist = 0; dmgMin = 1; dmgMax = 3; }
            else if (id == BoneWarden) { str = 55; resist = 8; dmgMin = 8; dmgMax = 16; }
            else if (id == ShadowCaptain) { str = 50; resist = 6; dmgMin = 10; dmgMax = 18; }
            else if (id == Hexarch) { str = 30; resist = 12; dmgMin = 8; dmgMax = 20; }
            else if (id == IronTyrant) { str = 62; resist = 10; dmgMin = 12; dmgMax = 22; }
            else if (id == Hart) { str = 8; resist = 0; dmgMin = 1; dmgMax = 2; }
            else if (id == Boar) { str = 10; resist = 0; dmgMin = 1; dmgMax = 3; }
        }

        public static bool TamableOf(string id)
        {
            if (MobData.TryGet(id, out var data)) return data.tamable;
            return id == Hart || id == Boar;
        }

        public static string DamageBandOf(string id)
        {
            LoreStats(id, out _, out _, out int min, out int max);
            return min + "-" + max;
        }
    }
}
