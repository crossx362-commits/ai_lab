namespace Ulon.Shared
{
    /// <summary>기획 §18.13 Target Cursor 종류. HUD·클릭·게이트가 같이 읽는다.</summary>
    public enum TargetKind
    {
        None = 0,
        Heal,
        Spell,
        Gather,
        Interact
    }

    public enum TargetInteract
    {
        None = 0,
        Evaluate,
        Lore,
        Vet,
        Scroll,
        Peace
    }

    /// <summary>
    /// 대상 지정 커서 원장. 안내 문구는 여기만. 판정은 서버(<c>TryHeal</c>·<c>TryCast</c>·<c>TryGather</c>).
    /// </summary>
    public static class TargetKinds
    {
        /// <summary>네거티브 컨트롤 — 켜면 커서가 안 뜨고 지정 모드도 안 열린다.</summary>
        public static bool NcHide;

        public const string PromptHeal = "치유 대상을 지정하세요";
        public const string PromptSpell = "주문 대상을 지정하세요";
        public const string PromptGather = "채집 대상을 지정하세요";
        public const string PromptInteract = "대상을 지정하세요";
        public const string CancelHint = "Esc·우클릭 취소";

        public static string PromptOf(TargetKind kind)
        {
            if (NcHide)
                return "";
            switch (kind)
            {
                case TargetKind.Heal: return PromptHeal;
                case TargetKind.Spell: return PromptSpell;
                case TargetKind.Gather: return PromptGather;
                case TargetKind.Interact: return PromptInteract;
                default: return "";
            }
        }
    }
}
